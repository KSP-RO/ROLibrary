using System;
using UnityEngine;

namespace ROLib
{
    public static class ROLLog
    {
        public static readonly bool debugMode = true;

        public static void stacktrace()
        {
            MonoBehaviour.print(System.Environment.StackTrace);
        }

        public static void log(string line)
        {
            MonoBehaviour.print("ROL-LOG  : " + line);
        }

        public static void log(System.Object obj)
        {
            MonoBehaviour.print("ROL-LOG  : " + obj);
        }

        public static void error(string line)
        {
            MonoBehaviour.print("ROL-ERROR: " + line);
        }

        public static void debug(string line)
        {
            if (!debugMode) { return; }
            MonoBehaviour.print("ROL-DEBUG: " + line);
        }

        public static void debug(object obj)
        {
            if (!debugMode) { return; }
            MonoBehaviour.print(obj);
        }
    }
}
