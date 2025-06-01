using PDFLibreria.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace PDFLibreria.Services;

public static class PDFCore
{
    private static readonly Encoding _pdfEncoding = Encoding.GetEncoding("ISO-8859-1");
    private const string PDF_HEADER = "%PDF-";
    private const string PDF_EOF = "%%EOF";

    private static string GetHeader(string pdfString)
    {
        int firstObjIndex = pdfString.IndexOf("obj");
        if (firstObjIndex > 0 && firstObjIndex < 30)
        {
            int endOfHeader = pdfString.LastIndexOf("%PDF-", firstObjIndex);
            if (endOfHeader != -1)
            {
                int newlineAfterHeader = pdfString.IndexOf("\n", endOfHeader);
                if (newlineAfterHeader != -1 && newlineAfterHeader < firstObjIndex)
                    return pdfString.Substring(0, newlineAfterHeader + 1);
            }
        }
        if (pdfString.StartsWith("%PDF-"))
        {
            int newline = pdfString.IndexOfAny(new char[] { '\r', '\n' });
            return pdfString.Substring(0, newline + (pdfString[newline] == '\r' && pdfString.Length > newline + 1 && pdfString[newline + 1] == '\n' ? 2 : 1));
        }
        return "%PDF-1.7\r\n%âãÏÓ\r\n";
    }

    public static string BuildXrefTable(List<KeyValuePair<int, long>> entries, int totalObjects)
    {
        var xrefBuilder = new StringBuilder();
        xrefBuilder.AppendLine("xref");

        if (!entries.Any())
        {
            xrefBuilder.AppendLine("0 1");
            xrefBuilder.AppendLine("0000000000 65535 f ");
            return xrefBuilder.ToString();
        }

        // Asegurar que tenemos una entrada para el objeto 0
        if (!entries.Any(e => e.Key == 0))
        {
            entries.Insert(0, new KeyValuePair<int, long>(0, 0));
        }

        // Ordenar entradas por número de objeto
        entries = entries.OrderBy(e => e.Key).ToList();

        // Escribir la tabla XRef
        xrefBuilder.AppendLine($"0 {totalObjects + 1}");
        xrefBuilder.AppendLine("0000000000 65535 f "); // Entrada para objeto 0

        var entryDict = entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        for (int i = 1; i <= totalObjects; i++)
        {
            if (entryDict.TryGetValue(i, out long offset))
            {
                xrefBuilder.AppendLine($"{offset:D10} 00000 n ");
            }
            else
            {
                xrefBuilder.AppendLine("0000000000 65535 f ");
            }
        }

        return xrefBuilder.ToString();
    }

    public static List<KeyValuePair<int, long>> BuildXrefEntries(string content, List<PDFObject> objects)
    {
        var xrefEntries = new List<KeyValuePair<int, long>>();
        foreach (var obj in objects.OrderBy(o => o.ObjectNumber))
        {
            string objPattern = $"{obj.ObjectNumber} {obj.GenerationNumber} obj";
            int objIndex = content.IndexOf(objPattern);
            if (objIndex >= 0)
            {
                long byteOffset = _pdfEncoding.GetByteCount(content.Substring(0, objIndex));
                xrefEntries.Add(new KeyValuePair<int, long>(obj.ObjectNumber, byteOffset));
            }
        }
        return xrefEntries;
    }

    public static string BuildTrailer(int numObjects, string rootRef, long startXrefByteOffsetValue)
    {
        var trailer = new StringBuilder();
        trailer.AppendLine("trailer");
        trailer.AppendLine("<<");
        trailer.AppendLine($"  /Size {numObjects + 1}"); // +1 para incluir el objeto 0
        trailer.AppendLine($"  /Root {rootRef}");
        trailer.AppendLine(">>");
        trailer.AppendLine("startxref");
        trailer.AppendLine(startXrefByteOffsetValue.ToString());
        trailer.Append("%%EOF"); // Removido el AppendLine para evitar espacios extra
        return trailer.ToString();
    }

