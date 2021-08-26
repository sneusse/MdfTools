using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using MdfTools.Native;
using MdfTools.Shared;
using MdfTools.Utils;

// Block raw structs are parsed and never instantiated manually
#pragma warning disable CS0649

namespace MdfTools.V4
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct IDBlockRaw
    {
        public fixed byte IdFile[8];
        public fixed byte IdVers[8];
        public fixed byte IdProg[8];
        public fixed byte IdReserved1[4];
        public ushort IdVer;
        public fixed byte IdReserved2[4];
        public ushort IdUnfinFlags;

        public string IdFileStr
        {
            get
            {
                fixed (byte* p = IdFile)
                {
                    return new string((sbyte*) p, 0, 8);
                }
            }
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct BlockHeader
    {
        public ushort IdHash;
        public BlockId Id;
        public uint Reserved;
        public long Length;
        public long LinkCount;
    }

    internal enum ZipType : byte
    {
        Deflate,
        TranspositionDeflate
    }

    [Flags]
    public enum ListFlags : ushort
    {
        EqualLength = 1 << 0,
        TimeValues = 1 << 1,
        AngleValues = 1 << 2,
        DistanceValues = 1 << 3
    }

    [Flags]
    public enum TimeFlags : byte
    {
        LocalTime = 1 << 0,
        OffsetsValid = 1 << 1
    }

    internal readonly struct DataBlockMap
    {
        public readonly long Link;
        public readonly long RawRecordOffset;
        private readonly Mdf4Parser _parser;

        public DataBlockMap(long link, long rawRecordOffset, Mdf4Parser parser)
        {
            Link = link;
            RawRecordOffset = rawRecordOffset;
            _parser = parser;
        }

        public Mdf4DataBlock Block => _parser.GetBlock<Mdf4DataBlock>(Link);
    }

    internal abstract class Mdf4DataBlock : Mdf4Block
    {
        public abstract long ByteLength { get; }
        public abstract Span<byte> GetBuffer();
        public abstract int CopyTo(byte[] fullBuffer, int offset);
    }


    internal interface IMdf4DataRoot
    {
        public Mdf4DataBlock this[int index] { get; }
        public uint BlockCount { get; }
        public IEnumerable<Mdf4DataBlock> GetAllDataBlocks();
        public IEnumerable<DataBlockMap> GetBlockMap();
    }

    internal class Mdf4DTBlock : Mdf4DataBlock, IMdf4DataRoot
    {
        public override long ByteLength => BlockDataLength;

        public IEnumerable<Mdf4DataBlock> GetAllDataBlocks()
        {
            yield return this;
        }

        public IEnumerable<DataBlockMap> GetBlockMap()
        {
            yield return new DataBlockMap(Offset, 0, Parser);
        }

        public Mdf4DataBlock this[int index]
        {
            get
            {
                if (index != 1)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return this;
            }
        }

        public uint BlockCount => 1;

        public override unsafe Span<byte> GetBuffer()
        {
            return new Span<byte>(Reader.GetRawPointer(BlockDataOffset).ToPointer(), (int) BlockDataLength);
        }

        public override int CopyTo(byte[] fullBuffer, int offset)
        {
            using var time = Mdf4File.Metrics.CopyRawData.Measure(BlockDataLength, BlockDataLength);

            // this is painfully slow.
            // Reader.ReadArray(BlockDataOffset, ref fullBuffer, offset, (int) BlockDataLength);

            Reader.BlockCopy(BlockDataOffset, ref fullBuffer, offset, (int) BlockDataLength);
            return (int) BlockDataLength;
        }
    }

    internal class Mdf4DZBlock : Mdf4DataBlock, IMdf4DataRoot
    {
        public enum ZLibCompressionInfo : byte
        {
            FastCompression = 0x01,
            DefaultCompression = 0x9C,
            BestCompression = 0xDA
        }

        private const byte ZLibHeader = 0x78;

        public Raw Data;
        public long ZippedDataOffset;

        public override long ByteLength => (long) Data.UncompressedDataLength;

        protected override void InitDataSection()
        {
            Read(out Data);
            ZippedDataOffset = Reader.Position;

            // we guessed wronk. no zlib header present? TODO: removeme when we get rid of the managed inflate algorithm.
            if (ZLibHeader != Data.ZLibHeader) ZippedDataOffset -= 2;
        }

        public override Span<byte> GetBuffer()
        {
            throw new NotImplementedException();
        }

        public override int CopyTo(byte[] fullBuffer, int offset)
        {
            using var time = Mdf4File.Metrics.CopyRawData.Measure((long)Data.CompressedDataLength, (long)Data.UncompressedDataLength);

            if (Data.ZipType == ZipType.TranspositionDeflate)
            {
                var rows = Data.ZipParameter;
                var columns = (int) (Data.UncompressedDataLength / Data.ZipParameter);

                var transposedData = MdfBufferPool.Rent(Data.UncompressedDataLength);
                var compressedData = Reader.GetRawPointer(ZippedDataOffset);

                // unmanaged version is ~ factor 2 faster
                LibDeflateDecompress.Decompress(compressedData, Data.CompressedDataLength, transposedData,
                    Data.UncompressedDataLength);

                // overhead for basically everything I have right now is bigger than the speedup.
                // maybe if we have some files with 2/4/8MB blocks? Test when we have such a file.

                using var transposetime = Mdf4File.Metrics.ExtractAndTranspose.Measure((long)Data.UncompressedDataLength, (long)Data.CompressedDataLength);

                if (fullBuffer.Length > 20 * 1024 * 1024)
                {
                    unsafe
                    {
                        const int block = 32 * 8;
                        fixed (byte* bufferStart = fullBuffer)
                        fixed (byte* fsrc = transposedData)
                        {
                            var dst = bufferStart + offset;
                            var src = fsrc;

                            Parallel.For(0, rows / block, l =>
                            {
                                int i = (int) l * block;
                                for (var j = 0; j < columns; ++j)
                                {
                                    for (var b = 0; b < block && i + b < rows; ++b)
                                    {
                                        dst[j * rows + i + b] = src[(i + b) * columns + j];
                                    }
                                }
                            });
                        }
                    }
                }
                else
                {
                    unsafe
                    {
                        // fixed (byte* bufferStart = fullBuffer)
                        // {
                        //     var b = bufferStart + offset; 
                        //     for (var c = 0; c < columns; c++)
                        //     for (var r = 0; r < rows; r++)
                        //     {
                        //         var transposedIndex = columns * r + c;
                        //         *b++ = transposedData[transposedIndex];
                        //     }
                        // }


                        const int block = 32 * 8;
                        fixed (byte* bufferStart = fullBuffer)
                        fixed (byte* src = transposedData)
                        {
                            var dst = bufferStart + offset;
                            for (var i = 0; i < rows; i += block)
                            {
                                for (var j = 0; j < columns; ++j)
                                {
                                    for (var b = 0; b < block && i + b < rows; ++b)
                                    {
                                        dst[j * rows + i + b] = src[(i + b) * columns + j];
                                    }
                                }
                            }
                        }
                    }
                }


                // remaining untransposed stuff:
                var transposedBytes = columns * rows;
                var rem = (uint) (Data.UncompressedDataLength - (ulong) transposedBytes);
                if (rem > 0)
                {
                    var remStart = offset + transposedBytes;
                    Unsafe.CopyBlock(ref fullBuffer[remStart], ref transposedData[transposedBytes], rem);
                }

                MdfBufferPool.Return(transposedData);
            }
            else
            {
                var compressedData = Reader.GetRawPointer(ZippedDataOffset);
                var destBuffer = fullBuffer.AsSpan(offset);
                LibDeflateDecompress.Decompress(compressedData, Data.CompressedDataLength, destBuffer,
                    Data.UncompressedDataLength);
            }

            #region Managed version

            //  Managed impl. 
            /*
            using var compressed = Reader.CreateStream(ZippedDataOffset, (long) Data.CompressedDataLength);
            using var decompressStream = new DeflateStream(compressed, CompressionMode.Decompress);

            if (Data.ZipType == ZipType.TranspositionDeflate)
            {
                var rows = Data.ZipParameter;
                var columns = (int) (Data.UncompressedDataLength / Data.ZipParameter);
                var bytesToProcess = columns * rows;

                // length of one row (uncompressed transposed)
                var transposedData = ArrayPool<byte>.Shared.Rent((int) bytesToProcess);
                decompressStream.Read(transposedData, 0, (int) bytesToProcess);
                for (var r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        var transposedIndex = (int) rows * c +  r;
                        buffer[transposedIndex] = transposedData[c];
                    }
                }
                ArrayPool<byte>.Shared.Return(transposedData);
            }
            else
            {
                throw new NotImplementedException();
            }

            */

            #endregion

            return (int) Data.UncompressedDataLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Raw
        {
            public BlockId OrgBlockId;
            public ZipType ZipType;
            private readonly byte _reserved;
            public uint ZipParameter;
            public ulong UncompressedDataLength;
            public ulong CompressedDataLength;
            public byte ZLibHeader;
            public ZLibCompressionInfo CompressionInfo;
        }

        public Mdf4DataBlock this[int index] => this;

        public uint BlockCount => 1;

        public IEnumerable<Mdf4DataBlock> GetAllDataBlocks()
        {
            yield return this;
        }

        public IEnumerable<DataBlockMap> GetBlockMap() => new DataBlockMap[1]
        {
            new DataBlockMap(Offset, 0, Parser)
        };
    }

    internal class Mdf4DLBlock : Mdf4Block, IMdf4DataRoot
    {
        private uint _blockCount;
        public long[] BlockOffsets;
        public ulong EqualLength;

        public ListFlags Flags;
        public IEnumerable<Mdf4DLBlock> DlBlocksWithSelf => GetIterator(this);
        public IEnumerable<Mdf4DataBlock> DataBlocks => Links.Skip(1).Select(k => Parser.GetBlock<Mdf4DataBlock>(k));

        public UnknownArray Time { get; private set; }
        public UnknownArray Angle { get; private set; }
        public UnknownArray Distance { get; private set; }

        public IEnumerable<Mdf4DataBlock> GetAllDataBlocks()
        {
            foreach (var mdfDlBlock in DlBlocksWithSelf)
            foreach (var dataBlock in mdfDlBlock.DataBlocks)
                yield return dataBlock;
        }

        public IEnumerable<DataBlockMap> GetBlockMap()
        {
            foreach (var mdfDlBlock in DlBlocksWithSelf)
                if (mdfDlBlock.EqualLength > 0)
                    for (uint i = 0; i < mdfDlBlock.BlockCount; i++)
                        yield return new DataBlockMap(mdfDlBlock.Links[i + 1], (long) (i * mdfDlBlock.EqualLength),
                            Parser);
                else
                    for (uint i = 0; i < mdfDlBlock.BlockCount; i++)
                        yield return new DataBlockMap(mdfDlBlock.Links[i + 1], mdfDlBlock.BlockOffsets[i], Parser);
        }

        public Mdf4DataBlock this[int index] => LinkTo<Mdf4DataBlock>(Links[index + 1]);
        public uint BlockCount => _blockCount;

        protected override void InitDataSection()
        {
            // why we cannot have the same byte length?
            Read(out byte fuMdf);
            Flags = (ListFlags) fuMdf;
            Reader.Seek(3, SeekOrigin.Current);
            Read(out _blockCount);

            BlockOffsets = new long[BlockCount];
            Time = new UnknownArray(BlockOffsets.Length);
            Angle = new UnknownArray(BlockOffsets.Length);
            Distance = new UnknownArray(BlockOffsets.Length);

            if (Flags.HasFlag(ListFlags.EqualLength))
                Read(out EqualLength);
            else
                Reader.ReadArray(ref BlockOffsets);

            if (Flags.HasFlag(ListFlags.TimeValues))
                Reader.ReadArray(ref Time.Storage);
            if (Flags.HasFlag(ListFlags.AngleValues))
                Reader.ReadArray(ref Angle.Storage);
            if (Flags.HasFlag(ListFlags.DistanceValues))
                Reader.ReadArray(ref Distance.Storage);
        }
    }

    internal class Mdf4HLBlock : Mdf4Block, IMdf4DataRoot
    {
        public ListFlags Flags;
        public ZipType ZipType;
        public Mdf4DLBlock FirstDlBlock => LinkTo<Mdf4DLBlock>(0);

        public IEnumerable<Mdf4DataBlock> GetAllDataBlocks()
        {
            return FirstDlBlock.GetAllDataBlocks();
        }

        public IEnumerable<DataBlockMap> GetBlockMap()
        {
            return FirstDlBlock.GetBlockMap();
        }

        public Mdf4DataBlock this[int index] => FirstDlBlock[index];

        public uint BlockCount => FirstDlBlock.BlockCount;

        protected override void InitDataSection()
        {
            Read(out Flags);
            Read(out ZipType);
        }
    }

    internal class Mdf4MDBlock : Mdf4TXBlock
    {
        protected override void InitDataSection()
        {
            Value = Reader.ReadUTF8(BlockDataLength).TrimEnd('\0');
        }

        public static implicit operator string(Mdf4MDBlock self)
        {
            return self.Value;
        }

        public static implicit operator XElement(Mdf4MDBlock self)
        {
            return XElement.Parse(self.Value);
        }
    }

    internal class Mdf4TXBlock : Mdf4Block
    {
        public string Value;

        protected override void InitDataSection()
        {
            Value = Reader.ReadAscii(BlockDataLength).TrimEnd('\0');
        }

        public static implicit operator string(Mdf4TXBlock txBlock)
        {
            return txBlock?.Value;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    internal class Mdf4CCBlock : Mdf4Block<Mdf4CCBlock.Raw>
    {
        [Flags]
        public enum ConversionFlags : ushort
        {
            PrecisionValid = 1 << 0,
            PhysicalValueRangeValid = 1 << 1,
            StatusString = 1 << 2
        }

        public enum ConversionType : byte
        {
            Identity,
            Linear,
            Rational,
            AlgebraicText,
            ValToValInterp,
            ValToValNoInterp,
            ValRangeToValTab,
            ValToTextScaleTab,
            ValRangeToTextScaleTab,
            TextToVal,
            TextToText,
            BitfieldText
        }

        private static readonly int ParamOffset = Unsafe.SizeOf<Raw>();

        internal double[] Params;

        public Mdf4TXBlock Name => LinkTo<Mdf4TXBlock>(0);
        public Mdf4TXBlock Unit => LinkTo<Mdf4TXBlock>(1);
        public Mdf4TXBlock Comment => LinkTo<Mdf4TXBlock>(2);

        internal Mdf4Block Ref(int refIndex)
        {
            return Parser.GetBlock<Mdf4Block>(Links[refIndex + 4]);
        }

        protected override void InitDataSection()
        {
            base.InitDataSection();

            var paramCount = (BlockDataLength - ParamOffset) / 8;
            if (paramCount > 0) Params = Reader.ReadArray<double>(BlockDataOffset + ParamOffset, (int) paramCount);
        }

        public struct Raw
        {
            public ConversionType ConversionType;
            public byte Precision;
            public ConversionFlags Flags;
            internal ushort RefCount;
            internal ushort ValCount;
            public double PhysRangeMin;
            public double PhysRangeMax;
        }
    }

    internal class Mdf4CNBlock : Mdf4Block<Mdf4CNBlock.Raw>
    {
        public enum ChannelDataType : byte
        {
            UnsignedLittleEndian,
            UnsignedBigEndian,
            SignedLittleEndian,
            SignedBigEndian,
            FloatLittleEndian,
            FloatBigEndian,
            AnsiString,
            Utf8String,
            Utf16LeString,
            Utf18BeString,
            ByteArray,
            MIMESample,
            MIMEStream,
            CANopenDate,
            CANopenTime,
            ComplexLe,
            ComplexBe
        }

        [Flags]
        public enum ChannelFlags : uint
        {
            AllValuesInvalid = 1 << 0,
            InvalidationBitValid = 1 << 1,
            PrecisionValid = 1 << 2,
            ValueRangeValid = 1 << 3,
            LimitRangeValid = 1 << 4,
            ExtendedLimitRangeValid = 1 << 5,
            DiscreteValue = 1 << 6,
            Calibration = 1 << 7,
            Calculated = 1 << 8,
            Virtual = 1 << 9,
            BusEvent = 1 << 10,
            StrictlyMonotonous = 1 << 11,
            DefaultXAxis = 1 << 12,
            EventSignal = 1 << 13,
            VariableLengthStream = 1 << 14
        }

        public enum ChannelType : byte
        {
            FixedLength,
            VariableLength,
            Master,
            VirtualMaster,
            Sync,
            MaxLength,
            Virtual
        }

        public enum SyncType : byte
        {
            None,
            Time,
            Angle,
            Distance,
            Index
        }

        // private const int cn_composition = 1;
        private const int cn_tx_name = 2;
        private const int cn_si_source = 3;

        private const int cn_cc_conversion = 4;

        // private const int cn_data = 5;
        private const int cn_md_unit = 6;

        private const int cn_md_comment = 7;
        // private const int cn_at_reference_start = 8;
        // private const int cn_default_x = ?;

        public Mdf4TXBlock Name => LinkTo<Mdf4TXBlock>(cn_tx_name);
        public Mdf4SIBlock Source => LinkTo<Mdf4SIBlock>(cn_si_source);
        public Mdf4CCBlock Conversion => LinkTo<Mdf4CCBlock>(cn_cc_conversion);
        public Mdf4TXBlock Unit => LinkTo<Mdf4TXBlock>(cn_md_unit);
        public Mdf4TXBlock Comment => LinkTo<Mdf4TXBlock>(cn_md_comment);


        [StructLayout(LayoutKind.Sequential)]
        public struct Raw
        {
            public ChannelType ChannelType;
            public SyncType SyncType;
            public ChannelDataType DataType;
            public byte BitOffset;
            public uint ByteOffset;
            public uint BitLength;
            public ChannelFlags Flags;
            public uint InvalidBitPos;
            public byte Precision;
            public byte _reserved;
            public ushort AttachmentCount;
            public double ValRangeMine;
            public double ValRangeMax;
            public double LimitMin;
            public double LimitMax;
            public double LimitMinExt;
            public double LimitMaxExt;
        }
    }

    internal class Mdf4SIBlock : Mdf4Block<Mdf4SIBlock.Raw>
    {
        public enum BusType : byte
        {
            None,
            Other,
            CAN,
            LIN,
            MOST,
            Flexray,
            KLine,
            Ethernet,
            USB
        }

        [Flags]
        public enum SiFlags : byte
        {
            Simulated = 1
        }

        public enum SourceType : byte
        {
            Other,
            Ecu,
            Bus,
            IO,
            Tool,
            User
        }

        public Mdf4TXBlock SourceName => LinkTo<Mdf4TXBlock>(0);
        public Mdf4TXBlock SourcePath => LinkTo<Mdf4TXBlock>(1);
        public Mdf4TXBlock SourceComment => LinkTo<Mdf4TXBlock>(2);

        [StructLayout(LayoutKind.Sequential)]
        public struct Raw
        {
            public SourceType SourceType;
            public BusType BusType;
            public SiFlags Flags;
        }
    }

    internal class Mdf4CGBlock : Mdf4Block<Mdf4CGBlock.Raw>
    {
        [Flags]
        public enum CGFlags : ushort
        {
            VlsdChannelGroup = 1 << 0,
            BusEventChannelGroup = 1 << 1,
            PlainBusEventChannelGroup = 1 << 2,
            RemoteMaster = 1 << 3,
            EventSignalGroup = 1 << 4
        }

        private const int cg_cn_first = 1;
        private const int cg_tx_acq_name = 2;

        private const int cg_tx_acq_source = 3;

        // private const int cg_sr_first = 4;
        private const int cg_md_comment = 5;
        private const int cg_cg_master = 6;

        internal Mdf4CGBlock RemoteMaster;

        internal IEnumerable<Mdf4CNBlock> CNBlocks => GetIterator<Mdf4CNBlock>(cg_cn_first);

        internal Mdf4TXBlock AcquisitionName => LinkTo<Mdf4TXBlock>(cg_tx_acq_name);
        internal Mdf4SIBlock AcquisitionSource => LinkTo<Mdf4SIBlock>(cg_tx_acq_source);
        internal Mdf4TXBlock Comment => LinkTo<Mdf4TXBlock>(cg_md_comment);


        protected override void InitDataSection()
        {
            base.InitDataSection();

            if (Data.Flags.HasFlag(CGFlags.RemoteMaster))
                RemoteMaster = LinkTo<Mdf4CGBlock>(cg_cg_master);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Raw
        {
            public ulong RecordId;
            public ulong CycleCount;
            public CGFlags Flags;
            public char PathSeparator;
            private readonly uint _reserved;
            public uint DataBytes;
            public uint InvalidBytes;
        }
    }

    internal class Mdf4DGBlock : Mdf4Block
    {
        public byte RecordIdSize;
        internal IEnumerable<Mdf4CGBlock> CGBlocks => GetIterator<Mdf4CGBlock>(1);
        internal IMdf4DataRoot DataRoot => (IMdf4DataRoot) LinkTo<Mdf4Block>(2);
        internal Mdf4TXBlock Comment => LinkTo<Mdf4TXBlock>(3);

        protected override void InitDataSection()
        {
            Read(out RecordIdSize);
        }
    }

    public interface IMdf4FileHistory
    {
        string Comment { get; }
        Mdf4FHBlockData Data { get; }
        DateTime FileTime { get; }
        XElement XComment { get; }
    }

    public struct Mdf4FHBlockData
    {
        public ulong FileTimeNs;
        public ushort TimeZoneOffsetMinutes;
        public ushort DstOffsetMinutes;

        public TimeFlags Flags;
        // 3 reserved bytes, who cares.
    }

    internal class Mdf4FHBlock : Mdf4Block<Mdf4FHBlockData>, IMdf4FileHistory
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal Mdf4TXBlock Comment => LinkTo<Mdf4TXBlock>(1);
        Mdf4FHBlockData IMdf4FileHistory.Data => Data;
        string IMdf4FileHistory.Comment => Comment;
        public XElement XComment => XElement.Parse(Comment);

        public DateTime FileTime
        {
            get
            {
                var date = Epoch.AddMilliseconds(Data.FileTimeNs / 1000000.0);


                if (Data.Flags.HasFlag(TimeFlags.OffsetsValid))
                {
                    date.AddMinutes(Data.DstOffsetMinutes).AddMinutes(Data.TimeZoneOffsetMinutes);
                }

                return date;
            }
        }

        public override string ToString()
        {
            return $"{((IMdf4FileHistory) this).Comment}";
        }
    }

    internal class Mdf4HDBlock : Mdf4Block<Mdf4HDBlock.Raw>
    {
        [Flags]
        public enum HeaderFlags : byte
        {
            StartAngleValid,
            StartDistanceValid
        }

        public enum TimeClass : byte
        {
            LocalReference,
            ExternalSource = 10,
            ExternalSyncSource = 16
        }

        [Flags]
        public enum TimeFlags : byte
        {
            LocalTime,
            OffsetsValid
        }

        internal IEnumerable<Mdf4DGBlock> DGBlocks => GetIterator<Mdf4DGBlock>(0);
        internal IEnumerable<Mdf4FHBlock> FHBlocks => GetIterator<Mdf4FHBlock>(1);

        [StructLayout(LayoutKind.Sequential)]
        public struct Raw
        {
            public ulong StartTimeNs;
            public short TimeZoneOffsetMinutes;
            public short DstOffsetMinutes;
            public TimeFlags TimeFlags;
            public TimeClass Class;
            public HeaderFlags HeaderFlags;
            private readonly byte _reserved;
            public double StartAngle;
            public double StartDistance;
        }
    }

    internal class Mdf4Block
    {
        protected const int Next = 0;
        internal long BlockDataLength;
        internal long BlockDataOffset;
        internal long[] Links;

        internal long Offset;

        protected Mdf4Parser Parser { get; private set; }
        protected FastBinaryReader Reader { get; private set; }


        protected Mdf4Block()
        {
        }

        public static Mdf4Block Create(Mdf4Parser parser, long offset, IDictionary<long, Mdf4Block> cache)
        {
            if (offset == 0)
                return null;

            var reader = parser.Reader;

            ref var section = ref reader.GetRaw<BlockHeader>(offset);
            Mdf4Block block;
            switch (section.Id)
            {
            case BlockId.MdfBlockAT:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockCA:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockCC:
                block = new Mdf4CCBlock();
                break;
            case BlockId.MdfBlockCG:
                block = new Mdf4CGBlock();
                break;
            case BlockId.MdfBlockCH:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockCN:
                block = new Mdf4CNBlock();
                break;
            case BlockId.MdfBlockDG:
                block = new Mdf4DGBlock();
                break;
            case BlockId.MdfBlockDI:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockDL:
                block = new Mdf4DLBlock();
                break;
            case BlockId.MdfBlockDT:
                block = new Mdf4DTBlock();
                break;
            case BlockId.MdfBlockDV:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockDZ:
                block = new Mdf4DZBlock();
                break;
            case BlockId.MdfBlockEV:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockFH:
                block = new Mdf4FHBlock();
                break;
            case BlockId.MdfBlockHD:
                block = new Mdf4HDBlock();
                break;
            case BlockId.MdfBlockHL:
                block = new Mdf4HLBlock();
                break;
            case BlockId.MdfBlockLD:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockMD:
                block = new Mdf4MDBlock();
                break;
            case BlockId.MdfBlockRD:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockRI:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockRV:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockSD:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockSI:
                block = new Mdf4SIBlock();
                break;
            case BlockId.MdfBlockSR:
                block = new Mdf4Block();
                break;
            case BlockId.MdfBlockTX:
                block = new Mdf4TXBlock();
                break;
            default:
                throw new ArgumentOutOfRangeException();
            }

            const int headerSize = 24;

            var linkOffset = offset +    // file offset
                             headerSize; // sizeof(header)

            var linkSize = 8 * section.LinkCount; // sizeof(long) * links
            var links = reader.ReadArray<long>(linkOffset, (int) section.LinkCount);

            block.Offset = offset;
            block.Reader = reader;
            block.Parser = parser;
            block.Links = links;
            block.BlockDataOffset = linkOffset + linkSize;
            block.BlockDataLength = section.Length - linkSize - headerSize;

            // add to cache before init :S 
            cache[offset] = block;

            block.Reader.Seek(block.BlockDataOffset);
            block.InitDataSection();

            return block;
        }

        /// <summary>
        ///     early phase, only load what's needed to get:
        ///     * file info
        ///     * signal selection
        /// </summary>
        protected virtual void InitDataSection()
        {
        }

        protected void Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
        {
            Reader.Seek(offset, origin);
        }

        protected void Read<T>(out T val) where T : struct
        {
            Reader.Read(out val);
        }

        protected T LinkTo<T>(int linkIndex) where T : Mdf4Block
        {
            return Parser.GetBlock<T>(Links[linkIndex]);
        }

        protected T LinkTo<T>(long offset) where T : Mdf4Block
        {
            return Parser.GetBlock<T>(offset);
        }

        protected IEnumerable<T> GetIterator<T>(int firstLinkIndex) where T : Mdf4Block
        {
            return new BlockIterator<T>(Links[firstLinkIndex], Parser);
        }

        protected IEnumerable<T> GetIterator<T>(T first) where T : Mdf4Block
        {
            return new BlockIterator<T>(first);
        }

        public override int GetHashCode()
        {
            return Offset.GetHashCode();
        }

        private class BlockIterator<T> : IEnumerable<T> where T : Mdf4Block
        {
            private readonly T _first;

            public BlockIterator(T first)
            {
                _first = first;
            }

            public BlockIterator(long first, Mdf4Parser reader)
            {
                _first = reader.GetBlock<T>(first);
            }

            public IEnumerator<T> GetEnumerator()
            {
                var next = _first;
                while (next != null)
                {
                    yield return next;

                    next = next.Parser.GetBlock<T>(next.Links[Next]);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }

    internal class Mdf4Block<T> : Mdf4Block where T : struct
    {
        internal T Data;

        protected override void InitDataSection()
        {
            Read(out Data);
        }
    }
}
