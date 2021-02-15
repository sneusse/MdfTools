using System;
using System.Collections;

namespace MdfTools.Shared.Data.Base
{
    public abstract class SampleBuffer : IDisposable
    {
        internal IDecodable Decodable { get; }
        public abstract IList Data { get; }

        protected SampleBuffer(IDecodable decodable)
        {
            Decodable = decodable;
        }

        public abstract void Update(Span<byte> raw, ulong offset, ulong sampleStart, uint sampleCount);

        public abstract void Dispose();
    }
}