    public static PDFDocument ParsePDFDocument(string pdfContent, Encoding encoding)
    {
        var doc = new PDFDocument();
        doc.Header = GetHeader(pdfContent);

        // Verificar si el PDF está encriptado
        if (pdfContent.Contains("/Encrypt") || pdfContent.Contains("/Encryption"))
        {
            Console.WriteLine("ADVERTENCIA: El PDF parece estar encriptado. Esto puede causar problemas al leer la estructura.");
        }

        // Extraer versión del PDF
        var versionMatch = Regex.Match(doc.Header, @"%PDF-(\d+\.\d+)");
        if (versionMatch.Success)
        {
            doc.Version = versionMatch.Groups[1].Value;
        }

        Console.WriteLine($"Parsing PDF version {doc.Version}");

        // Parse all objects
        var objectMatches = Regex.Matches(pdfContent, @"(\d+)\s+(\d+)\s+obj\s*(.*?)(?=\d+\s+\d+\s+obj|$)", RegexOptions.Singleline);
        Console.WriteLine($"Found {objectMatches.Count} objects in PDF");

        // Primero, buscar el trailer para encontrar el Root
        var trailerSectionMatch = Regex.Match(pdfContent, @"trailer\s*<<(.*?)>>", RegexOptions.Singleline);
        string trailerContent = "";
        if (trailerSectionMatch.Success)
        {
            Console.WriteLine("Found trailer section");
            trailerContent = trailerSectionMatch.Groups[1].Value;

            // Buscar Root en el trailer
            var rootInTrailerMatch = Regex.Match(trailerContent, @"/Root\s*(\d+)\s+(\d+)\s+R");
            if (rootInTrailerMatch.Success)
            {
                int rootNum = int.Parse(rootInTrailerMatch.Groups[1].Value);
                Console.WriteLine($"Found Root reference in trailer: {rootNum}");
            }
        }

        foreach (Match match in objectMatches)
        {
            var obj = new PDFObject
            {
                ObjectNumber = int.Parse(match.Groups[1].Value),
                GenerationNumber = int.Parse(match.Groups[2].Value),
                Content = match.Groups[3].Value.Trim(),
                Offset = encoding.GetByteCount(pdfContent.Substring(0, match.Index))
            };

            // Parse dictionary first
            var dictMatch = Regex.Match(obj.Content, @"<<(.*?)>>", RegexOptions.Singleline);
            if (dictMatch.Success)
            {
                ParseDictionary(dictMatch.Groups[1].Value, obj);
            }

            // Check if it's a stream
            if (obj.Dictionary.ContainsKey("/Length"))
            {
                obj.IsStream = true;
                // Extraer el contenido del stream
                var streamStart = obj.Content.IndexOf("stream", StringComparison.OrdinalIgnoreCase);
                if (streamStart >= 0)
                {
                    streamStart += "stream".Length;
                    // Buscar el final del stream, considerando diferentes tipos de saltos de línea
                    var streamEnd = obj.Content.IndexOf("endstream", streamStart, StringComparison.OrdinalIgnoreCase);
                    if (streamEnd >= 0)
                    {
                        // Extraer el contenido del stream, incluyendo el salto de línea después de "stream"
                        var streamContent = obj.Content.Substring(streamStart).TrimStart('\r', '\n');
                        streamContent = streamContent.Substring(0, streamContent.Length - "endstream".Length).TrimEnd('\r', '\n');
                        obj.Content = streamContent;
                    }
                }
            }

            // Determinar el tipo de objeto
            if (obj.Dictionary.ContainsKey("/Type"))
            {
                obj.Type = obj.Dictionary["/Type"].TrimStart('/');
                Console.WriteLine($"Object {obj.ObjectNumber} is of type {obj.Type}");

                // Mostrar más información para objetos importantes
                if (obj.Type == "Catalog" || obj.Type == "Pages" || obj.Type == "Page")
                {
                    Console.WriteLine($"  Content preview: {obj.Content.Substring(0, Math.Min(100, obj.Content.Length))}...");
                    foreach (var entry in obj.Dictionary)
                    {
                        Console.WriteLine($"  Dictionary entry: /{entry.Key} = {entry.Value}");
                    }
                }
            }

            // Find references
            var refMatches = Regex.Matches(obj.Content, @"(\d+)\s+(\d+)\s+R");
            foreach (Match refMatch in refMatches)
            {
                obj.References.Add(int.Parse(refMatch.Groups[1].Value));
            }

            doc.Objects.Add(obj);
        }

        // Find Root object (Catalog) - múltiples métodos mejorados
        Console.WriteLine("\nBuscando objeto Root (Catalog)...");

        // Método 1: Buscar por tipo exacto
        doc.Root = doc.Objects.FirstOrDefault(o =>
            o.Type == "Catalog" ||
            o.Dictionary.ContainsKey("/Type") && o.Dictionary["/Type"].TrimStart('/') == "Catalog");

        if (doc.Root != null)
        {
            Console.WriteLine($"Found Root object {doc.Root.ObjectNumber} by exact type match");
        }
        else
        {
            Console.WriteLine("No Root found by exact type, trying alternative methods...");

            // Método 2: Buscar por contenido específico
            doc.Root = doc.Objects.FirstOrDefault(o =>
                o.Content.Contains("/Type /Catalog") ||
                o.Content.Contains("/Type/Catalog") ||
                o.Content.Contains("/Type  /Catalog") ||
                o.Content.Contains("/Type\t/Catalog"));

            if (doc.Root != null)
            {
                Console.WriteLine($"Found Root object {doc.Root.ObjectNumber} by content search");
            }
            else if (!string.IsNullOrEmpty(trailerContent))
            {
                // Método 3: Usar el trailer ya encontrado
                Console.WriteLine("Using previously found trailer section to search for Root reference...");
                var rootRefMatch = Regex.Match(trailerContent, @"/Root\s*(\d+)\s+(\d+)\s+R");
                if (rootRefMatch.Success)
                {
                    int rootNum = int.Parse(rootRefMatch.Groups[1].Value);
                    Console.WriteLine($"Found Root reference in trailer: {rootNum}");
                    doc.Root = doc.Objects.FirstOrDefault(o => o.ObjectNumber == rootNum);
                    if (doc.Root != null)
                    {
                        Console.WriteLine($"Found Root object {rootNum} from trailer reference");
                    }
                }
            }

            // Método 4: Buscar por estructura de diccionario
            if (doc.Root == null)
            {
                Console.WriteLine("Trying to find Root by dictionary structure...");
                foreach (var obj in doc.Objects)
                {
                    // Un objeto Catalog típicamente tiene estas entradas
                    if (obj.Dictionary.ContainsKey("/Pages") &&
                        (obj.Dictionary.ContainsKey("/Type") || obj.Content.Contains("/Type")))
                    {
                        doc.Root = obj;
                        Console.WriteLine($"Found potential Root object {obj.ObjectNumber} by dictionary structure");
                        break;
                    }
                }
            }
        }

        // Verificación final del Root
        if (doc.Root == null)
        {
            Console.WriteLine("\nERROR: No se pudo encontrar el objeto Root (Catalog)");
            Console.WriteLine("Contenido del PDF para análisis:");
            Console.WriteLine(pdfContent.Substring(0, Math.Min(1000, pdfContent.Length)) + "...");

            // Mostrar todos los objetos que podrían ser Root
            Console.WriteLine("\nObjetos que podrían ser Root:");
            foreach (var obj in doc.Objects)
            {
                if (obj.Dictionary.ContainsKey("/Pages") ||
                    obj.Content.Contains("/Pages") ||
                    obj.Content.Contains("/Type") ||
                    obj.Dictionary.ContainsKey("/Type"))
                {
                    Console.WriteLine($"\nObject {obj.ObjectNumber}:");
                    Console.WriteLine($"Type: {obj.Type}");
                    Console.WriteLine($"Dictionary entries: {string.Join(", ", obj.Dictionary.Keys)}");
                    Console.WriteLine($"Content preview: {obj.Content.Substring(0, Math.Min(100, obj.Content.Length))}...");
                }
            }
        }
        else
        {
            Console.WriteLine($"\nRoot object {doc.Root.ObjectNumber} found successfully");
            Console.WriteLine($"Type: {doc.Root.Type}");
            Console.WriteLine($"Dictionary entries: {string.Join(", ", doc.Root.Dictionary.Keys)}");
        }

        // Find Pages object - múltiples métodos
        if (doc.Root != null)
        {
            // Método 1: Buscar en el diccionario del Root
            if (doc.Root.Dictionary.ContainsKey("/Pages"))
            {
                var pagesRef = doc.Root.Dictionary["/Pages"];
                var pagesMatch = Regex.Match(pagesRef, @"(\d+)\s+(\d+)\s+R");
                if (pagesMatch.Success)
                {
                    int pagesNum = int.Parse(pagesMatch.Groups[1].Value);
                    doc.Pages = doc.Objects.FirstOrDefault(o => o.ObjectNumber == pagesNum);
                    if (doc.Pages != null)
                    {
                        Console.WriteLine($"Found Pages object {pagesNum} from Root dictionary");
                    }
                }
            }

            // Método 2: Buscar en el contenido del Root
            if (doc.Pages == null)
            {
                var rootPagesMatch = Regex.Match(doc.Root.Content, @"/Pages\s*(\d+)\s+(\d+)\s+R");
                if (rootPagesMatch.Success)
                {
                    int pagesNum = int.Parse(rootPagesMatch.Groups[1].Value);
                    doc.Pages = doc.Objects.FirstOrDefault(o => o.ObjectNumber == pagesNum);
                    if (doc.Pages != null)
                    {
                        Console.WriteLine($"Found Pages object {pagesNum} from Root content");
                    }
                }
            }
        }

        // Método 3: Buscar por tipo
        if (doc.Pages == null)
        {
            doc.Pages = doc.Objects.FirstOrDefault(o => o.Type == "Pages");
            if (doc.Pages != null)
            {
                Console.WriteLine($"Found Pages object {doc.Pages.ObjectNumber} by type search");
            }
        }

        // Método 4: Buscar por contenido
        if (doc.Pages == null)
        {
            doc.Pages = doc.Objects.FirstOrDefault(o =>
                o.Content.Contains("/Type /Pages") ||
                o.Content.Contains("/Type/Pages"));
            if (doc.Pages != null)
            {
                Console.WriteLine($"Found Pages object {doc.Pages.ObjectNumber} by content search");
            }
        }

        // Find all page objects - múltiples métodos mejorados
        if (doc.Pages != null)
        {
            Console.WriteLine("\nBuscando objetos Page...");

            // Método 1: Buscar en /Kids
            if (doc.Pages.Dictionary.ContainsKey("/Kids"))
            {
                var kidsRef = doc.Pages.Dictionary["/Kids"];
                Console.WriteLine($"Found /Kids reference: {kidsRef}");
                var kidMatches = Regex.Matches(kidsRef, @"(\d+)\s+(\d+)\s+R");
                foreach (Match kidMatch in kidMatches)
                {
                    var pageObj = doc.Objects.FirstOrDefault(o => o.ObjectNumber == int.Parse(kidMatch.Groups[1].Value));
                    if (pageObj != null)
                    {
                        doc.PageObjects.Add(pageObj);
                        Console.WriteLine($"Found Page object {pageObj.ObjectNumber} from /Kids");
                    }
                }
            }

            // Método 2: Buscar en el contenido de Pages
            if (doc.PageObjects.Count == 0)
            {
                var kidsContentMatch = Regex.Match(doc.Pages.Content, @"/Kids\s*\[(.*?)\]", RegexOptions.Singleline);
                if (kidsContentMatch.Success)
                {
                    var kidsContent = kidsContentMatch.Groups[1].Value;
                    var kidMatches = Regex.Matches(kidsContent, @"(\d+)\s+(\d+)\s+R");
                    foreach (Match kidMatch in kidMatches)
                    {
                        var pageObj = doc.Objects.FirstOrDefault(o => o.ObjectNumber == int.Parse(kidMatch.Groups[1].Value));
                        if (pageObj != null)
                        {
                            doc.PageObjects.Add(pageObj);
                            Console.WriteLine($"Found Page object {pageObj.ObjectNumber} from Pages content");
                        }
                    }
                }
            }
        }

        // Método 3: Buscar objetos Page directamente
        if (doc.PageObjects.Count == 0)
        {
            Console.WriteLine("Searching for Page objects directly...");
            var pageObjects = doc.Objects.Where(o =>
                o.Type == "Page" ||
                o.Content.Contains("/Type /Page") ||
                o.Content.Contains("/Type/Page") ||
                o.Dictionary.ContainsKey("/Type") && o.Dictionary["/Type"].TrimStart('/') == "Page").ToList();

            foreach (var pageObj in pageObjects)
            {
                if (!doc.PageObjects.Any(p => p.ObjectNumber == pageObj.ObjectNumber))
                {
                    doc.PageObjects.Add(pageObj);
                    Console.WriteLine($"Found Page object {pageObj.ObjectNumber} by direct search");
                }
            }
        }

        // Verificación final de páginas
        if (doc.PageObjects.Count == 0)
        {
            Console.WriteLine("\nERROR: No se encontraron objetos Page");
            if (doc.Pages != null)
            {
                Console.WriteLine("Contenido del objeto Pages para análisis:");
                Console.WriteLine(doc.Pages.Content);
            }
        }
        else
        {
            Console.WriteLine($"\nSe encontraron {doc.PageObjects.Count} objetos Page");
            foreach (var page in doc.PageObjects)
            {
                Console.WriteLine($"Page {page.ObjectNumber}: Type={page.Type}, References={string.Join(", ", page.References)}");
            }
        }

        return doc;
    }

