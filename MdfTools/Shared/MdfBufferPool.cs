using System.Buffers;

namespace MdfTools.Shared
{
    internal static class MdfBufferPool
    {
        // Default pool will allocate above 1Mb. Long channel groups with a lot of records might be bigger.
        // Default pool limits to 50 buffers. Long channel groups with a lot of channels might be bigger.
        // Ram is cheap, performance increase ~ factor 1.1 (small files) - 2 (big files > 2GB)
        public static ArrayPool<byte> Instance = ArrayPool<byte>.Create(1024 * 1024 * 128, 128);


        public static byte[] Rent(in ulong len)
        {
            return Instance.Rent((int) len);
        }

        public static byte[] Rent(in long len)
        {
            return Instance.Rent((int) len);
        }

        public static void Return(byte[] buffer)
        {
            Instance.Return(buffer);
        }
    }
}
