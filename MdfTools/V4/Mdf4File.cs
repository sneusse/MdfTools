#define ENABLE_PERF_MON

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MdfTools.Shared.Data;
using MdfTools.Shared.Data.Base;
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

        internal Mdf4HDBlock Header => Mdf4Parser.GetBlock<Mdf4HDBlock>(64);

        public string Filename { get; }

        public bool IsWritable { get; }

        public bool IsReadable { get; }

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

        public static void Bench(string filename)
        {
            Metrics = new PerfMetrics();
            var sw = Stopwatch.StartNew();
            var parser = new Mdf4Parser(filename);
            var mf4 = parser.Open().PrepareForMultiThreading();
            var buffers = Mdf4Sampler.LoadFull(mf4.ChannelGroups.SelectMany(k => k.Channels));
            var elapsed = sw.Elapsed.TotalSeconds;

            Console.WriteLine("-- File Info........");
            Console.WriteLine($"# Groups in file   : {mf4.ChannelGroups.Count}");
            Console.WriteLine($"# Groups loaded    : {buffers.Select(k => k.Channel.ChannelGroup).Distinct().Count()}");
            Console.WriteLine($"# Channels in file : {mf4.ChannelGroups.SelectMany(k => k.Channels).Count()}");
            Console.WriteLine($"# Channels loaded  : {buffers.Select(k => k.Channel).Distinct().Count()}");
            Console.WriteLine("-- Data.............");
            Console.WriteLine($"Raw-bytes loaded   : {FormatUtils.GetBytesReadable(Metrics.CopyRawData.Value0)}");
            Console.WriteLine($"Zip-bytes loaded   : {FormatUtils.GetBytesReadable(Metrics.ExtractAndTranspose.Value0)}");
            Console.WriteLine($"Samples loaded     : {FormatUtils.GetBytesReadable(Metrics.SampleReading.Value0, "samples")}");
            Console.WriteLine($"Read speed         : {FormatUtils.GetBytesReadable((long) (Metrics.SampleReading.Value1 / elapsed))}ps");
            Console.WriteLine($"Allocations        : {FormatUtils.GetBytesReadable(Metrics.Allocations.Value0)}");
            Console.WriteLine("-- Times............");
            Console.WriteLine($"Full load time     : {elapsed:N1}s");
            Console.WriteLine($"Time opening       : {Metrics.TimeOpening}");
            Console.WriteLine($"Block creation     : {Metrics.BlockCreation}");
            Console.WriteLine($"BLI construction   : {Metrics.BlockLoadingInfoConstruction}");
            Console.WriteLine($"Raw copies         : {Metrics.CopyRawData}");
            Console.WriteLine($"Inflate/Transpose  : {Metrics.ExtractAndTranspose}");
            Console.WriteLine($"SampleReading      : {Metrics.SampleReading}");
            Console.WriteLine($"Allocations        : {Metrics.Allocations}");
            Console.WriteLine("-- Parser stuff.....");
            parser.DumpInfo();
        }

        internal class PerfMetrics
        {
            public PerfHelper.Metric TimeOpening { get; } = new PerfHelper.Metric();
            public PerfHelper.Metric BlockCreation { get; } = new PerfHelper.Metric();
            public PerfHelper.Metric BlockLoadingInfoConstruction { get; } = new PerfHelper.Metric();
            public PerfHelper.Metric CopyRawData { get; } = new PerfHelper.Metric();
            public PerfHelper.Metric ExtractAndTranspose { get; } = new PerfHelper.Metric();
            public PerfHelper.Metric SampleReading { get; } = new PerfHelper.Metric();
            public PerfHelper.Metric Allocations { get; } = new PerfHelper.Metric();
        }

        internal class PerfHelper : IDisposable
        {
            private static readonly Stopwatch Watch = Stopwatch.StartNew();
            private readonly Action<long, long> _leaveAction;
            private readonly long _start;

            private PerfHelper(Action<long, long> leaveAction)
            {
                _start = Watch.ElapsedMilliseconds;
                _leaveAction = leaveAction;
            }

            public void Dispose()
            {
                _leaveAction(Watch.ElapsedMilliseconds - _start, Watch.ElapsedMilliseconds);
            }

            public class Metric
            {
                internal long FirstCall = -1;
                internal long LastReturn = -1;
                internal long TotalCpuTime;
                internal long Value0;
                internal long Value1;

                public IDisposable Measure(long value0 = 0, long value1 = 0)
                {
                    if (FirstCall == -1) FirstCall = Watch.ElapsedMilliseconds;
                    return new PerfHelper((elapsed, total) =>
                    {
                        Interlocked.Add(ref TotalCpuTime, elapsed);
                        Interlocked.Add(ref Value0, value0);
                        Interlocked.Add(ref Value1, value1);

                        if (LastReturn < total) LastReturn = total;
                    });
                }

                public override string ToString()
                {
                    return $"CPU:{TotalCpuTime}ms RT: {FirstCall}ms-{LastReturn}ms";
                }
            }
        }
    }
}
