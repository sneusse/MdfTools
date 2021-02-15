#if RELEASE
#define PARALLEL
//#define PARALLEL_GAPS
#endif

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MdfTools.Shared;
using MdfTools.Shared.Data.Base;

namespace MdfTools.V4
{
    public class Mdf4Sampler
    {
        public long SampleOffset { get; }
        public long SampleCount { get; }

        public BufferView<Mdf4Channel>[] Buffers { get; }

        internal Mdf4Sampler(IEnumerable<Mdf4Channel> channels)
        {
            var channelsByGroup = channels.GroupBy(k => k.ChannelGroup);
        }

        internal Mdf4Sampler(IEnumerable<Mdf4Channel> chanz, ulong sampleOffset, ulong sampleCnt)
        {
            var channels = chanz.ToArray();


            var src = channels.First().ChannelGroup;
            var recLen = src.RecordLength;
            var sampleToRecordFirst = sampleOffset * recLen;
            var sampleToRecordLast = (sampleOffset + sampleCnt) * recLen;

            var blis = src.BlockLoadingInfos;

            //TODO: Auf binarysearch umstellen....
            var firstMapIndex = Array.FindIndex(blis, 0, map => map.BytePosition <= (long) sampleToRecordFirst);
            firstMapIndex = firstMapIndex == -1 ? 0 : firstMapIndex;
            var lastMapIndex =
                Array.FindIndex(blis, firstMapIndex, map => map.BytePosition >= (long) sampleToRecordLast);
            lastMapIndex = lastMapIndex == -1 ? blis.Length - 1 : lastMapIndex;

            if (lastMapIndex >= 0)
            {
                SampleCount = blis[lastMapIndex].SampleIndex + blis[lastMapIndex].SampleCount -
                              blis[firstMapIndex].SampleIndex;

                SampleOffset = blis[firstMapIndex].SampleIndex;
            }
            else
            {
                SampleCount = 0;
                SampleOffset = 0;
            }

            var buffers = channels.Select(k => k.CreateBuffer(SampleCount)).ToArray();
            Buffers = buffers.Select(k => k.CreateView<Mdf4Channel>()).ToArray();

            using var _ = Mdf4File.Metrics.SampleReading.Measure(SampleCount, SampleCount * recLen);

            // MDF4 allows records to be spread across multiple blocks.
            // "Why is that so?", you might ask. "This is stupid!", you might say. I might agree.
            // Consider this example:
            // Blocks  -> [.......][......][............][...........]
            // Records ->  AAABBBC  CCDDDE  EEFFFGGGHHHJ  JJKKKLLLMMM
            // This implementation will process the aligned stuff fast (parallel) and sync up
            // on the 'gaps' as the last step. ( | = parallel, -> sequential)
            // : AAABBB | DDD | FFFGGGHHH | KKKLLLMMM / sync / CCC -> EEE -> JJJ

#if PARALLEL
            Parallel.For(firstMapIndex, lastMapIndex + 1, i =>
#else
            for (var i = firstMapIndex; i <= lastMapIndex; ++i)
#endif
                         {
                             var bli = blis[i];
                             var blk = bli.Block;

                             var recordBuffer = MdfBufferPool.Rent(blk.ByteLength);
                             blk.CopyTo(recordBuffer, 0);
                             bli.CopyGaps(recordBuffer, src.GapBuffer);

                             //TODO: find better metric -.-
                             var threadMetric = bli.SampleCount * channels.Length;
                             var threadCount = (int) Math.Ceiling(threadMetric / 1000000000.0);

#if PARALLEL
                             //NORMAL VERSION
                             if (threadCount <= 1)
                             {
#endif
                                 var byteOffset = (ulong) bli.Alignment.LeftByteOffset;
                                 var sampleStart = (ulong) bli.SampleIndex;
                                 var sampleCount = (uint) bli.SampleCount;

                                 for (var cIndex = 0; cIndex < channels.Length; cIndex++)
                                 {
                                     var buffer = buffers[cIndex];
                                     buffer.Update(recordBuffer, byteOffset, sampleStart, sampleCount);
                                 }
#if PARALLEL
                             }

                             // THREADED VERSION
                             else
                             {
                                 var numThreads = threadCount;
                                 var split = bli.SampleCount / numThreads;
                                 var rest = bli.SampleCount % numThreads;

                                 var byteOffset = bli.Alignment.LeftByteOffset;
                                 Parallel.For(0, numThreads, i =>
                                 {
                                     var sampleStart = (ulong) (bli.SampleIndex + i * split);
                                     var sampleCount = (uint) (split + rest);

                                     for (var cIndex = 0; cIndex < channels.Length; cIndex++)
                                     {
                                         var buffer = buffers[cIndex];
                                         buffer.Update(recordBuffer, (ulong) byteOffset, sampleStart, sampleCount);
                                     }
                                 });
                             }
#endif
                             MdfBufferPool.Return(recordBuffer);
                         }
#if PARALLEL
            );
#endif
            if (src.GapBuffer != null && src.GapBuffer.Length > 0)
            {
                var firstSample = 0;
                var lastSample = (int) SampleCount;

                // TODO: Binarysearch.
                var firstGap = Array.FindIndex(src.GapIndexToSampleIndex, i => i >= firstSample);
                var lastGap = Array.FindIndex(src.GapIndexToSampleIndex, i => i >= lastSample);

                //TODO: fix checks (out of range, before, after, ...)
                firstGap = firstGap == -1 ? 0 : firstGap;
                lastGap = lastGap == -1 ? src.GapIndexToSampleIndex.Length : lastGap;

#if PARALLEL && PARALLEL_GAPS
                Parallel.For(0, channels.Length, cIndex =>
#else
                for (var cIndex = 0; cIndex < channels.Length; cIndex++)
#endif
                {
                    var sampleBuffer = buffers[cIndex];
                    var byteOffset = firstGap * src.RecordLength;

                    for (var gapIndex = firstGap; gapIndex < lastGap; gapIndex++)
                    {
                        sampleBuffer.Update(src.GapBuffer, (ulong) byteOffset,
                                            (ulong) src.GapIndexToSampleIndex[gapIndex], 1);
                        byteOffset += (int) src.RecordLength;
                    }
                }
#if RELEASE && PARALLEL_GAPS
                );
#endif
            }
        }

        public static BufferView<Mdf4Channel>[] LoadFull(IEnumerable<Mdf4Channel> channels)
        {
            var byGroup = channels.GroupBy(k => k.ChannelGroup);

            var stuff = new ConcurrentBag<Mdf4Sampler>();
#if RELEASE
            Parallel.ForEach(byGroup, grouping =>
#else
            foreach (var grouping in byGroup)
#endif
                             {
                                 var grp = grouping.Key;
                                 var smp = Create(grouping, 0, grp.SampleCount);
                                 stuff.Add(smp);
                             }
#if RELEASE
            );
#endif

            return stuff.SelectMany(k => k.Buffers).ToArray();
        }

        public static Mdf4Sampler Create(IEnumerable<Mdf4Channel> channels, ulong sampleOffset, ulong sampleCnt)
        {
            return new Mdf4Sampler(channels, sampleOffset, sampleCnt);
        }
    }
}
