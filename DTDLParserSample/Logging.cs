namespace DTDLParserSample
{
    using System;
    public static class Logging
    {

        static public void LogOutPutNoCR(string s, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write(s);
            Console.ForegroundColor = ConsoleColor.White;
        }

        static public void LogOutPut(string s, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(s);
            Console.ForegroundColor = ConsoleColor.White;
        }

        static public void LogError(string s)
        {
            LogOutPut(s, ConsoleColor.Red);
        }

        static public void LogError(Exception ex, string s)
        {
            var exception = s + "\n" + ex.ToString();
            LogOutPut(exception, ConsoleColor.Red);
        }

        static public void LogWarn(string s)
        {
            LogOutPut(s, ConsoleColor.DarkYellow);
        }
        static public void LogSuccess(string s)
        {
            LogOutPut(s, ConsoleColor.Green);
        }
    }
}
