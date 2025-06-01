using System.Text;
using PDFDanNativo.Models;

namespace PDFDanNativo;

public interface IPDFCore
{
    // 1. CABECERA
    string BuildPdfHeader(PDFConfig config);

    // 2. CUERPO
    string[] BuildPdfBodyObjects(string texto, PDFConfig config);

    // 3. TABLA DE REFERENCIA CRUZADA (xref)
    int[] CalculateOffsets(string[] contentParts);

    string BuildXrefTable(string[] contentParts, int[] offsets);

    // 4. TRAILER
    string BuildTrailer(int numObjects, int startxref, PDFConfig config);

    void WritePdfFile(string filePath, string header, string[] bodyObjects, string xref, string trailer);

    // Varios
    bool IsValidPdf(string filePath);

    int GetPageCount(string filePath);

    List<string> GetPdfFormFieldNames(string filePath);

    // Métodos para fusión de PDFs
    string[] ExtractPdfObjects(string pdfContent, int numObjects);
    string[] MergePdfObjects(string[] objects1, string[] objects2);
}

public class PDFCore : IPDFCore
{
    private static readonly Dictionary<PageSize, (float width, float height)> PageDimensions = new()
    {
        { PageSize.A4, (595.28f, 841.89f) },      // 210mm x 297mm
        { PageSize.Letter, (612f, 792f) },        // 8.5" x 11"
        { PageSize.Legal, (612f, 1008f) },        // 8.5" x 14"
        { PageSize.A3, (841.89f, 1190.55f) }      // 297mm x 420mm
    };

    public string BuildPdfHeader(PDFConfig config)
    {
        var header = new StringBuilder();
        header.AppendLine("%PDF-1.4");
        
        // Agregar metadatos si están presentes
        if (!string.IsNullOrEmpty(config.Metadata.Title) ||
            !string.IsNullOrEmpty(config.Metadata.Author) ||
            !string.IsNullOrEmpty(config.Metadata.Subject) ||
            !string.IsNullOrEmpty(config.Metadata.Keywords))
        {
            header.AppendLine("%%PDFDanNativo Metadata");
            if (!string.IsNullOrEmpty(config.Metadata.Title))
                header.AppendLine($"%%Title: {config.Metadata.Title}");
            if (!string.IsNullOrEmpty(config.Metadata.Author))
                header.AppendLine($"%%Author: {config.Metadata.Author}");
            if (!string.IsNullOrEmpty(config.Metadata.Subject))
                header.AppendLine($"%%Subject: {config.Metadata.Subject}");
            if (!string.IsNullOrEmpty(config.Metadata.Keywords))
                header.AppendLine($"%%Keywords: {config.Metadata.Keywords}");
        }

        return header.ToString();
    }

