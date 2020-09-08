using System;

namespace CryCompressor
{
    public static class ColorConsole
    {
        public static void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public static void WriteInfo(string message)
        {
            WriteDatetime();
            Console.ResetColor();
            WriteLine(message);
        }

        private static void WriteDatetime()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss.ffff}] ");         
        }

        public static void WriteError(string message)
        {
            WriteDatetime();
            Console.ForegroundColor = ConsoleColor.Red;
            WriteLine(message);     
            Console.ResetColor();
        }
    }
}