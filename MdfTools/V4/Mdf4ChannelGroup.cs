#define PARALLEL_GAPS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MdfTools.V4.Helpers;

namespace MdfTools.V4
{
    public partial class Mdf4ChannelGroup
    {
        private readonly Mdf4CGBlock _cgBlock;
        private readonly List<Mdf4Channel> _channels = new List<Mdf4Channel>();
        private readonly Mdf4DGBlock _dgBlock;

        private BlockLoadingInfo[] _blockLoadingInfos;

        internal byte[] GapBuffer;
        internal int[] GapIndexToSampleIndex;

        public Mdf4File File { get; }

        public IReadOnlyList<Mdf4Channel> Channels => _channels;
        public Mdf4Channel MasterChannel { get; internal set; }
        public string Name => _cgBlock.AcquisitionName;
        public string Source => _cgBlock.AcquisitionSource?.SourceName ?? "<?>";
        public ulong SampleCount => _cgBlock.Data.CycleCount;
        public uint RecordLength { get; }
        public byte RecordIdSize => _dgBlock.RecordIdSize;
        internal BlockLoadingInfo[] BlockLoadingInfos => _blockLoadingInfos ??= CreateBlockLoadingInfo();

        internal Mdf4ChannelGroup(Mdf4File file, Mdf4CGBlock cgBlock, Mdf4DGBlock dgBlock)
        {
            File = file;
            _cgBlock = cgBlock;
            _dgBlock = dgBlock;
            RecordLength = _cgBlock.Data.DataBytes + _cgBlock.Data.InvalidBytes + RecordIdSize;
        }

        private BlockLoadingInfo[] CreateBlockLoadingInfo()
        {
            using var time = Mdf4File.Metrics.BlockLoadingInfoConstruction.Measure();

            var infos = _dgBlock
                        .DataRoot?
                        .GetBlockMap()?
                        .Select(k => new BlockLoadingInfo(k)).ToArray() ??
                        Array.Empty<BlockLoadingInfo>();

            if (infos.Length == 0)
                return infos;

            var indices = new List<int>();
            var gapBufferIndex = 0;
            foreach (var info in infos)
            {
                var blockByteLength = info.ByteLength;
                var blockByteStart = info.BytePosition;

                var leftAlignment = (RecordLength - blockByteStart % RecordLength) % RecordLength;
                var rightAlignment = (blockByteLength - leftAlignment) % RecordLength;

                ref var alignment = ref info.Alignment;
                alignment.LeftByteOffset = (int) leftAlignment;
                alignment.RightByteOffset = (int) rightAlignment;

                alignment.LeftGapIndex = gapBufferIndex;
                gapBufferIndex += alignment.LeftByteOffset;
                alignment.RightGapIndex = gapBufferIndex;
                gapBufferIndex += alignment.RightByteOffset;

                info.SampleCount = (blockByteLength - leftAlignment - rightAlignment) / RecordLength;
                info.SampleIndex = (blockByteStart + leftAlignment) / RecordLength;

                if (rightAlignment > 0) indices.Add((int) info.SampleEnd);
            }

            // that's the reason we did this...
            Debug.Assert(gapBufferIndex % RecordLength == 0);

            var gapRecords = (int) (gapBufferIndex / RecordLength);
            GapBuffer = new byte[gapBufferIndex]; // last index == length 
            GapIndexToSampleIndex = indices.ToArray();

            return infos;
        }

        public override string ToString()
        {
            return $"{Name}/{Source}";
        }

        internal void Add(Mdf4Channel channel)
        {
            _channels.Add(channel);
        }
    }
}
