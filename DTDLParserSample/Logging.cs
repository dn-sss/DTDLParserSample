namespace DTDLParserSample
{
    using System;
    public static class Logging
    {

        static public void LogOutPutNoCR(string S, ConsoleColor Color = ConsoleColor.White)
        {
            Console.ForegroundColor = Color;
            Console.Write(S);
            Console.ForegroundColor = ConsoleColor.White;
        }

        static public void LogOutPut(string S, ConsoleColor Color = ConsoleColor.White)
        {
            Console.ForegroundColor = Color;
            Console.WriteLine(S);
            Console.ForegroundColor = ConsoleColor.White;
        }

        static public void LogError(string S)
        {
            LogOutPut(S, ConsoleColor.Red);
        }

        static public void LogError(Exception Ex, string S)
        {
            var exception = S + "\n" + Ex.ToString();
            LogOutPut(exception, ConsoleColor.Red);
        }

        static public void LogWarn(string S)
        {
            LogOutPut(S, ConsoleColor.DarkYellow);
        }
        static public void LogSuccess(string S)
        {
            LogOutPut(S, ConsoleColor.Green);
        }
    }
}
