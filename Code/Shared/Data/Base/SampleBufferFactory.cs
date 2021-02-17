using MdfTools.Shared.Data.Spec;

namespace MdfTools.Shared.Data.Base
{
    public abstract class SampleBufferFactory
    {
        public virtual void Cache(ValueDecoderSpec spec)
        {
        }

        public virtual void FinalizeCache()
        {
        }

        public abstract SampleBuffer Allocate(IDecodable channel, long length, bool noConversion);
    }
}
