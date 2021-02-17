namespace MdfTools.Shared.Data.Spec
{
    public abstract class DisplayConversionSpec
    {
        public static readonly DisplayConversionSpec Default = new Identity();

        public class Identity : DisplayConversionSpec
        {
            
        }
    }

}
