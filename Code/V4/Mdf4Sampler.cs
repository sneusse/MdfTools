#define PARALLEL
#define PARALLEL_GROUPS
#define PARALLEL_GAPS
//#define MERGE_BLOCKS

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MdfTools.Shared;
using MdfTools.Shared.Data.Base;

namespace MdfTools.V4
{
    public static class MissingStuff
    {
        public static IEnumerable<IReadOnlyList<T>> ChunkList<T>(this IEnumerable<T> items, int chunkSize)
        {
            List<T> tmp = new List<T>(chunkSize);
            foreach (var item in items)
            {
                tmp.Add(item);
                if (tmp.Count >= chunkSize)
                {
                    yield return tmp;
                    tmp = new List<T>(chunkSize);
                }
            }
        }
    }

    public class Mdf4Sampler : IDisposable
    {
        public long SampleOffset { get; }
        public long SampleCount { get; }
        public long BytesLoaded;
        public long BytesAllocated;

        public BufferView<Mdf4Channel>[] Buffers { get; }

        internal Mdf4Sampler(IEnumerable<Mdf4Channel> chanz, ulong sampleOffset, ulong sampleCnt, int blockStride = 1)
        {
            var channels = chanz.ToArray();


            var src = channels.First().ChannelGroup;
            var recLen = src.RecordLength;
            var blis = src.BlockLoadingInfos;


            if (blis.Length == 0)
            {
                Buffers = Array.Empty<BufferView<Mdf4Channel>>();
                return;
            }


            //TODO: Auf binarysearch umstellen....
            // var sampleToRecordFirst = sampleOffset * recLen;
            // var sampleToRecordLast = (sampleOffset + sampleCnt) * recLen;
            // var firstMapIndex = Array.FindIndex(blis, 0, map => map.BytePosition >= (long) sampleToRecordFirst);
            // firstMapIndex = firstMapIndex == -1 ? 0 : firstMapIndex;
            // var lastMapIndex =
            //     Array.FindIndex(blis, firstMapIndex, map => map.BytePosition >= (long) sampleToRecordLast);
            // lastMapIndex = lastMapIndex == -1 ? blis.Length - 1 : lastMapIndex;

            var firstMapIndex = Array.FindIndex(blis, 0, map => (ulong) map.SampleEnd >= sampleOffset);
            firstMapIndex = firstMapIndex == -1 ? 0 : firstMapIndex;
            var lastMapIndex =
                Array.FindIndex(blis, firstMapIndex, map => (ulong) map.SampleEnd >= sampleOffset + sampleCnt);
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

            var skippedDelta = (ulong) blis[firstMapIndex].SampleIndex - sampleOffset;

            //TODO: which one do we prefer? starting at user sample or block boundary?
            ulong realOffset = (ulong) blis[firstMapIndex].SampleIndex;


            var buffers = channels.Select(k => k.CreateBuffer(SampleCount)).ToArray();
            Buffers = buffers.Select(k => k.CreateView<Mdf4Channel>()).ToArray();
            BytesAllocated = SampleCount * 8 * buffers.Length;

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
#if MERGE_BLOCKS
            // ~50 ?
            int blocksPerThread = (int)((8.0 * blis[firstMapIndex].ByteLength / (channels.Length / 100.0)) / 50000);

            if (blocksPerThread > 100)
                blocksPerThread = 100;
            if (blocksPerThread < 1)
                blocksPerThread = 1;

            var indices = new List<int>();
            int next = firstMapIndex;
            while (next <= lastMapIndex)
            {
                indices.Add(next);
                next += blocksPerThread;
            }

            Parallel.ForEach(indices, ploop =>
                {
                    for (int i = ploop; i < (ploop + blocksPerThread); ++i)
                    {
                        if (i > lastMapIndex)
                            return;
#else // MERGE_BLOCKS
            Parallel.For(firstMapIndex, lastMapIndex + 1, i =>
                {
#endif // MERGE_BLOCKS


#else // PARALLEL
            for (var i = firstMapIndex; i <= lastMapIndex; ++i)
                {
#endif // PARALLEL

                    var bli = blis[i];
                    var blk = bli.Block;

                    // allocate 'a little bit more' as we always read 8 bytes
                    var recordBuffer = MdfBufferPool.Rent(blk.ByteLength + 8);
                    blk.CopyTo(recordBuffer, 0);
                    bli.CopyGaps(recordBuffer);

                    Interlocked.Add(ref BytesLoaded, blk.ByteLength);

                    //TODO: find better metric -.-
                    var threadMetric = bli.SampleCount * channels.Length;
                    var threadCount = (int) Math.Ceiling(threadMetric / 100000.0);
                    if (threadCount > 10)
                        threadCount = 10;

#if PARALLEL
                    //NORMAL VERSION
                    if (threadCount <= 1)
                    {
#endif
                        var byteOffset = (ulong) bli.Alignment.LeftByteOffset;
                        var sampleStart = (ulong) bli.SampleIndex - realOffset;
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
                        var chunks = Buffers.ChunkList(channels.Length / threadCount);
                        var byteOffset = bli.Alignment.LeftByteOffset;
                        var sampleStart = (ulong) bli.SampleIndex - realOffset;
                        var sampleCount = (uint) bli.SampleCount;

                        Parallel.ForEach(chunks, chunkedChannels =>
                        {
                            foreach (var bufferView in chunkedChannels)
                            {
                                var buffer = bufferView.Original;
                                buffer.Update(recordBuffer, (ulong) byteOffset, sampleStart, sampleCount);
                            }
                        });
                    }
#endif
                    MdfBufferPool.Return(recordBuffer);
                }
#if PARALLEL
#if MERGE_BLOCKS
        }
#endif
            );
#endif

            Parallel.For(firstMapIndex, lastMapIndex + 1, i => 
            // for (var i = firstMapIndex; i <= lastMapIndex; ++i)
            {
                var bli = blis[i];

                ulong gapSample = (ulong) (bli.SampleEnd);
                if (gapSample >= (sampleOffset + sampleCnt))
                    return;

                if (bli.RightGapBuffer != null)
                {
                    for (var cIndex = 0; cIndex < channels.Length; cIndex++)
                    {
                        var buffer = buffers[cIndex];

                        buffer.Update(bli.RightGapBuffer, 0, (ulong) (gapSample), 1);
                    }
                }
            }

            );
        }

        public static Mdf4Sampler[] CreateMany(IEnumerable<Mdf4Channel> channels, long sampleLimit = -1)
        {
            var byGroup = channels.GroupBy(k => k.ChannelGroup);

            var stuff = new ConcurrentBag<Mdf4Sampler>();
#if PARALLEL_GROUPS
            Parallel.ForEach(byGroup, grouping =>
#else
            foreach (var grouping in byGroup)
#endif
                {
                    var grp = grouping.Key;
                    var limit = sampleLimit == -1 ? grp.SampleCount : (ulong) sampleLimit;

                    var smp = CreateForSingleGroup(grouping, 0, limit);
                    stuff.Add(smp);
                }
#if PARALLEL_GROUPS
            );
#endif

            return stuff.ToArray();
        }

        /// <summary>
        /// This method allocates sample buffers for all given signals
        /// </summary>
        /// <remarks>
        /// All channels must belong to the same channel group. This is not enforced yet.
        /// You also should dispose the buffers to reclaim your precious memory.
        /// </remarks>
        /// <param name="channels">A set of channels</param>
        /// <param name="firstSample">The first sample (index)</param>
        /// <param name="sampleCnt">Number of samples to decode</param>
        /// <returns>A sampler object containing the buffers</returns>
        public static Mdf4Sampler CreateForSingleGroup(IEnumerable<Mdf4Channel> channels, ulong firstSample, ulong sampleCnt, int blockStride = 1)
        {
            return new Mdf4Sampler(channels, firstSample, sampleCnt, blockStride);
        }

        public void Dispose()
        {
            for (var index = 0; index < Buffers.Length; index++)
            {
                var bufferView = Buffers[index];
                bufferView.Dispose();
            }
        }
    }
}
