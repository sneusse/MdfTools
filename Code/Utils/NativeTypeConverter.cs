using System;
using System.Reflection.Emit;

namespace MdfTools.Utils
{
    public static class NativeTypeConverter
    {
        public static TTo Convert<TFrom, TTo>(TFrom value)
        {
            return ConverterHelper<TFrom, TTo>.Convert(value);
        }

        // loosely based on: https://stackoverflow.com/questions/3343551/how-to-cast-a-value-of-generic-type-t-to-double-without-boxing
        private static class ConverterHelper<TFrom, TTo>
        {
            internal static readonly Func<TFrom, TTo> Convert = EmitConverter();

            private static Func<TFrom, TTo> EmitConverter()
            {
                var method = new DynamicMethod(string.Empty, typeof(TTo), new[] {typeof(TFrom)});
                var il = method.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                if (typeof(TFrom) != typeof(TTo))
                    switch (default(TTo))
                    {
                    case bool _:
                        il.Emit(OpCodes.Conv_I); // TODO: check if this is actually correct -.-
                        break;
                    case double _:
                        il.Emit(OpCodes.Conv_R8);
                        break;
                    case float _:
                        il.Emit(OpCodes.Conv_R4);
                        break;
                    case sbyte _:
                        il.Emit(OpCodes.Conv_I1);
                        break;
                    case short _:
                        il.Emit(OpCodes.Conv_I2);
                        break;
                    case int _:
                        il.Emit(OpCodes.Conv_I4);
                        break;
                    case long _:
                        il.Emit(OpCodes.Conv_I8);
                        break;
                    case byte _:
                        il.Emit(OpCodes.Conv_U1);
                        break;
                    case ushort _:
                        il.Emit(OpCodes.Conv_U2);
                        break;
                    case uint _:
                        il.Emit(OpCodes.Conv_U4);
                        break;
                    case ulong _:
                        il.Emit(OpCodes.Conv_U8);
                        break;
                    default:
                        throw new NotImplementedException($"conversion to {typeof(TTo)} cannot be done.");
                    }

                il.Emit(OpCodes.Ret);

                return (Func<TFrom, TTo>) method.CreateDelegate(typeof(Func<TFrom, TTo>));
            }
        }
    }
}
