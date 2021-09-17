using System;
using System.Text;

namespace Logger
{
    public static class PlayerLogger
    {
        public delegate void OnLog(string info);

        public static OnLog fLogMethod;

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
