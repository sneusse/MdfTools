using System.Collections.Generic;
using System.Globalization;

namespace MdfTools.Shared.Data.Spec
{
    public abstract class DisplayConversionSpec
    {
        public static readonly DisplayConversionSpec Default = new Identity();

        public abstract string GetDisplayValue(double value);

        public class Identity : DisplayConversionSpec
        {
            public override string GetDisplayValue(double value)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public class LookupTable : DisplayConversionSpec
        {
            private readonly Dictionary<double, string> _lookup = new Dictionary<double, string>();

            internal LookupTable()
            {

            }

            internal string this[double key]
            {
                get => GetDisplayValue(key);
                set => _lookup[key] = value;
            }

            public override string GetDisplayValue(double value) => 
                _lookup.TryGetValue(value, out var dispValue) ? dispValue : Default.GetDisplayValue(value);
        }
    }
}
