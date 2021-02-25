using MdfTools.Shared.Data.Spec;

namespace MdfTools.Shared.Data.Base
{
    public static class DecodableExtensions
    {
        public static string GetDisplayValue(this IDecodable decodable, double value) => 
            decodable.DecoderSpec.DisplayConversionSpec.GetDisplayValue(value);
    }

    public interface IDecodable
    {
        ValueDecoderSpec DecoderSpec { get; }
    }
}
