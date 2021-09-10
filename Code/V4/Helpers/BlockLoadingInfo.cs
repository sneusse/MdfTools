using System.Runtime.CompilerServices;

namespace MdfTools.V4.Helpers
{
    internal class BlockLoadingInfo
    {
        internal AlignmentInfo Alignment;
        internal long SampleCount;

        internal long SampleIndex;
        internal byte[] LeftGapBuffer;
        internal byte[] RightGapBuffer;

        internal Mdf4DataBlock Block { get; }
        public long BytePosition { get; }
        public long ByteLength => Block.ByteLength;
        internal long SampleEnd => SampleIndex + SampleCount;

        internal BlockLoadingInfo(DataBlockMap map)
        {
            Block = map.Block;
            BytePosition = map.RawRecordOffset;
        }

        internal void CopyGaps(byte[] recordBuffer)
        {
            if (Alignment.LeftByteOffset > 0)
                Unsafe.CopyBlock(ref LeftGapBuffer[LeftGapBuffer.Length - Alignment.LeftByteOffset],
                    ref recordBuffer[0],
                    (uint) Alignment.LeftByteOffset);

            if (Alignment.RightByteOffset > 0)
                Unsafe.CopyBlock(ref RightGapBuffer[0],
                    ref recordBuffer[ByteLength - Alignment.RightByteOffset],
                    (uint) Alignment.RightByteOffset);
        }
    }
}
