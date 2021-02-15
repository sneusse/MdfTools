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

        public RawDecoderSpec(uint stride, int byteOffset, int bitOffset, int bitLength, ByteOrder byteOrder,
                              DataType dataType)
        {
            Stride = stride;
            ByteOffset = byteOffset;
            BitOffset = bitOffset;
            BitLength = bitLength;
            ByteOrder = byteOrder;
            DataType = dataType;

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

            var samplesAreLittleEndian = ByteOrder == ByteOrder.Intel;
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

        public Type GetStorageType()
        {
            switch (DataType)
            {
            case DataType.Unsigned:
                if (BitLength <= 8) return typeof(byte);
                if (BitLength <= 16) return typeof(ushort);
                if (BitLength <= 32) return typeof(uint);
                if (BitLength <= 64) return typeof(ulong);

                break;
            case DataType.Signed:
                if (BitLength <= 8) return typeof(sbyte);
                if (BitLength <= 16) return typeof(short);
                if (BitLength <= 32) return typeof(int);
                if (BitLength <= 64) return typeof(long);

                break;
            case DataType.Float:
                if (BitLength <= 16) throw new NotImplementedException("Float16");
                if (BitLength <= 32) return typeof(float);
                if (BitLength <= 64) return typeof(double);
                break;
            case DataType.AnsiString:
                return typeof(string);
            case DataType.ByteArray:
                return typeof(byte[]);
            case DataType.Bool:
                return typeof(bool);
            }

            throw new NotImplementedException($"{DataType} has unknown storage");
        }
    }
}
