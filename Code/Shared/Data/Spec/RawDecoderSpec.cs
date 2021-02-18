using System;

namespace MdfTools.Shared.Data.Spec
{
    public sealed class RawDecoderSpec
    {
        public uint Stride { get; }

        /// <summary>
        ///     Byte offset not including bit offset
        /// </summary>
        public int ByteOffset { get; }

        public int BitOffset { get; }
        public int BitLength { get; }

        /// <summary>
        ///     Byte offset including bit offset
        /// </summary>
        public int TotalByteOffset { get; }

        /// <summary>
        ///     Total number of bytes needed during extraction
        /// </summary>
        public int TotalByteLength { get; }

        public int Shift { get; }
        public ulong Mask { get; }

        public bool IsSameEndianess { get; }
        public bool IsFullWidth { get; }
        public bool IsByteAligned { get; }

        public ByteOrder ByteOrder { get; }
        public DataType DataType { get; }
        public bool IsNumeric { get; }

        public NativeType NativeType { get; }

        public RawDecoderSpec(uint stride, int byteOffset, int bitOffset, int bitLength, ByteOrder byteOrder,
                              DataType dataType)
        {
            Stride = stride;
            ByteOffset = byteOffset;
            BitOffset = bitOffset;
            BitLength = bitLength;
            ByteOrder = byteOrder;
            DataType = dataType;
            NativeType = GetNativeType();

            TotalByteOffset = byteOffset + bitOffset / 8;
            Shift = bitOffset % 8;
            TotalByteLength = (int) Math.Ceiling((bitLength + Shift) / 8.0);

            unchecked
            {
                if (bitLength != 64)
                    Mask = (1UL << bitLength) - 1;

                else
                    Mask = ulong.MaxValue;
            }

            var samplesAreLittleEndian = ByteOrder == ByteOrder.LittleEndian;
            IsSameEndianess = BitConverter.IsLittleEndian == samplesAreLittleEndian;

            if (DataType == DataType.Float && (bitLength == 32 || bitLength == 64))
            {
                IsNumeric = true;
                IsFullWidth = true;
                IsByteAligned = true;
            }
            else if (DataType == DataType.Signed || DataType == DataType.Unsigned)
            {
                IsNumeric = true;
                IsByteAligned = Shift == 0;
                IsFullWidth = IsByteAligned &&
                              (bitLength == 8 || bitLength == 16 || bitLength == 32 || bitLength == 64);
            }
            else
            {
                IsNumeric = false;
            }
        }


        private NativeType GetNativeType()
        {
            switch (DataType)
            {
            case DataType.Unsigned:
                if (BitLength <= 8) return NativeType.UInt8;
                if (BitLength <= 16) return NativeType.UInt16;
                if (BitLength <= 32) return NativeType.UInt32;
                if (BitLength <= 64) return NativeType.UInt64;
                break;
            case DataType.Signed:
                if (BitLength <= 8) return NativeType.Int8;
                if (BitLength <= 16) return NativeType.Int16;
                if (BitLength <= 32) return NativeType.Int32;
                if (BitLength <= 64) return NativeType.Int64;
                break;
            case DataType.Float:
                if (BitLength <= 16) throw new NotImplementedException("Float16");
                if (BitLength <= 32) return NativeType.Float;
                if (BitLength <= 64) return NativeType.Double;
                break;
            }

            return NativeType.NotNative;
        }
    }
}
