#define SUPPRESSED

using System;

namespace MdfTools.Shared
{
    public class Check
    {
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
