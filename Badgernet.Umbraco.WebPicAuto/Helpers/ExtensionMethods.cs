namespace Badgernet.Umbraco.WebPicAuto.Helpers;

public static class ExtensionMethods
{
    public static string ToDiskSize(this long value, int decimalPlaces = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimalPlaces);
        
        if (value < 0) { return "-" + ToDiskSize(-value, decimalPlaces); }
        if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

        // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
        var mag = (int)Math.Log(value, 1024);
        
        // 1L << (mag * 10) == 2 ^ (10 * mag) [i.e. the number of bytes in the unit corresponding to mag]
        var adjustedSize = (decimal)value / (1L << (mag * 10));
        
        // make adjustment when the value is large enough that it would round up to 1000 or more
        if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
        {
            mag += 1;
            adjustedSize /= 1024;
        }

        var suffix = mag switch
        {
            0 => "byte",
            1 => "KB",
            2 => "MB",
            3 => "GB",
            4 => "TB",
            5 => "PB",
            6 => "EB",
            7 => "ZB",
            8 => "YB",
            _ => ""
        };

        return string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, suffix);
    }
}