namespace PDFLibreria.Extensions;

public static class ByteArrayExtensions
{
    public static bool EndsWith(this byte[] source, byte[] pattern)
    {
        if (source == null || pattern == null || source.Length < pattern.Length)
            return false;

        for (int i = 0; i < pattern.Length; i++)
        {
            if (source[source.Length - pattern.Length + i] != pattern[i])
                return false;
        }
        return true;
    }

} 