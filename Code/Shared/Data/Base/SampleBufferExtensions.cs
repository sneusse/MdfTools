namespace MdfTools.Shared.Data.Base
{
    public static class SampleBufferExtensions
    {
        public static BufferView<TDecodable> CreateView<TDecodable>(this SampleBuffer buffer)
            where TDecodable : IDecodable
        {
            return new BufferView<TDecodable>(buffer);
        }
    }
}
