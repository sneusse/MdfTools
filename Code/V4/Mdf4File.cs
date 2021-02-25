#define ENABLE_PERF_MON

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MdfTools.Shared.Data;
using MdfTools.Shared.Data.Base;
using MdfTools.Shared.Data.Spec;
using MdfTools.Utils;

namespace MdfTools.V4
{
    public class Mdf4File : IDisposable
    {
        internal static PerfMetrics Metrics = new PerfMetrics();

        internal readonly List<Mdf4ChannelGroup> ChannelGroupsInternal = new List<Mdf4ChannelGroup>();

        internal readonly Mdf4Parser Mdf4Parser;

        internal readonly SampleBufferFactory SampleBufferFactory;

        public IReadOnlyList<Mdf4ChannelGroup> ChannelGroups => ChannelGroupsInternal;

        public IEnumerable<Mdf4Channel> Channels => ChannelGroups.SelectMany(k => k.Channels);

        public IEnumerable<IMdf4FileHistory> History => Header.FHBlocks;

        internal Mdf4HDBlock Header => Mdf4Parser.GetBlock<Mdf4HDBlock>(64);

        public string Filename { get; }

        public bool IsWritable { get; }

        public bool IsReadable { get; }

        internal readonly Dictionary<Mdf4CCBlock, (ValueConversionSpec, DisplayConversionSpec)> ConversionCache =
            new Dictionary<Mdf4CCBlock, (ValueConversionSpec, DisplayConversionSpec)>();

        internal Mdf4File(Mdf4Parser mdf4Parser, string filename)
        {
            Mdf4Parser = mdf4Parser;
            Filename = filename;
            IsWritable = false;
            IsReadable = true;
            SampleBufferFactory = new DefaultSampleBufferFactory();
        }

        public void Dispose()
        {
            Mdf4Parser?.Dispose();
        }

        public static Mdf4File Open(string filename)
        {
            return new Mdf4Parser(filename).Open();
        }

        // actually not needed anymore as the reader is thread safe now.
        public Mdf4File PrepareForMultiThreading()
        {
            // block map
            ChannelGroups.Select(k => k.BlockLoadingInfos).ToArray();
            // create decoders
            ChannelGroups.SelectMany(k => k.Channels).Select(k => k.DecoderSpec).ToArray();
            return this;
        }

        public void DumpInfo()
        {
            Console.WriteLine($"Groups   : {ChannelGroups.Count}");
            Console.WriteLine($"Channels : {ChannelGroups.SelectMany(k => k.Channels).Count()}");
        }

