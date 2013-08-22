using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace GoogleVoice
{
    public static class Extensions
    {
        public static void Try(this object o, Action a)
        {
            try
            {
                a.Invoke();
            }
            catch (Exception ex)
            {
                Trace.WriteLine("*** Failure: " + ex);
            }
        }

        public static void TryLog(this object o, string log, Action a)
        {
            Trace.WriteLine(log);
            try
            {
                a.Invoke();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("*** {0}: {1}", log, ex));
            }
        }

        public static void TryLogThrow(this object o, string log, Action a)
        {
            Trace.WriteLine(log);
            try
            {
                a.Invoke();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("*** {0}: {1}", log, ex));
                throw;
            }
        }
    }
}
