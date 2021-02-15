using System;
using System.Collections.Generic;
using MdfTools.Shared;
using MdfTools.Shared.Data.Base;
using MdfTools.Shared.Data.Spec;

namespace MdfTools.V4
{
    public class Mdf4Channel : IDecodable
    {
        private readonly Mdf4CNBlock _cnBlock;

        private ValueDecoderSpec _spec;

        public string Name => _cnBlock.Name;
        public string Comment => _cnBlock.Comment;

        public string Source => _cnBlock.Source?.SourceName;
        public Mdf4ChannelGroup ChannelGroup { get; }

        public Mdf4Channel Master => ChannelGroup.MasterChannel;

        internal Mdf4Channel(Mdf4ChannelGroup channelGroup, Mdf4CNBlock cnBlock)
        {
            ChannelGroup = channelGroup;
            _cnBlock = cnBlock;

            if (_cnBlock.Data.ChannelType == Mdf4CNBlock.ChannelType.Master)
                channelGroup.MasterChannel = this;

            if (cnBlock.Data.ChannelType == Mdf4CNBlock.ChannelType.VariableLength)
                Check.NotImplementedSuppressed();
        }

        public ValueDecoderSpec DecoderSpec => _spec ??= CreateDataSpecLazy();

        public SampleBuffer CreateBuffer(long length, bool noConversion = false)
        {
            return ChannelGroup.File.SampleBufferFactory.Allocate(this, length, noConversion);
        }

        private (ValueConversionSpec Val, DisplayConversionSpec Disp) CreateConverters()
        {
            DisplayConversionSpec display = DisplayConversionSpec.Default;
            ValueConversionSpec values = ValueConversionSpec.Default;

            var c = _cnBlock.Conversion;
            if (c == null) return (values, display);

            //TODO: add cache here?

            var p = c.Params;

            switch (c.Data.ConversionType)
            {
            case Mdf4CCBlock.ConversionType.Identity:
                values = ValueConversionSpec.Default;
                break;
            case Mdf4CCBlock.ConversionType.Linear:
                if (c.Params[0] == 0 && c.Params[1] == 1)
                    values = ValueConversionSpec.Default;
                else
                    values = new ValueConversionSpec.Linear(p[0], p[1]);

                break;
            case Mdf4CCBlock.ConversionType.Rational:
                if (p[0] == 0 && p[3] == 0 && p[4] == 0 && p[5] == 1)
                    values = new ValueConversionSpec.Linear(p[2], p[1]);
                else
                    Check.NotImplemented(new NotImplementedException());

                break;
            case Mdf4CCBlock.ConversionType.AlgebraicText:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CCBlock.ConversionType.ValToValInterp:
                if (p.Length == 4 && p[0] == p[1] && p[2] == p[3])
                    values = ValueConversionSpec.Default;
                else
                    Check.NotImplemented(new NotImplementedException());

                break;
            case Mdf4CCBlock.ConversionType.ValToValNoInterp:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CCBlock.ConversionType.ValRangeToValTab:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CCBlock.ConversionType.ValToTextScaleTab:
            {
                //TODO: add the text stuff again (and clean it up.)
                var len = p.Length;
                var allAreText = true;
                var items = new Mdf4Block[len];
                for (var i = 0; i < len; i++)
                {
                    var blk = c.Ref(i);
                    allAreText &= blk is Mdf4TXBlock;
                    items[i] = blk;
                }

                if (allAreText)
                {
                    var lookup = new Dictionary<double, string>();
                    for (var i = 0; i < items.Length; i++) lookup[i] = items[i].ToString();

                    // text = new TextConverter.LookupConverter(lookup);
                }
                else
                {
                    Check.NotImplemented(new NotImplementedException());
                }

                break;
            }
            case Mdf4CCBlock.ConversionType.ValRangeToTextScaleTab:
            {
                //TODO: add the text stuff again (and clean it up.)
                var allAreText = true;
                var items = new Mdf4Block[c.Data.RefCount];
                for (var i = 0; i < items.Length; i++)
                {
                    var blk = c.Ref(i);
                    var okIsh = blk is Mdf4TXBlock;
                    if (!okIsh && blk is Mdf4CCBlock c2 &&
                        c2.Data.ConversionType == Mdf4CCBlock.ConversionType.Identity)
                        okIsh = true;

                    allAreText &= okIsh;
                    items[i] = blk;
                }

                if (!allAreText) Check.NotImplemented(new NotImplementedException());

                var thereIsNoRange = true;
                for (var i = 0; i < p.Length; i += 2) thereIsNoRange &= p[i] == p[i + 1];

                if (thereIsNoRange)
                {
                    var lookup = new Dictionary<double, string>();
                    for (var i = 0; i < items.Length; i++) lookup[i] = items[i].ToString();

                    // text = new TextConverter.LookupConverter(lookup);
                }
                else
                {
                    Check.NotImplemented(new NotImplementedException());
                }


                break;
            }
            case Mdf4CCBlock.ConversionType.TextToVal:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CCBlock.ConversionType.TextToText:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CCBlock.ConversionType.BitfieldText:
                Check.NotImplemented(new NotImplementedException());
                break;
            }

            return (values, display);
        }