    public static void ParseDictionary(string dictContent, PDFObject obj)
    {
        var entries = Regex.Matches(dictContent, @"/(\w+)\s+([^/]+?)(?=/\w+|$)");
        foreach (Match entry in entries)
        {
            obj.Dictionary[entry.Groups[1].Value] = entry.Groups[2].Value.Trim();
        }
    }

    public static string RebuildObject(PDFObject obj)
    {
        var sb = new StringBuilder();

        // Write object header
        sb.Append($"{obj.ObjectNumber} {obj.GenerationNumber} obj\r\n");

        // Write dictionary
        if (obj.Dictionary.Any())
        {
            sb.Append("<<\r\n");
            foreach (var entry in obj.Dictionary)
            {
                sb.Append($"/{entry.Key} {entry.Value}\r\n");
            }
            sb.Append(">>\r\n");
        }

        // Write content
        if (obj.IsStream)
        {
            sb.Append("stream\r\n");
            if (obj.RawContent != null)
            {
                // For streams, we'll handle the content separately in SaveDocument
                sb.Append("[STREAM_CONTENT]");
            }
            else if (!string.IsNullOrEmpty(obj.Content))
            {
                sb.Append(obj.Content);
            }
            sb.Append("\r\nendstream\r\n");
        }
        else if (!string.IsNullOrEmpty(obj.Content))
        {
            sb.Append(obj.Content);
            if (!obj.Content.EndsWith("\r\n"))
            {
                sb.Append("\r\n");
            }
        }

        sb.Append("endobj\r\n");
        return sb.ToString();
    }

