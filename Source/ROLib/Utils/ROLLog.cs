using System;
using UnityEngine;

namespace ROLib
{
    public static class ROLLog
    {
        public static readonly bool debugMode = true;

        public static void stacktrace()
        {
            MonoBehaviour.print(Environment.StackTrace);
        }

        public static void log(string line)
        {
            MonoBehaviour.print($"<color=#00FF00>ROL-LOG: </color>{line}");
        }

        public static void error(string line)
        {
            MonoBehaviour.print($"<color=#FF0000>ROL-ERROR: {line}</color>");
        }

        public static void debug(string line)
        {
            if (!debugMode) { return; }
            MonoBehaviour.print($"<color=#0000FF>ROL-DEBUG: </color>{line}");
        }

        public static void exc(Exception e)
        {
            MonoBehaviour.print("ROL-LOG  : " + e);
        }
    }
}
