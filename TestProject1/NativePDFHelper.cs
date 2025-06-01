namespace TestProject1;

public static class NativePDFHelper
{
    public static string[] ExtractPdfObjects(string pdf, int numObjs)
    {
        var objs = new string[numObjs];
        int idx = 0;
        int searchIdx = 0;
        for (int i = 1; i <= numObjs; i++)
        {
            string objTag = $"{i} 0 obj";
            int start = pdf.IndexOf(objTag, searchIdx, StringComparison.Ordinal);
            int end = pdf.IndexOf("endobj", start, StringComparison.Ordinal) + "endobj\n".Length;
            objs[i - 1] = pdf.Substring(start, end - start);
            searchIdx = end;
        }
        return objs;
    }

    public static List<string> ExtractAllPdfObjects(string pdf)
    {
        var objs = new List<string>();
        int searchIdx = 0;
        while (true)
        {
            int objStart = pdf.IndexOf("obj", searchIdx, StringComparison.Ordinal);
            if (objStart == -1) break;
            int lineStart = pdf.LastIndexOf('\n', objStart);
            if (lineStart == -1) lineStart = 0; else lineStart++;
            int objEnd = pdf.IndexOf("endobj", objStart, StringComparison.Ordinal);
            if (objEnd == -1) break;
            objEnd += "endobj".Length;
            if (objEnd < pdf.Length && pdf[objEnd] == '\n') objEnd++;
            objs.Add(pdf.Substring(lineStart, objEnd - lineStart));
            searchIdx = objEnd;
        }
        return objs;
    }

    // Versión robusta para JPEGs estándar (SOF0, SOF2, SOF3, SOF1, SOF9, SOF13)
    public static bool GetJpegDimensions(byte[] bytes, out int width, out int height)
    {
        width = 0; height = 0;
        int i = 0;
        while (i < bytes.Length - 9)
        {
            if (bytes[i] == 0xFF && bytes[i + 1] == 0xD8) // SOI
            {
                i += 2;
                break;
            }
            i++;
        }
        while (i < bytes.Length - 9)
        {
            if (bytes[i] != 0xFF)
            {
                i++;
                continue;
            }
            byte marker = bytes[i + 1];
            if (marker == 0xD9 || marker == 0xDA) // EOI o SOS
                break;
            int segmentLength = (bytes[i + 2] << 8) + bytes[i + 3];
            // SOF0, SOF2, SOF3, SOF1, SOF9, SOF13, etc.
            if ((marker >= 0xC0 && marker <= 0xC3) || (marker >= 0xC5 && marker <= 0xC7)
                || (marker >= 0xC9 && marker <= 0xCB) || (marker >= 0xCD && marker <= 0xCF))
            {
                height = (bytes[i + 5] << 8) + bytes[i + 6];
                width = (bytes[i + 7] << 8) + bytes[i + 8];
                return true;
            }
            i += 2 + segmentLength;
        }
        return false;
    }
}