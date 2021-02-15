// With managed allocations of very long channels
// the large object heap gets filled quite fast
// when loading multiple large files.

#define USE_NATIVE_ALLOCATIONS

using System;
using System.Collections;
using System.Net.Http.Headers;
using System.Numerics;
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
            {
                ret = new LinearBuffer(channel, length);
            }

            //TODO: add endianess swapped buffer when we have access to validation data.

            if (noConversion)
                ret.DisableConversion();

            if (ret == null)
            {
            }

            return ret;
        }

        private class LinearBuffer : NumericBufferBase
        {
            private ValueConversionSpec.Linear _conv;
            private readonly ulong _mask;
            private readonly int _shift;

            public LinearBuffer(IDecodable decodable, long length) : base(decodable, length)
            {
                _conv = Val as ValueConversionSpec.Linear ?? ValueConversionSpec.LinearIdentity;
                _mask = Raw.Mask;
                _shift = Raw.Shift;
            }

            public override void DisableConversion()
            {
                _conv = ValueConversionSpec.LinearIdentity;
            }

            public override unsafe void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount)
            {
                var str = (int) (offset + (ulong) Raw.TotalByteOffset);
#if USE_NATIVE_ALLOCATIONS
                double* Storage = (double*) HeapArray.ToPointer();
#endif
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
                            Storage[i] = ((byte) ((value >> _shift) & _mask)) * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt16:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            Storage[i] = ((ushort) ((value >> _shift) & _mask)) * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt32:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            Storage[i] = ((uint) ((value >> _shift) & _mask)) * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.UInt64:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            Storage[i] = ((ulong) ((value >> _shift) & _mask)) * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.Int8:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            Storage[i] = ((sbyte) ((value >> _shift) & _mask)) * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.Int16:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            Storage[i] = ((short) ((value >> _shift) & _mask)) * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.Int32:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            Storage[i] = ((int) ((value >> _shift) & _mask)) * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.Int64:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<ulong>(ref raw[str]);
                            Storage[i] = ((long) ((value >> _shift) & _mask)) * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.Float:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<float>(ref raw[str]);
                            Storage[i] = value * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    case NativeType.Double:
                        for (var i = sampleStart; i < sampleStart + sampleCount; ++i)
                        {
                            var value = Unsafe.ReadUnaligned<double>(ref raw[str]);
                            Storage[i] = value * _conv.Scale + _conv.Offset;
                            str += Stride;
                        }

                        break;
                    }
                }
            }
        }

        private abstract class NumericBufferBase : SampleBuffer<double>, IDisposable
        {
#if USE_NATIVE_ALLOCATIONS
            protected readonly IntPtr HeapArray;
            protected readonly long Length;
            public sealed override IList Data => null; //TODO: hmm... maybe change the interface?
            public override unsafe Span<double> Span => new Span<double>(HeapArray.ToPointer(), (int) Length);
#else
            protected readonly double[] Storage;
            public sealed override IList Data => Storage;
#endif

            protected readonly RawDecoderSpec Raw;
            protected readonly ValueConversionSpec Val;

            protected readonly int Stride;


            protected NumericBufferBase(IDecodable decodable, long length) : base(decodable)
            {
#if USE_NATIVE_ALLOCATIONS
                Length = length;
                HeapArray = Marshal.AllocHGlobal((IntPtr) (length * 8));
#else
                Storage = new double[length];
#endif
                var decoder = decodable.DecoderSpec;
                Raw = decoder.RawDecoderSpec;
                Val = decoder.ValueConversionSpec;
                Stride = (int) Raw.Stride;
            }

            public virtual void DisableConversion()
            {
            }

            private void ReleaseUnmanagedResources()
            {
                // TODO release unmanaged resources here
            }

            protected virtual void Dispose(bool disposing)
            {
                ReleaseUnmanagedResources();
                if (disposing)
                {
#if USE_NATIVE_ALLOCATIONS
                    Marshal.FreeHGlobal(HeapArray);
#endif
                }
            }

            public override void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~NumericBufferBase()
            {
                Dispose(false);
            }
        }
    }
}
