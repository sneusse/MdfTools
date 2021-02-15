using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MdfTools.Utils
{
    public class UnsafeData<T> : IDisposable where T : unmanaged
    {
        private readonly int _length;
        private readonly IntPtr _ptr;


        public unsafe Span<T> Span => new Span<T>((T*) _ptr, _length);

        public unsafe T Value => Unsafe.AsRef<T>((T*) _ptr);

        public long Address => _ptr.ToInt64();

        public UnsafeData(int length = 1)
        {
            _length = length;
            _ptr = Marshal.AllocHGlobal(length * Unsafe.SizeOf<T>());
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~UnsafeData()
        {
            ReleaseUnmanagedResources();
        }

        private void ReleaseUnmanagedResources()
        {
            Marshal.FreeHGlobal(_ptr);
        }
    }
}