    public static void UpdateObjectReferences(PDFObject obj, Dictionary<int, int> objectNumberMap)
    {
        // Actualizar referencias en el diccionario
        foreach (var key in obj.Dictionary.Keys.ToList())
        {
            var value = obj.Dictionary[key];
            var refMatches = Regex.Matches(value, @"(\d+)\s+(\d+)\s+R");
            foreach (Match refMatch in refMatches)
            {
                int oldRef = int.Parse(refMatch.Groups[1].Value);
                if (objectNumberMap.TryGetValue(oldRef, out int newRef))
                {
                    obj.Dictionary[key] = value.Replace(
                        $"{oldRef} {refMatch.Groups[2].Value} R",
                        $"{newRef} {refMatch.Groups[2].Value} R"
                    );
                }
            }
        }

        // Actualizar referencias en el contenido
        if (!obj.IsStream)
        {
            var contentRefMatches = Regex.Matches(obj.Content, @"(\d+)\s+(\d+)\s+R");
            foreach (Match refMatch in contentRefMatches)
            {
                int oldRef = int.Parse(refMatch.Groups[1].Value);
                if (objectNumberMap.TryGetValue(oldRef, out int newRef))
                {
                    obj.Content = obj.Content.Replace(
                        $"{oldRef} {refMatch.Groups[2].Value} R",
                        $"{newRef} {refMatch.Groups[2].Value} R"
                    );
                }
            }
        }
    }

