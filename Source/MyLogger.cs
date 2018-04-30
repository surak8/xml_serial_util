using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace NSLaserCfg {
    static class MyLogger {
        public static void log(MethodBase mb) {
            log(makeSig(mb));
        }

        public static string makeSig(MethodBase mb) {
            return mb.ReflectedType.Name + "." + mb.Name + makeParm(mb);
        }

        static string makeParm(MethodBase mb) {
            StringBuilder sb = new StringBuilder();
            int n = 0;

            sb.Append("(");
            foreach (var avar in mb.GetParameters()) {
                if (n > 0)
                    sb.Append(", ");
                sb.Append(avar.ParameterType.Name + " " + avar.Name);
                n++;
            }
            sb.Append(")");
            return sb.ToString();
        }

        public static void log(MethodBase mb, string msg) {
            log(makeSig(mb) + ":" + msg);
        }

        public static void log(MethodBase mb, Exception ex) {
            log(mb, decomposeException(ex) + Environment.NewLine + ex.StackTrace);
        }

        public static void log(string msg) {
            Trace.WriteLine(msg);
        }

        public static string decomposeException(Exception ex) {
            StringBuilder sb = new StringBuilder();
            Exception exo = ex;

            while (exo != null) {
                sb.AppendLine(exo.GetType().FullName + ":" + exo.Message);
                exo = exo.InnerException;
            }
            return sb.ToString();
        }
    }
}