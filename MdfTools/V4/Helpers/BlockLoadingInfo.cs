using System.Runtime.CompilerServices;

namespace MdfTools.V4.Helpers
{
    internal class BlockLoadingInfo
    {
        internal AlignmentInfo Alignment;
        internal long SampleCount;

        internal long SampleIndex;

        internal Mdf4DataBlock Block { get; }
        public long BytePosition { get; }
        public long ByteLength => Block.ByteLength;
        internal long SampleEnd => SampleIndex + SampleCount;

        internal BlockLoadingInfo(DataBlockMap map)
        {
            Block = map.Block;
            BytePosition = map.RawRecordOffset;
        }

        internal void CopyGaps(byte[] recordBuffer, byte[] gapBuffer)
        {
            if (Alignment.LeftByteOffset > 0)
                Unsafe.CopyBlock(ref gapBuffer[Alignment.LeftGapIndex], ref recordBuffer[0],
                                 (uint) Alignment.LeftByteOffset);

            if (Alignment.RightByteOffset > 0)
                Unsafe.CopyBlock(ref gapBuffer[Alignment.RightGapIndex],
                                 ref recordBuffer[ByteLength - Alignment.RightByteOffset],
                                 (uint) Alignment.RightByteOffset);
        }
    }
}