        public static void Bench(string filename, bool @short = false, long sampleLimit = -1)
        {
            var sw = Stopwatch.StartNew();

            Metrics = new PerfMetrics();
            var parser = new Mdf4Parser(filename);
            var mf4 = parser.Open().PrepareForMultiThreading();
            var buffers = Mdf4Sampler.LoadFull(mf4.ChannelGroups.SelectMany(k => k.Channels), sampleLimit);

            var elapsed = sw.Elapsed.TotalSeconds;


            var metrics = Metrics;
            if (@short)
            {
                Console.WriteLine($"{FormatUtils.GetBytesReadable(metrics.SampleReading.Value0, "samples")}" +
                                  $" - {FormatUtils.GetBytesReadable(metrics.SampleReading.Value1, "B")}" +
                                  $" - {FormatUtils.GetBytesReadable((long) (metrics.SampleReading.Value1 / elapsed))}ps" +
                                  $" - {elapsed:n2}s");
            }
            else
            {
                Console.WriteLine("-- File Info........");
                Console.WriteLine($"# Groups in file   : {mf4.ChannelGroups.Count}");
                Console.WriteLine($"# Groups loaded    : {buffers.Select(k => k.Channel.ChannelGroup).Distinct().Count()}");
                Console.WriteLine($"# Channels in file : {mf4.ChannelGroups.SelectMany(k => k.Channels).Count()}");
                Console.WriteLine($"# Channels loaded  : {buffers.Select(k => k.Channel).Distinct().Count()}");
                Console.WriteLine("-- Data.............");
                Console.WriteLine($"Raw-bytes loaded   : {FormatUtils.GetBytesReadable(metrics.CopyRawData.Value0)}");
                Console.WriteLine($"Zip-bytes loaded   : {FormatUtils.GetBytesReadable(metrics.ExtractAndTranspose.Value0)}");
                Console.WriteLine($"Samples loaded     : {FormatUtils.GetBytesReadable(metrics.SampleReading.Value0, "samples")}");
                Console.WriteLine($"Read speed         : {FormatUtils.GetBytesReadable((long) (metrics.SampleReading.Value1 / elapsed))}ps");
                Console.WriteLine($"Allocations        : {FormatUtils.GetBytesReadable(metrics.Allocations.Value0)}");
                Console.WriteLine("-- Times............");
                Console.WriteLine($"Full load time     : {elapsed:N1}s");
                Console.WriteLine($"Time opening       : {metrics.TimeOpening}");
                Console.WriteLine($"Block creation     : {metrics.BlockCreation}");
                Console.WriteLine($"BLI construction   : {metrics.BlockLoadingInfoConstruction}");
                Console.WriteLine($"Raw copies         : {metrics.CopyRawData}");
                Console.WriteLine($"Inflate/Transpose  : {metrics.ExtractAndTranspose}");
                Console.WriteLine($"SampleReading      : {metrics.SampleReading}");
                Console.WriteLine($"Allocations        : {metrics.Allocations}");
                Console.WriteLine("-- Parser stuff.....");
                parser.DumpInfo();
            }

            mf4.Dispose();

            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i].Dispose();
            }
        }

        internal class PerfMetrics
        {
            public Stopwatch Watch;

            public PerfMetrics()
            {
                Watch = Stopwatch.StartNew();
                Allocations = new PerfHelper.Metric(Watch);
                SampleReading = new PerfHelper.Metric(Watch);
                ExtractAndTranspose = new PerfHelper.Metric(Watch);
                CopyRawData = new PerfHelper.Metric(Watch);
                BlockLoadingInfoConstruction = new PerfHelper.Metric(Watch);
                BlockCreation = new PerfHelper.Metric(Watch);
                TimeOpening = new PerfHelper.Metric(Watch);
            }

            public PerfHelper.Metric TimeOpening { get; }
            public PerfHelper.Metric BlockCreation { get; }
            public PerfHelper.Metric BlockLoadingInfoConstruction { get; }
            public PerfHelper.Metric CopyRawData { get; }
            public PerfHelper.Metric ExtractAndTranspose { get; }
            public PerfHelper.Metric SampleReading { get; }
            public PerfHelper.Metric Allocations { get; }
        }

        internal class PerfHelper : IDisposable
        {
            private readonly Action _leaveAction;

            private PerfHelper(Action leaveAction)
            {
                _leaveAction = leaveAction;
            }

            public void Dispose()
            {
                _leaveAction();
            }

            public class Metric
            {
                private readonly Stopwatch _watch;
                internal long FirstCall = -1;
                internal long LastReturn = -1;
                internal long TotalCpuTime;
                internal long Value0 = 0;
                internal long Value1 = 0;

                public Metric(Stopwatch watch)
                {
                    _watch = watch;
                }

                public IDisposable Measure(long value0 = 0, long value1 = 0)
                {
                    Interlocked.Add(ref Value0, value0);
                    Interlocked.Add(ref Value1, value1);
                    var start = _watch.ElapsedMilliseconds;
                    if (FirstCall == -1) FirstCall = start;
                    return new PerfHelper(() =>
                    {
                        var stop = _watch.ElapsedMilliseconds;
                        Interlocked.Add(ref TotalCpuTime, stop-start);
                        if (LastReturn < stop) LastReturn = stop;
                    });
                }

                public override string ToString()
                {
                    return $"CPU:{TotalCpuTime}ms RT: {FirstCall}ms/{LastReturn}ms";
                }
            }
        }

        public override string ToString()
        {
            return Filename;
        }
    }
}
