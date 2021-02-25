// With managed allocations of very long channels
// the large object heap gets filled quite fast
// when loading multiple large files.

using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
            var conv = decoder.ValueConversionSpec.ConversionType;
            NumericBufferBase ret = null;

            if (raw.IsSameEndianess &&
                conv == ValueConversionType.Linear ||
                conv == ValueConversionType.Identity)
                ret = new LinearBuffer(channel, length);

            else if (raw.IsSameEndianess && conv == ValueConversionType.Rational3)
                ret = new Rat3Buffer(channel, length);

            //TODO: add endianess swapped buffer when we have access to validation data.

            if (noConversion)
                ret.DisableConversion();

            if (ret == null) Check.PleaseSendMeYourFile();

            return ret;
        }


#if USE_NATIVE_ALLOCATIONS
        private unsafe class Rat3Buffer : NumericBufferBaseNative
#else
        private class Rat3Buffer : NumericBufferBaseManaged
#endif
        {
            private readonly ulong _mask;
            private readonly int _shift;
            private ValueConversionSpec.Rational3 _conv;

            public Rat3Buffer(IDecodable decodable, long length) : base(decodable, length)
            {
                _conv = (ValueConversionSpec.Rational3) Val;
                _mask = Raw.Mask;
                _shift = Raw.Shift;
            }

            public override void DisableConversion()
            {
                _conv = ValueConversionSpec.Rat3Identity;
            }

            public override void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount)
            {
                var str = (int) (offset + (ulong) Raw.TotalByteOffset);

                var storage = Storage;

                unchecked
                {
                    switch (Raw.NativeType)
                    {
                    case NativeType.NotNative:
                        Check.ThrowUnexpectedExecutionPath();
                        break;
                    case NativeType.UInt8:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            double t = (byte) ((value >> _shift) & _mask);
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt16:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            double t = (ushort) ((value >> _shift) & _mask);
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt32:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            double t = (uint) ((value >> _shift) & _mask);
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt64:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            double t = (ulong) ((value >> _shift) & _mask);
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.Int8:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            double t = (sbyte) ((value >> _shift) & _mask);
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.Int16:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            double t = (short) ((value >> _shift) & _mask);
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.Int32:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            double t = (int) ((value >> _shift) & _mask);
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.Int64:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            double t = (long) ((value >> _shift) & _mask);
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.Float:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<float>(ref raw[str]);
                            double t = value;
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    case NativeType.Double:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<double>(ref raw[str]);
                            double t = value;
                            double t2 = t * t;
                            storage[i] = (_conv.N0 + _conv.N1 * t + _conv.N2 * t2) / (_conv.D0 + _conv.D1 * t + _conv.D2 * t2);
                            str += Stride;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }


#if USE_NATIVE_ALLOCATIONS
        private unsafe class LinearBuffer : NumericBufferBaseNative
#else
        private class LinearBuffer : NumericBufferBaseManaged
#endif
        {
            private ValueConversionSpec.Linear _conv;

            public LinearBuffer(IDecodable decodable, long length) : base(decodable, length)
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

                var storage = Storage;

                var scale = _conv.Scale;
                var ofs = _conv.Offset;
                var mask = Raw.Mask;
                var shift = Raw.Shift;

                unchecked
                {
                    switch (Raw.NativeType)
                    {
                    case NativeType.NotNative:
                        Check.ThrowUnexpectedExecutionPath();
                        break;
                    case NativeType.UInt8:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            storage[i] = (byte) ((value >> shift) & mask) * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt16:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            storage[i] = (ushort) ((value >> shift) & mask) * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt32:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            storage[i] = (uint) ((value >> shift) & mask) * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt64:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            storage[i] = ((value >> shift) & mask) * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.Int8:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            storage[i] = (sbyte) ((value >> shift) & mask) * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.Int16:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            storage[i] = (short) ((value >> shift) & mask) * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.Int32:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            storage[i] = (int) ((value >> shift) & mask) * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.Int64:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            storage[i] = (long) ((value >> shift) & mask) * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.Float:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<float>(ref raw[str]);
                            storage[i] = value * scale + ofs;
                            str += Stride;
                        }

                        break;
                    case NativeType.Double:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<double>(ref raw[str]);
                            storage[i] = value * scale + ofs;
                            str += Stride;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
    }

    public abstract class NumericBufferBaseManaged : NumericBufferBase
    {
        protected readonly double[] Storage;
        public sealed override IList Data => Storage;
        public override Span<double> Span => Storage.AsSpan();

        protected NumericBufferBaseManaged(IDecodable decodable, long length) : base(decodable)
        {
            Storage = new double[length];
        }
    }

    public abstract unsafe class NumericBufferBaseNative : NumericBufferBase
    {
        internal readonly IntPtr HeapArray;
        protected readonly long Length;

        protected readonly double* Storage;
        public sealed override IList Data => null; //TODO: hmm... maybe change the interface?
        public override Span<double> Span => new Span<double>(HeapArray.ToPointer(), (int) Length);

        protected NumericBufferBaseNative(IDecodable decodable, long length) : base(decodable)
        {
            Length = length;
            HeapArray = Marshal.AllocHGlobal((IntPtr) (length * Unsafe.SizeOf<double>()));
            Storage = (double*) HeapArray.ToPointer();
        }

        private void ReleaseUnmanagedResources()
        {
            Marshal.FreeHGlobal(HeapArray);
        }

        protected virtual void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
            {
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NumericBufferBaseNative()
        {
            Dispose(false);
        }
    }

    public abstract class NumericBufferBase : SampleBuffer<double>
    {
        protected readonly RawDecoderSpec Raw;

        protected readonly int Stride;
        protected readonly ValueConversionSpec Val;

        protected NumericBufferBase(IDecodable decodable) : base(decodable)
        {
            var decoder = decodable.DecoderSpec;
            Raw = decoder.RawDecoderSpec;
            Val = decoder.ValueConversionSpec;
            Stride = (int) Raw.Stride;
        }

        public virtual void DisableConversion()
        {
        }

        public override void Dispose()
        {
        }
    }
}
