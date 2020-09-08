using System;

namespace CryCompressor
{
    public static class ColorConsole
    {
        public static void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}