        private ValueDecoderSpec CreateDataSpecLazy()
        {
            int extraOffsetFromMdfTrashSpec = ChannelGroup.RecordIdSize;

            // var spec = new RawDecoderSpec();
            ref var data = ref _cnBlock.Data;
            var stride = ChannelGroup.RecordLength;
            var bitOffset = data.BitOffset;
            var byteOffset = (int) data.ByteOffset + extraOffsetFromMdfTrashSpec;
            var bitLength = (int) data.BitLength;
            var byteOrder = ByteOrder.Undefined;
            var dataType = DataType.Unknown;
            switch (data.DataType)
            {
            case Mdf4CNBlock.ChannelDataType.UnsignedLittleEndian:
                byteOrder = ByteOrder.Intel;
                dataType = DataType.Unsigned;
                break;
            case Mdf4CNBlock.ChannelDataType.UnsignedBigEndian:
                byteOrder = ByteOrder.Motorola;
                dataType = DataType.Unsigned;
                break;
            case Mdf4CNBlock.ChannelDataType.SignedLittleEndian:
                byteOrder = ByteOrder.Intel;
                dataType = DataType.Signed;
                break;
            case Mdf4CNBlock.ChannelDataType.SignedBigEndian:
                byteOrder = ByteOrder.Motorola;
                dataType = DataType.Signed;
                break;
            case Mdf4CNBlock.ChannelDataType.FloatLittleEndian:
                byteOrder = ByteOrder.Intel;
                dataType = DataType.Float;
                break;
            case Mdf4CNBlock.ChannelDataType.FloatBigEndian:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.AnsiString:
                dataType = DataType.AnsiString;
                break;
            case Mdf4CNBlock.ChannelDataType.Utf8String:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.Utf16LeString:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.Utf18BeString:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.ByteArray:
                dataType = DataType.ByteArray;
                break;
            case Mdf4CNBlock.ChannelDataType.MIMESample:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.MIMEStream:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.CANopenDate:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.CANopenTime:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.ComplexLe:
                Check.NotImplemented(new NotImplementedException());
                break;
            case Mdf4CNBlock.ChannelDataType.ComplexBe:
                Check.NotImplemented(new NotImplementedException());
                break;
            default:
                throw new ArgumentOutOfRangeException();
            }

            var packSpec = new RawDecoderSpec(stride, byteOffset, bitOffset, bitLength, byteOrder, dataType);
            var converters = CreateConverters();
            return new ValueDecoderSpec(packSpec, converters.Val, converters.Disp);
        }

        public override string ToString()
        {
            return $"{Name}/{ChannelGroup.Name}/{Source}";
        }
    }
}
