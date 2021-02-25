namespace MdfTools.Shared.Data.Spec
{
    public abstract class ValueConversionSpec
    {
        public static readonly Identity Default = new Identity();
        public static readonly Linear LinearIdentity = new Linear(0, 1);
        public static readonly Rational3 Rat3Identity = new Rational3(0, 1, 0, 1, 0, 0);

        public abstract ValueConversionType ConversionType { get; }

        public class Linear : ValueConversionSpec
        {
            public double Offset { get; }
            public double Scale { get; }

            public override ValueConversionType ConversionType => ValueConversionType.Linear;

            public Linear(double offset, double scale)
            {
                Offset = offset;
                Scale = scale;
            }
        }

        public class Identity : ValueConversionSpec
        {
            public override ValueConversionType ConversionType => ValueConversionType.Identity;
        }

        public class Rational3 : ValueConversionSpec
        {
            public double N0 { get; }
            public double N1 { get; }
            public double N2 { get; }
            public double D0 { get; }
            public double D1 { get; }
            public double D2 { get; }
            public override ValueConversionType ConversionType => ValueConversionType.Rational3;

            public Rational3(double n0, double n1, double n2, double d0, double d1, double d2)
            {
                N0 = n0;
                N1 = n1;
                N2 = n2;
                D0 = d0;
                D1 = d1;
                D2 = d2;
            }
        }
    }
}
