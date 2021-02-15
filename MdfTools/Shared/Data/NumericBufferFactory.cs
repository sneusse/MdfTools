using System;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using MdfTools.Shared.Data.Base;
using MdfTools.Shared.Data.Spec;

namespace MdfTools.Shared.Data
{
    public class NumericBufferFactory : SampleBufferFactory
    {
        public override SampleBuffer Allocate(IDecodable channel, long length, bool noConversion)
        {
            var decoder = channel.DecoderSpec;
            var raw = decoder.RawDecoderSpec;
            NumericBufferBase ret = null;
            if (raw.DataType == DataType.Float && raw.BitLength == 32)
                ret = new FloatSampleBufferLinear(channel, length);
            if (raw.DataType == DataType.Float && raw.BitLength == 64)
                ret = new DoubleSampleBufferLinear(channel, length);
            if (raw.DataType == DataType.Unsigned || raw.DataType == DataType.Signed)
                ret = new IntBufferLinear(channel, length);

            if (noConversion)
                ret.DisableConversion();

            return ret;
        }

        private class FloatSampleBufferLinear : NumericBufferBase
        {
            private ValueConversionSpec.Linear _conv;

            public FloatSampleBufferLinear(IDecodable decodable, long length) : base(decodable, length)
            {
                _conv = Val as ValueConversionSpec.Linear ?? ValueConversionSpec.LinearIdentity;
            }

            public override void DisableConversion()
            {
                _conv = ValueConversionSpec.LinearIdentity;
            }

            public override void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount)
            {
                var str = (int) (offset + (ulong) Raw.TotalByteOffset);
                for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                {
                    ref var bytes = ref raw[str];
                    var value = Unsafe.ReadUnaligned<float>(ref bytes);
                    Storage[i] = value * _conv.Scale + _conv.Offset;
                    str += Stride;
                }
            }
        }

        private class DoubleSampleBufferLinear : NumericBufferBase
        {
            private ValueConversionSpec.Linear _conv;

            public DoubleSampleBufferLinear(IDecodable decodable, long length) : base(decodable, length)
            {
                _conv = Val as ValueConversionSpec.Linear ?? ValueConversionSpec.LinearIdentity;
            }

            public override void DisableConversion()
            {
                _conv = ValueConversionSpec.LinearIdentity;
            }

            public override void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount)
            {
                var str = (int) (offset + (ulong) Raw.TotalByteOffset);
                for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                {
                    ref var bytes = ref raw[str];
                    var value = Unsafe.ReadUnaligned<double>(ref bytes);
                    Storage[i] = value * _conv.Scale + _conv.Offset;
                    str += Stride;
                }
            }
        }

        private class IntBufferLinear : NumericBufferBase
        {
            private readonly ulong _mask;
            private readonly int _shift;
            private ValueConversionSpec.Linear _conv;

            public IntBufferLinear(IDecodable decodable, long length) : base(decodable, length)
            {
                _conv = Val as ValueConversionSpec.Linear ?? ValueConversionSpec.LinearIdentity;
                _mask = Raw.Mask;
                _shift = Raw.Shift;
            }

            public override void DisableConversion()
            {
                _conv = ValueConversionSpec.LinearIdentity;
            }

            public override void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount)
            {
                var str = (int) (offset + (ulong) Raw.TotalByteOffset);
                for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                {
                    ref var bytes = ref raw[str];
                    var value = Unsafe.ReadUnaligned<ulong>(ref bytes);
                    Storage[i] = ((value >> _shift) & _mask) * _conv.Scale + _conv.Offset;
                    str += Stride;
                }
            }
        }

        private class IntBufferLinearSimd : NumericBufferBase
        {
            private readonly ValueConversionSpec.Linear _conv;
            private readonly ulong _mask;
            private readonly Vector<double> _offsetVector;
            private readonly int _shift;

            public IntBufferLinearSimd(IDecodable decodable, long length) : base(decodable, length)
            {
                _conv = Val as ValueConversionSpec.Linear ?? ValueConversionSpec.LinearIdentity;
                _mask = Raw.Mask;
                _shift = Raw.Shift;
                var factor = (ulong) (1 << _shift);
                _offsetVector =
                    new Vector<double>(new[] {_conv.Offset, _conv.Offset, _conv.Offset, _conv.Offset});
            }

            public override void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount)
            {
                throw new NotImplementedException();
                // int str = (int) (offset + (ulong) Raw.TotalByteOffset);
                // Debug.Assert(Vector<ulong>.Count == 4);
                // for (long i = (long) sampleStart; i < (long) (sampleStart + sampleCount - 4); i += 4)
                // {
                //     try
                //     {
                //         // ReSharper disable once StackAllocInsideLoop - this is correct. we need dis memory.
                //         Span<double> data = stackalloc double[4];
                //         data[0] = (Unsafe.ReadUnaligned<ulong>(ref raw[str]) >> _shift) & _mask;
                //         str += Stride;
                //         data[1] = (Unsafe.ReadUnaligned<ulong>(ref raw[str]) >> _shift) & _mask;
                //         str += Stride;
                //         data[2] = (Unsafe.ReadUnaligned<ulong>(ref raw[str]) >> _shift) & _mask;
                //         str += Stride;
                //         data[3] = (Unsafe.ReadUnaligned<ulong>(ref raw[str]) >> _shift) & _mask;
                //         str += Stride;
                //
                //         Vector<double> vec = new Vector<double>(data);
                //         var res = (vec * _conv.Scale) + _offsetVector;
                //         res.CopyTo(Storage, (int) i);
                //     }
                //     catch (Exception e)
                //     {
                //         Console.WriteLine(e);
                //     }
                // }
            }
        }

        private abstract class NumericBufferBase : SampleBuffer
        {
            protected readonly RawDecoderSpec Raw;
            protected readonly double[] Storage;
            protected readonly int Stride;
            protected readonly ValueConversionSpec Val;

            public sealed override IList Data => Storage;

            protected NumericBufferBase(IDecodable decodable, long length) : base(decodable)
            {
                Storage = new double[length];
                var decoder = decodable.DecoderSpec;
                Raw = decoder.RawDecoderSpec;
                Val = decoder.ValueConversionSpec;
                Stride = (int) Raw.Stride;
            }

            public virtual void DisableConversion()
            {
            }
        }
    }
}
