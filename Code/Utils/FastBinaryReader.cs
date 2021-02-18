using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MdfTools.Utils
{
    public sealed unsafe class FastBinaryReader : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly ThreadLocal<long> _position = new ThreadLocal<long>(() => 0);
        private readonly byte* _startOfFile = null;
        private readonly MemoryMappedViewAccessor _view;

        public long Position
        {
            get => _position.Value;
            private set => _position.Value = value;
        }

        public FastBinaryReader(string filename)
        {
            _mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.Open, null);
            _view = _mmf.CreateViewAccessor();
            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _startOfFile);
        }

        public void Dispose()
        {
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _view?.Dispose();
            _mmf?.Dispose();
        }

        public IntPtr GetRawPointer(long offset)
        {
            return (IntPtr) (_startOfFile + offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref T GetRaw<T>(long position)
        {
            Debug.Assert(position < _view.Capacity);
            return ref Unsafe.AsRef<T>(_startOfFile + position);
        }

        internal void Seek(long position, SeekOrigin origin = SeekOrigin.Begin)
        {
            switch (origin)
            {
            case SeekOrigin.Begin:
                Position = position;
                break;
            case SeekOrigin.Current:
                Position += position;
                break;
            case SeekOrigin.End:
                throw new NotImplementedException("nah.");
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }
        }

        public string ReadAscii(long length)
        {
            var s = Encoding.Default.GetString(_startOfFile + Position, (int) length);
            Position += length + 1; // 0 char
            return s;
        }

        public string ReadUTF8(long length)
        {
            var s = Encoding.UTF8.GetString(_startOfFile + Position, (int) length);
            Position += length + 1; // 0 char
            return s;
        }

        internal void Read<T>(out T val) where T : struct
        {
            _view.Read(Position, out val);
            Position += Unsafe.SizeOf<T>();
        }

        internal void ReadArray<T>(ref T[] r, int offset = 0, int count = 0) where T : struct
        {
            count = count == 0 ? r.Length : count;
            _view.ReadArray(Position, r, offset, count);
            Position += Unsafe.SizeOf<T>() * count;
        }
        internal T[] ReadArray<T>(long position, int length) where T : struct
        {
            var r = new T[length];
            ReadArray(ref r);
            return r;
        }

        internal void BlockCopy(long position, ref byte[] dest, int offset, int count)
        {
            fixed (byte* tgt = dest)
            {
                Unsafe.CopyBlock(tgt + offset, _startOfFile + position, (uint) count);
            }
        }
    }
}
