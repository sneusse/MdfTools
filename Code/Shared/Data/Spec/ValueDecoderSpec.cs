namespace MdfTools.Shared.Data.Spec
{
    public sealed class ValueDecoderSpec
    {
        public RawDecoderSpec RawDecoderSpec { get; }
        public ValueConversionSpec ValueConversionSpec { get; }
        public DisplayConversionSpec DisplayConversionSpec { get; }

        public bool IsNumeric => RawDecoderSpec.IsNumeric;

        public ValueDecoderSpec(RawDecoderSpec rawDecoderSpec, ValueConversionSpec valueConversionSpec,
                                DisplayConversionSpec displayConversionSpec)
        {
            RawDecoderSpec = rawDecoderSpec;
            ValueConversionSpec = valueConversionSpec;
            DisplayConversionSpec = displayConversionSpec;
        }
    }
}
