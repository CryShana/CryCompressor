using System;

namespace CryCompressor
{
    internal static class Extensions
    {
        internal static string ToTimeString(this double timeMilliseconds)
        {
            if (timeMilliseconds < 1000) return $"{Math.Round(timeMilliseconds, 2)}ms";
            else if (timeMilliseconds < 1000 * 60) return $"{Math.Round((timeMilliseconds / 1000.0), 2)}sec";
            else if (timeMilliseconds < 1000 * 60 * 60) return $"{Math.Round(((timeMilliseconds / 1000.0) / 60), 2)}min";
            else return $"{Math.Round((((timeMilliseconds / 1000.0) / 60) / 60), 2)}h";
        }

        internal static string GetExtensionWithoutDot(this string extension)
        {
            if (string.IsNullOrEmpty(extension)) return extension;

            if (extension.StartsWith('.')) return extension.Substring(1);

            return extension;
        }
    }
}