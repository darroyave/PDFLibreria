using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PDFDanNativo.Core;

public static class PDFHelper
{
    public static string[] ExtractPdfObjects(string pdf, int numObjs)
    {
        var objs = new string[numObjs];
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

    public static string[] ExtractPdfObjects(string pdf)
    {
        var objects = new List<string>();
        int start = 0;
        while (true)
        {
            int objStart = pdf.IndexOf("obj", start);
            if (objStart == -1) break;

            int objEnd = pdf.IndexOf("endobj", objStart);
            if (objEnd == -1) break;

            objects.Add(pdf.Substring(objStart - 10, objEnd + 6 - (objStart - 10)));
            start = objEnd + 6;
        }
        return objects.ToArray();
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
            if (marker >= 0xC0 && marker <= 0xC3 || marker >= 0xC5 && marker <= 0xC7
                || marker >= 0xC9 && marker <= 0xCB || marker >= 0xCD && marker <= 0xCF)
            {
                height = (bytes[i + 5] << 8) + bytes[i + 6];
                width = (bytes[i + 7] << 8) + bytes[i + 8];
                return true;
            }
            i += 2 + segmentLength;
        }
        return false;
    }

    // --- Helpers para PDF encryption RC4 40bits ---
    public static byte[] PadOrTruncatePassword(string pwd)
    {
        byte[] pad = {
        0x28,0xBF,0x4E,0x5E,0x4E,0x75,0x8A,0x41,
        0x64,0x00,0x4E,0x56,0xFF,0xFA,0x01,0x08,
        0x2E,0x2E,0x00,0xB6,0xD0,0x68,0x3E,0x80,
        0x2F,0x0C,0xA9,0xFE,0x64,0x53,0x69,0x7A
    };
        byte[] pwdBytes = Encoding.ASCII.GetBytes(pwd ?? "");
        byte[] outBytes = new byte[32];
        int len = Math.Min(pwdBytes.Length, 32);
        Array.Copy(pwdBytes, outBytes, len);
        if (len < 32) Array.Copy(pad, 0, outBytes, len, 32 - len);
        return outBytes;
    }
    public static byte[] ComputeO(byte[] user, byte[] owner)
    {
        // owner = RC4(ownerpad, userpad)
        return RC4(owner, user);
    }
    public static byte[] ComputeEncryptionKey(byte[] userPad, byte[] O)
    {
        // key = MD5(userpad + O + P + id + 0s)
        // Para PDF básico, solo 5 bytes de MD5(userPad+O+P+4bytes+0)
        using (var md5 = MD5.Create())
        {
            byte[] P = BitConverter.GetBytes(-4);
            byte[] input = new byte[32 + 32 + 4];
            Array.Copy(userPad, 0, input, 0, 32);
            Array.Copy(O, 0, input, 32, 32);
            Array.Copy(P, 0, input, 64, 4);
            var hash = md5.ComputeHash(input);
            byte[] key = new byte[5]; // 40bits
            Array.Copy(hash, key, 5);
            return key;
        }
    }
    public static byte[] ComputeU(byte[] key)
    {
        // U = RC4(key, pad)
        byte[] pad = PadOrTruncatePassword("");
        return RC4(pad, key);
    }
    public static string ToHex(byte[] data)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in data)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
    public static byte[] RC4(byte[] data, byte[] key)
    {
        byte[] s = new byte[256];
        for (int i = 0; i < 256; i++) s[i] = (byte)i;
        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = j + s[i] + key[i % key.Length] & 0xFF;
            byte temp = s[i]; s[i] = s[j]; s[j] = temp;
        }
        byte[] output = new byte[data.Length];
        int iidx = 0, jidx = 0;
        for (int k = 0; k < data.Length; k++)
        {
            iidx = iidx + 1 & 0xFF;
            jidx = jidx + s[iidx] & 0xFF;
            byte temp = s[iidx]; s[iidx] = s[jidx]; s[jidx] = temp;
            output[k] = (byte)(data[k] ^ s[s[iidx] + s[jidx] & 0xFF]);
        }
        return output;
    }

    public static string BuildFormFieldWidget(int objNumber, string fieldName, float x, float y, float width, float height)
    {
        return $"{objNumber} 0 obj\n" +
               $"<< /Type /Annot /Subtype /Widget " +
               $"/Rect [{x} {y} {x + width} {y + height}] " +
               $"/FT /Tx /T ({fieldName}) /F 4 " +
               $"/V () /DA (/F1 12 Tf 0 g) " +
               $"/P {objNumber - 4} 0 R >>\nendobj\n";
    }

    public static string BuildFormFieldsArray(int[] fieldObjectNumbers)
    {
        var refs = string.Join(" ", fieldObjectNumbers.Select(n => $"{n} 0 R"));
        return $"{fieldObjectNumbers[0] + 1} 0 obj\n[{refs}]\nendobj\n";
    }

    public static string BuildAcroForm(int fieldsArrayObjectNumber)
    {
        return $"{fieldsArrayObjectNumber + 1} 0 obj\n" +
               $"<< /Fields {fieldsArrayObjectNumber} 0 R >>\nendobj\n";
    }

    // Attach
    public static string BuildEmbeddedFileObject(int objNumber, byte[] fileBytes)
    {
        return $"{objNumber} 0 obj\n" +
               $"<< /Type /EmbeddedFile /Length {fileBytes.Length} >>\n" +
               "stream\n" +
               Encoding.ASCII.GetString(fileBytes) + "\n" +
               "endstream\nendobj\n";
    }

    public static string BuildFileSpecObject(int objNumber, string fileName, int embeddedFileObjNumber)
    {
        return $"{objNumber} 0 obj\n" +
               $"<< /Type /Filespec /F ({fileName}) /EF <</F {embeddedFileObjNumber} 0 R>> >>\n" +
               "endobj\n";
    }

    public static string BuildNamesObject(int objNumber, Dictionary<string, int> fileSpecs)
    {
        var names = string.Join(" ", fileSpecs.Select(kv => $"({kv.Key}) {kv.Value} 0 R"));
        return $"{objNumber} 0 obj\n" +
               $"<< /Names [{names}] >>\n" +
               "endobj\n";
    }

    public static string BuildEmbeddedFilesObject(int objNumber, int namesObjNumber)
    {
        return $"{objNumber} 0 obj\n" +
               $"<< /EmbeddedFiles {namesObjNumber} 0 R >>\n" +
               "endobj\n";
    }

    public static string BuildFileAttachmentObject(int objNumber, string fileName, int fileSpecObjNumber)
    {
        return $"{objNumber} 0 obj\n" +
               "<< /Type /Annot /Subtype /FileAttachment\n" +
               "   /Rect [10 10 30 30]\n" +
               $"   /FS {fileSpecObjNumber} 0 R\n" +
               $"   /Contents ({fileName})\n" +
               "   /Name PushPin\n" +
               "   /T (Adjunto)\n" +
               ">>\n" +
               "endobj\n";
    }

    public static string UpdateCatalogWithNames(string catalog, int embeddedFilesObjNumber)
    {
        if (!catalog.Contains("/Names"))
        {
            int insertAt = catalog.IndexOf(">>");
            return catalog.Insert(insertAt, $" /Names {embeddedFilesObjNumber} 0 R");
        }
        return catalog;
    }

    public static string UpdatePageWithAnnotation(string page, int annotObjNumber)
    {
        if (!page.Contains("/Annots"))
        {
            int insert = page.LastIndexOf("endobj");
            return page.Insert(insert, $" /Annots [{annotObjNumber} 0 R]\n");
        }
        else
        {
            int annotsStart = page.IndexOf("/Annots [") + "/Annots [".Length;
            return page.Insert(annotsStart, $"{annotObjNumber} 0 R ");
        }
    }

    public static string BuildImageObject(int objNumber, byte[] imgBytes, int width, int height)
    {
        return $"{objNumber} 0 obj\n" +
               $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} " +
               $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode " +
               $"/Length {imgBytes.Length} >>\n" +
               "stream\n" +
               $"{Convert.ToBase64String(imgBytes)}\n" +
               "endstream\nendobj\n";
    }

    public static string BuildImageContentObject(int objNumber, int imgObjNumber, int displayWidth, int displayHeight, int posX, int posY)
    {
        string contentStream = $@"q
            {displayWidth} 0 0 {displayHeight} {posX} {posY} cm
            /Im1 Do
            Q";
        return $"{objNumber} 0 obj\n<< /Length {contentStream.Length} >>\nstream\n{contentStream}\nendstream\nendobj\n";
    }

    public static string UpdatePageWithImage(string pageObj, int imgObjNumber, int contentObjNumber, int originalContentObjNumber)
    {
        // Agrega la imagen a /Resources /XObject
        if (pageObj.Contains("/XObject"))
        {
            pageObj = Regex.Replace(
                pageObj,
                @"/XObject\s*<<",
                $"/XObject << /Im1 {imgObjNumber} 0 R ");
        }
        else
        {
            pageObj = Regex.Replace(
                pageObj,
                @"/Resources\s*<<",
                $"/Resources << /XObject <</Im1 {imgObjNumber} 0 R>> ");
        }

        // Actualiza /Contents para incluir ambos streams
        pageObj = Regex.Replace(
            pageObj,
            @"/Contents\s+\d+\s+0\s+R",
            $"/Contents [{originalContentObjNumber} 0 R {contentObjNumber} 0 R]");

        return pageObj;
    }
}