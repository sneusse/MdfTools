namespace MdfTools.Utils
{
    public static class FormatUtils
    {
        // Found on SO, modified to take unit, lost the link :/ shame on me.
        // Returns the human-readable file size for an arbitrary, 64-bit file size 
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
        public static string GetBytesReadable(long i, string unit = "B")
        {
            // Get absolute value
            var absolute_i = i < 0 ? -i : i;
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = $"E{unit}";
                readable = i >> 50;
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = $"P{unit}";
                readable = i >> 40;
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = $"T{unit}";
                readable = i >> 30;
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = $"G{unit}";
                readable = i >> 20;
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = $"M{unit}";
                readable = i >> 10;
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = $"K{unit}";
                readable = i;
            }
            else
            {
                return i.ToString($"0 {unit}"); // Byte
            }

            // Divide by 1024 to get fractional value
            readable = readable / 1024;
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }
    }
}
