#define SUPPRESSED

using System;

namespace MdfTools.Shared
{

    // this code should be unreachable
    public class UnexpectedExecutionPath : Exception
    {

    }

    public class Check
    {
        public static void ThrowUnexpectedExecutionPath() => throw new UnexpectedExecutionPath();

        public static void NotImplemented(Exception ex = null)
        {
#if RELEASE
#else
            ex ??= new NotImplementedException();
            throw ex;
#endif
        }

        public static void NotImplementedSuppressed(Exception ex = null)
        {
#if !SUPPRESSED
            ex ??= new NotImplementedException();
            throw ex;
#endif
        }
    }
}
