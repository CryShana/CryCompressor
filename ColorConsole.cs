using System;

using static System.Console;

namespace CryCompressor;

public static class ColorConsole
{
    static void WriteDatetime()
    {
        ForegroundColor = ConsoleColor.DarkGray;
        Write($"[{DateTime.Now:HH:mm:ss.ffff}] ");
    }

    public static void WriteInfo(string message)
    {
        WriteDatetime();
        ResetColor();
        WriteLine(message);
    }

    public static void WriteError(string message)
    {
        WriteDatetime();
        ForegroundColor = ConsoleColor.Red;
        WriteLine(message);
        ResetColor();
    }

    public static void WriteUpdate(int total, int current)
    {
        var prg = (current / (double)total) * 100;

        Write("\r");
        WriteDatetime();
        ResetColor();
        Write($"Progress: ");
        ForegroundColor = ConsoleColor.Cyan;
        Write($"{prg:0.00}%");
        ResetColor();
        Write($" ({current}/{total})" + emptyLineShort);
    }

    readonly static string emptyLine = new string(' ', 50);
    readonly static string emptyLineShort = new string(' ', 7);
    public static void WriteEmpty()
    {
        Write("\r");
        Write(emptyLine);
        ResetColor();
    }
}