    // Escapa paréntesis y backslashes para el texto PDF
    public static string EscapePdfText(string text)
    {
        return text.Replace(@"\", @"\\").Replace("(", @"\(").Replace(")", @"\)");
    }

    private static bool StartsWithPDFHeader(byte[] content)
    {
        if (content == null || content.Length < 5)
            return false;

        // Verificar "%PDF-"
        return content[0] == 0x25 && // %
               content[1] == 0x50 && // P
               content[2] == 0x44 && // D
               content[3] == 0x46 && // F
               content[4] == 0x2D;   // -
    }

    private static bool EndsWithEOF(byte[] content)
    {
        if (content == null || content.Length < 5)
            return false;

        // Verificar "%%EOF" al final
        int eofLength = 5;
        return content[content.Length - 5] == 0x25 && // %
               content[content.Length - 4] == 0x25 && // %
               content[content.Length - 3] == 0x45 && // E
               content[content.Length - 2] == 0x4F && // O
               content[content.Length - 1] == 0x46;   // F
    }

    public static void SaveDocument(PDFDocument doc, string outputPath)
    {
        try
        {
            Console.WriteLine("Starting PDF document save process...");
            
            using (var writer = new StreamWriter(outputPath, false, _pdfEncoding))
            {
                // 1. Write PDF Header
                writer.Write(doc.Header);
                writer.WriteLine();

                // 2. Write all objects
                var objectOffsets = new Dictionary<int, long>();
                foreach (var obj in doc.Objects.OrderBy(o => o.ObjectNumber))
                {
                    // Record offset before writing object
                    objectOffsets[obj.ObjectNumber] = writer.BaseStream.Position;
                    
                    // Write object number and generation
                    writer.Write($"{obj.ObjectNumber} {obj.GenerationNumber} obj\r\n");
                    
                    // Write dictionary if exists
                    if (obj.Dictionary.Any())
                    {
                        writer.Write("<<\r\n");
                        foreach (var entry in obj.Dictionary)
                        {
                            writer.Write($"/{entry.Key} {entry.Value}\r\n");
                        }
                        writer.Write(">>\r\n");
                    }

                    // Write stream content if it's a stream
                    if (obj.IsStream)
                    {
                        writer.Write("stream\r\n");
                        
                        if (obj.RawContent != null)
                        {
                            // Write raw content directly
                            writer.Flush(); // Ensure dictionary is written
                            writer.BaseStream.Write(obj.RawContent, 0, obj.RawContent.Length);
                        }
                        else if (!string.IsNullOrEmpty(obj.Content))
                        {
                            writer.Write(obj.Content);
                        }
                        
                        writer.Write("\r\nendstream\r\n");
                    }
                    else if (!string.IsNullOrEmpty(obj.Content))
                    {
                        writer.Write(obj.Content);
                        if (!obj.Content.EndsWith("\r\n"))
                        {
                            writer.Write("\r\n");
                        }
                    }

                    writer.Write("endobj\r\n");
                    writer.Flush();
                }

                // 3. Write xref table
                var startXref = writer.BaseStream.Position;
                writer.Write("xref\r\n");
                writer.Write($"0 {doc.Objects.Count + 1}\r\n");
                writer.Write("0000000000 65535 f \r\n"); // Free entry for object 0

                // Write xref entries
                foreach (var obj in doc.Objects.OrderBy(o => o.ObjectNumber))
                {
                    if (objectOffsets.TryGetValue(obj.ObjectNumber, out long offset))
                    {
                        writer.Write($"{offset:D10} 00000 n \r\n");
                    }
                    else
                    {
                        writer.Write("0000000000 65535 f \r\n");
                    }
                }

                // 4. Write trailer
                writer.Write("trailer\r\n");
                writer.Write("<<\r\n");
                writer.Write($"/Size {doc.Objects.Count + 1}\r\n");
                writer.Write($"/Root {doc.Root.ObjectNumber} {doc.Root.GenerationNumber} R\r\n");
                writer.Write(">>\r\n");
                writer.Write("startxref\r\n");
                writer.Write($"{startXref}\r\n");
                writer.Write("%%EOF");

                writer.Flush();
            }

            // Verify the file was written correctly
            var fileInfo = new FileInfo(outputPath);
            if (fileInfo.Length == 0)
            {
                throw new Exception("Generated PDF file is empty");
            }

            Console.WriteLine($"PDF file written successfully: {fileInfo.Length} bytes");

            // Verify PDF structure
            var content = File.ReadAllBytes(outputPath);
            if (!StartsWithPDFHeader(content))
            {
                throw new Exception("Generated file does not start with PDF header");
            }
            if (!EndsWithEOF(content))
            {
                throw new Exception("Generated file does not end with PDF EOF marker");
            }

            Console.WriteLine("PDF structure verification passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving PDF: {ex.Message}");
            if (File.Exists(outputPath))
            {
                try { File.Delete(outputPath); }
                catch { }
            }
            throw;
        }
    }
}
