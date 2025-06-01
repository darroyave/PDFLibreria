using System.Text;

namespace PDFDanNativo;

public interface IPDFCore
{
    // 1. CABECERA
    string BuildPdfHeader();

    // 2. CUERPO
    string[] BuildPdfBodyObjects(string texto);

    // 3. TABLA DE REFERENCIA CRUZADA (xref)
    int[] CalculateOffsets(string[] contentParts);

    string BuildXrefTable(string[] contentParts, int[] offsets);

    // 4. TRAILER
    string BuildTrailer(int numObjects, int startxref);

    void WritePdfFile(string filePath, string header, string[] bodyObjects, string xref, string trailer);

    bool IsValidPdf(string filePath);

    int GetPageCount(string filePath);

    List<string> GetPdfFormFieldNames(string filePath);
}

public class PDFCore: IPDFCore
{
    public string BuildPdfHeader()
    {
        return "%PDF-1.4\n";
    }

    public string[] BuildPdfBodyObjects(string texto)
    {
        // (objetos PDF: catálogo, páginas, página, contenido, fuente)
        string obj1 = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
        string obj2 = "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n";
        string obj3 = "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n";

        // Escape de paréntesis
        string textEscaped = texto.Replace("(", "\\(").Replace(")", "\\)");

        //  flujo de contenido de texto
        string obj4_stream = $"BT\n70 100 TD\n/F1 24 Tf\n({textEscaped}) Tj\nET";
        string obj4 = $"4 0 obj\n<< /Length {obj4_stream.Length} >>\nstream\n{obj4_stream}\nendstream\nendobj\n";
        string obj5 = "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n";

        // Junta el cuerpo (Body)
        return [obj1, obj2, obj3, obj4, obj5];
    }

    public int[] CalculateOffsets(string[] contentParts)
    {
        // Primero, obtenemos todos los offsets desde el principio del archivo
        int[] offsets = new int[contentParts.Length];
        int offset = 0;
        for (int i = 0; i < contentParts.Length; i++)
        {
            offsets[i] = offset;
            offset += Encoding.ASCII.GetByteCount(contentParts[i]);
        }
        return offsets;
    }

    public string BuildXrefTable(string[] contentParts, int[] offsets)
    {
        StringBuilder xref = new();
        xref.AppendLine("xref");
        xref.AppendLine($"0 {contentParts.Length}");
        xref.AppendLine("0000000000 65535 f ");
        for (int i = 1; i < contentParts.Length; i++)
            xref.AppendLine($"{offsets[i]:D10} 00000 n ");
        return xref.ToString();
    }

    public string BuildTrailer(int numObjects, int startxref)
    {
        return $"trailer\n<< /Size {numObjects} /Root 1 0 R >>\nstartxref\n{startxref}\n%%EOF\n";
    }

    public void WritePdfFile(string filePath, string header, string[] bodyObjects, string xref, string trailer)
    {
        using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write);

        using StreamWriter sw = new(fs, Encoding.ASCII);

        sw.Write(header);

        foreach (var obj in bodyObjects)
            sw.Write(obj);

        sw.Write(xref);

        sw.Write(trailer);
    }

    public bool IsValidPdf(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        byte[] header = new byte[5];
        try
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);

            // Leer los primeros 5 bytes
            if (fs.Read(header, 0, 5) != 5)
                return false;

            string headerStr = Encoding.ASCII.GetString(header);
            if (!headerStr.StartsWith("%PDF-"))
                return false;

            // Leer los últimos 20 bytes para buscar %%EOF
            fs.Seek(-20, SeekOrigin.End);
            byte[] tail = new byte[20];
            fs.ReadExactly(tail, 0, 20);
            string tailStr = Encoding.ASCII.GetString(tail);

            return tailStr.Contains("%%EOF");
        }
        catch
        {
            return false;
        }
    }

    public int GetPageCount(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("El archivo PDF no existe.", filePath);

        string contenido;
        using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read))
        using (StreamReader sr = new(fs, Encoding.ASCII))
        {
            contenido = sr.ReadToEnd();
        }

        var idx = contenido.IndexOf("/Count");
        if (idx == -1)
            return -1; // No se encontró

        idx += "/Count".Length;
        while (idx < contenido.Length && (contenido[idx] == ' ' || contenido[idx] == '\t'))
            idx++;

        StringBuilder sb = new StringBuilder();
        while (idx < contenido.Length && char.IsDigit(contenido[idx]))
        {
            sb.Append(contenido[idx]);
            idx++;
        }

        if (int.TryParse(sb.ToString(), out int count))
            return count;

        return -1; // No se pudo extraer el número
    }

    public List<string> GetPdfFormFieldNames(string filePath)
    {
        var fieldNames = new List<string>();
        var lines = File.ReadAllLines(filePath, Encoding.ASCII);

        foreach (var line in lines)
        {
            int idx = line.IndexOf("/T (");
            if (idx >= 0)
            {
                int start = idx + 4; // después de "/T ("
                int end = line.IndexOf(")", start);
                if (end > start)
                {
                    string name = line.Substring(start, end - start);
                    fieldNames.Add(name);
                }
            }
        }
        return fieldNames;
    }
}
