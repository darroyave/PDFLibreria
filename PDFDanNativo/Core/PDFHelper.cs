using System.Security.Cryptography;
using System.Text;

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
}