using System;
using System.Runtime.InteropServices;

namespace MdfTools.Native
{
    internal static class LibDeflateDecompress
    {
        // TODO: cleanup this memory?
        [ThreadStatic] private static IntPtr _libdeflateHandle;

        [DllImport("libdeflate_x64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libdeflate_alloc_decompressor();

        [DllImport("libdeflate_x64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe libdeflate_result
            libdeflate_deflate_decompress(
                IntPtr decompressor,
                void* compressedData,
                ulong compressedDataLength,
                void* decompressedData,
                ulong decompressedBufferSize,
                ulong* actual_out_nbytes_ret);

        public static unsafe void Decompress(IntPtr input, ulong inSize, Span<byte> output, ulong outSize)
        {
            if (_libdeflateHandle == IntPtr.Zero)
                _libdeflateHandle = libdeflate_alloc_decompressor();

            var inputPtr = (void*) input;
            fixed (byte* outputPtr = output)
            {
                var result = libdeflate_deflate_decompress(
                    _libdeflateHandle,
                    inputPtr, inSize,
                    outputPtr, outSize,
                    (ulong*) 0);

                if (result != libdeflate_result.LIBDEFLATE_SUCCESS)
                    throw new InvalidOperationException();
            }
        }

        private enum libdeflate_result
        {
            /* Decompression was successful.  */
            LIBDEFLATE_SUCCESS = 0,

            /* Decompressed failed because the compressed data was invalid, corrupt,
             * or otherwise unsupported.  */
            LIBDEFLATE_BAD_DATA = 1,

            /* A NULL 'actual_out_nbytes_ret' was provided, but the data would have
             * decompressed to fewer than 'out_nbytes_avail' bytes.  */
            LIBDEFLATE_SHORT_OUTPUT = 2,

            /* The data would have decompressed to more than 'out_nbytes_avail'
             * bytes.  */
            LIBDEFLATE_INSUFFICIENT_SPACE = 3
        }
    }
}
