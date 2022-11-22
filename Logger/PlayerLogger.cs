using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Logger
{
    public static class PlayerLogger
    {
        public delegate void OnLog(string info);

        public static OnLog fLogMethod;

        public unsafe static void LogDllOutput(byte* data)
        {
            var vData = Marshal.PtrToStringAnsi((IntPtr)data);

            if (fLogMethod != null)
            {
                fLogMethod(vData);
            }
        }

        public static string LogArray<T>(T[] arrayToLog)
        {
            StringBuilder sb = new StringBuilder();
            
            for (int i = 0; i < arrayToLog.Length; i++)
            {
                sb.Append($"[{i}] => { arrayToLog[i] }, \n");
            }

            return sb.ToString();
        }
    }
}
