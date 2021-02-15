using System;
using System.Runtime.InteropServices;

namespace MdfTools.Utils
{
    public class UnknownArray
    {
        internal ulong[] Storage;

        public Span<double> Float => MemoryMarshal.Cast<ulong, double>(Storage);
        public Span<long> Long => MemoryMarshal.Cast<ulong, long>(Storage);

        public UnknownArray(int length)
        {
            Storage = new ulong[length];
        }
    }
}
