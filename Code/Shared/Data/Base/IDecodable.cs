using MdfTools.Shared.Data.Spec;

namespace MdfTools.Shared.Data.Base
{
    public interface IDecodable
    {
        ValueDecoderSpec DecoderSpec { get; }
    }
}
