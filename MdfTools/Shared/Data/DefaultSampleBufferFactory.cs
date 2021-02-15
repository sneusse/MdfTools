using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using MdfTools.Shared.Data.Base;
using MdfTools.Shared.Data.Spec;
using MdfTools.V4;

namespace MdfTools.Shared.Data
{
    public class DefaultSampleBufferFactory : SampleBufferFactory
    {
        private readonly NumericBufferFactory _numericBufferFactory;

        public DefaultSampleBufferFactory()
        {
            _numericBufferFactory = new NumericBufferFactory();
        }

        public override SampleBuffer Allocate(IDecodable channel, long length, bool noConversion)
        {
            using var time = Mdf4File.Metrics.Allocations.Measure(length * 8);

            var spec = channel.DecoderSpec;
            if (spec.IsNumeric) return _numericBufferFactory.Allocate(channel, length, noConversion);

            if (spec.RawDecoderSpec.DataType == DataType.ByteArray)
                return new ByteBuffer(channel, length);

            if (spec.RawDecoderSpec.DataType == DataType.AnsiString)
                return new StringBuffer(channel, length);


            return null;
        }

        private sealed class StringBuffer : SampleBuffer
        {
            private readonly RawDecoderSpec _raw;
            private readonly string[] _storage;

            public override IList Data => _storage;

            public StringBuffer(IDecodable decodable, long length) : base(decodable)
            {
                _raw = decodable.DecoderSpec.RawDecoderSpec;
                _storage = new string[length];
            }

            public override void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount)
            {
                var str = (int) (offset + (ulong) _raw.TotalByteOffset);
                for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                {
                    var value = Encoding.Default.GetString(raw.Slice(str, _raw.TotalByteLength));
                    _storage[i] = value;
                    str += (int) _raw.Stride;
                }
            }
        }

        private sealed class ByteBuffer : SampleBuffer
        {
            private readonly byte[] _storage;

            public override IList Data => _storage;

            public ByteBuffer(IDecodable decodable, long length) : base(decodable)
            {
                _storage = new byte[decodable.DecoderSpec.RawDecoderSpec.TotalByteLength * length];
            }

            public override void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount)
            {
                var byteLength = Decodable.DecoderSpec.RawDecoderSpec.TotalByteLength;
                offset += (ulong) Decodable.DecoderSpec.RawDecoderSpec.ByteOffset;
                for (var i = sampleStart; i < sampleStart + sampleCount; i++)
                {
                    Unsafe.CopyBlock(ref raw[(int) offset], ref _storage[i * (ulong) byteLength],
                                     (uint) byteLength);
                    offset += Decodable.DecoderSpec.RawDecoderSpec.Stride;
                }
            }
        }

        // public virtual SampleBufferFactory GetBufferFactory(IDecodable decodable)
        // {
        //     var decoder = decodable.DecoderSpec;
        //     if (decoder.IsNumeric)
        //     {
        //         return _numericBufferFactory.GetBufferFactory(decodable);
        //         // return _tccPackerFactory.GetBufferFactory(decoderSpec);
        //     }
        //
        //     if (decoder.RawDecoderSpec.DataType == DataType.ByteArray)
        //         return new SimpleByteArrayPacker(decodable);
        //
        //     if (decoder.RawDecoderSpec.DataType == DataType.AnsiString)
        //         return new StringPacker(decodable);
        //
        //
        //     return null;
        // }
    }
}