    public string[] BuildPdfBodyObjects(string texto, PDFConfig config)
    {
        // Obtener dimensiones de página según configuración
        var (width, height) = PageDimensions[config.PageSize];
        if (config.Orientation == PageOrientation.Landscape)
            (width, height) = (height, width);

        // Ajustar dimensiones según márgenes
        width -= (config.Margins.Left + config.Margins.Right);
        height -= (config.Margins.Top + config.Margins.Bottom);

        // (objetos PDF: catálogo, páginas, página, contenido, fuente)
        string obj1 = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
        string obj2 = "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n";
        
        // Página con dimensiones y márgenes configurados
        string obj3 = $"3 0 obj\n<< /Type /Page /Parent 2 0 R " +
                     $"/MediaBox [0 0 {width} {height}] " +
                     $"/Contents 4 0 R " +
                     $"/Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n";

        // Escape de paréntesis y caracteres especiales
        string textEscaped = texto.Replace("(", "\\(")
                                .Replace(")", "\\)")
                                .Replace("\\", "\\\\")
                                .Replace("\r", "\\r")
                                .Replace("\n", "\\n");

        // Posición inicial del texto considerando márgenes
        float startX = config.Margins.Left;
        float startY = height - config.Margins.Top; // PDF usa coordenadas desde abajo

        // Flujo de contenido con formato personalizado
        string obj4_stream = $"BT\n{startX} {startY} TD\n" +
                           $"/F1 {config.FontSize} Tf\n" +
                           $"({textEscaped}) Tj\nET";
        
        string obj4 = $"4 0 obj\n<< /Length {obj4_stream.Length} >>\nstream\n{obj4_stream}\nendstream\nendobj\n";
        
        // Fuente configurada
        string obj5 = $"5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /{config.FontName} >>\nendobj\n";

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

    public string BuildTrailer(int numObjects, int startxref, PDFConfig config)
    {
        var trailer = new StringBuilder();
        trailer.AppendLine("trailer");
        trailer.AppendLine("<<");
        trailer.AppendLine($"  /Size {numObjects}");
        trailer.AppendLine("  /Root 1 0 R");

        // Agregar metadatos al trailer si están presentes
        if (!string.IsNullOrEmpty(config.Metadata.Title) ||
            !string.IsNullOrEmpty(config.Metadata.Author) ||
            !string.IsNullOrEmpty(config.Metadata.Subject) ||
            !string.IsNullOrEmpty(config.Metadata.Keywords))
        {
            trailer.AppendLine("  /Info <<");
            if (!string.IsNullOrEmpty(config.Metadata.Title))
                trailer.AppendLine($"    /Title ({config.Metadata.Title})");
            if (!string.IsNullOrEmpty(config.Metadata.Author))
                trailer.AppendLine($"    /Author ({config.Metadata.Author})");
            if (!string.IsNullOrEmpty(config.Metadata.Subject))
                trailer.AppendLine($"    /Subject ({config.Metadata.Subject})");
            if (!string.IsNullOrEmpty(config.Metadata.Keywords))
                trailer.AppendLine($"    /Keywords ({config.Metadata.Keywords})");
            trailer.AppendLine($"    /Creator ({config.Metadata.Creator})");
            trailer.AppendLine($"    /CreationDate (D:{config.Metadata.CreationDate:yyyyMMddHHmmss})");
            trailer.AppendLine("  >>");
        }

        trailer.AppendLine(">>");
        trailer.AppendLine($"startxref\n{startxref}\n%%EOF");
        return trailer.ToString();
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

    public string[] ExtractPdfObjects(string pdfContent, int numObjects)
    {
        var objs = new string[numObjects];
        int searchIdx = 0;
        for (int i = 1; i <= numObjects; i++)
        {
            string objTag = $"{i} 0 obj";
            int start = pdfContent.IndexOf(objTag, searchIdx, StringComparison.Ordinal);
            if (start == -1)
                throw new InvalidOperationException($"No se encontró el objeto {i} en el PDF");
                
            int end = pdfContent.IndexOf("endobj", start, StringComparison.Ordinal) + "endobj\n".Length;
            objs[i - 1] = pdfContent.Substring(start, end - start);
            searchIdx = end;
        }
        return objs;
    }

    public string[] MergePdfObjects(string[] objects1, string[] objects2)
    {
        // Aseguramos salto de línea al final de cada objeto
        for (int i = 0; i < objects1.Length; i++)
            if (!objects1[i].EndsWith("\n")) objects1[i] += "\n";
        for (int i = 0; i < objects2.Length; i++)
            if (!objects2[i].EndsWith("\n")) objects2[i] += "\n";

        // Objetos nuevos
        StringBuilder[] objects = new StringBuilder[8];

        // 1 0 obj: Catalog
        objects[0] = new StringBuilder("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // 2 0 obj: Pages con 2 hijos
        objects[1] = new StringBuilder("2 0 obj\n<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>\nendobj\n");

        // 3 0 obj: Page 1 (ajusta /Parent, /Contents y /Font)
        objects[2] = new StringBuilder(objects1[2]
            .Replace("3 0 obj", "3 0 obj")
            .Replace("/Parent 2 0 R", "/Parent 2 0 R")
            .Replace("/Contents 4 0 R", "/Contents 4 0 R")
            .Replace("/F1 5 0 R", "/F1 5 0 R")
        );

        // 4 0 obj: Contents 1
        objects[3] = new StringBuilder(objects1[3].Replace("4 0 obj", "4 0 obj"));

        // 5 0 obj: Font 1
        objects[4] = new StringBuilder(objects1[4].Replace("5 0 obj", "5 0 obj"));

        // 6 0 obj: Page 2 (ajusta referencias a los objetos correctos)
        objects[5] = new StringBuilder(objects2[2]
            .Replace("3 0 obj", "6 0 obj")
            .Replace("/Parent 2 0 R", "/Parent 2 0 R")
            .Replace("/Contents 4 0 R", "/Contents 7 0 R")
            .Replace("/F1 5 0 R", "/F1 8 0 R")
        );

        // 7 0 obj: Contents 2
        objects[6] = new StringBuilder(objects2[3].Replace("4 0 obj", "7 0 obj"));

        // 8 0 obj: Font 2
        objects[7] = new StringBuilder(objects2[4].Replace("5 0 obj", "8 0 obj"));

        return objects.Select(sb => sb.ToString()).ToArray();
    }
}
