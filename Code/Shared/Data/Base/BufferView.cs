using System;
using MdfTools.Shared.Data.Spec;

namespace MdfTools.Shared.Data.Base
{
    public class BufferView<TDecodable> : IDisposable where TDecodable : IDecodable
    {
        public SampleBuffer Original { get; }

        public TDecodable Channel => (TDecodable) Original.Decodable;

        public BufferView(SampleBuffer original)
        {
            Original = original;
        }

        public T[] GetData<T>()
        {
            return (T[]) Original.Data;
        }

        public Span<T> GetSpan<T>()
        {
            return ((SampleBuffer<T>) Original).Span;
        }

        public override string ToString()
        {
            return $"{Channel}: {Original.Data.Count} samples";
        }

        public void Dispose()
        {
            Original.Dispose();
        }
    }
}
