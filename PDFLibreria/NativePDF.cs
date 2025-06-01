using System.Text;
using System.Text.RegularExpressions; // For Regex

namespace PDFLibreria;

// Añadir método de extensión para arrays de bytes
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

public class PDFObject
{
    public int ObjectNumber { get; set; }
    public int GenerationNumber { get; set; }
    public string Content { get; set; }
    public bool IsStream { get; set; }
    public Dictionary<string, string> Dictionary { get; set; }
    public long Offset { get; set; }
    public List<int> References { get; set; }
    public string Type { get; set; }

    public PDFObject()
    {
        Dictionary = new Dictionary<string, string>();
        References = new List<int>();
        Type = "";
    }
}

public class PDFDocument
{
    public string Header { get; set; }
    public List<PDFObject> Objects { get; set; }
    public PDFObject Root { get; set; }
    public PDFObject Pages { get; set; }
    public List<PDFObject> PageObjects { get; set; }
    public string Version { get; set; }

    public PDFDocument()
    {
        Objects = new List<PDFObject>();
        PageObjects = new List<PDFObject>();
        Version = "";
    }
}

public class NativePDF
{
    // ReadPdfBytes, FindObjects, FindXref, FindTrailer methods from previous steps
    // These are kept for continuity but new Merge logic has its own parsing.
    private byte[]? ReadPdfBytes(string filePath)
    {
        try
        {
            return File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
            return null;
        }
    }

    public void MergePDF(string pdfPath1, string pdfPath2, string outputPath)
    {
        Console.WriteLine($"Attempting to merge {pdfPath1} and {pdfPath2} into {outputPath}");

        try
        {
            byte[] pdf1Bytes = ReadPdfBytes(pdfPath1);
            byte[] pdf2Bytes = ReadPdfBytes(pdfPath2);

            if (pdf1Bytes == null || pdf2Bytes == null)
            {
                throw new Exception("Error reading one or both PDF files.");
            }

            Encoding pdfEncoding = Encoding.GetEncoding("ISO-8859-1");

            // Parse both PDFs into document structures
            PDFDocument doc1 = ParsePDFDocument(pdfEncoding.GetString(pdf1Bytes), pdfEncoding);
            PDFDocument doc2 = ParsePDFDocument(pdfEncoding.GetString(pdf2Bytes), pdfEncoding);

            if (doc1.Pages == null || doc2.Pages == null)
            {
                throw new Exception("One or both PDFs are missing required page structure.");
            }

            // Validar la estructura básica
            if (doc1.Root == null || doc2.Root == null)
            {
                throw new Exception("One or both PDFs are missing Root object.");
            }

            // Calculate new object numbers for PDF2
            int maxObjNumPdf1 = doc1.Objects.Max(o => o.ObjectNumber);
            Dictionary<int, int> objectNumberMap = new Dictionary<int, int>();
            
            // Create mapping for PDF2 objects
            foreach (var obj in doc2.Objects)
            {
                int newNumber = obj.ObjectNumber + maxObjNumPdf1;
                objectNumberMap[obj.ObjectNumber] = newNumber;
                obj.ObjectNumber = newNumber;
            }

            // Update references in PDF2 objects
            foreach (var obj in doc2.Objects)
            {
                UpdateObjectReferences(obj, objectNumberMap);
            }

            // Merge page trees
            var allPages = new List<PDFObject>();
            allPages.AddRange(doc1.PageObjects);
            allPages.AddRange(doc2.PageObjects);

            // Update PDF1's Pages object
            if (doc1.Pages != null)
            {
                var kidsRefs = string.Join(" ", allPages.Select(p => $"{p.ObjectNumber} {p.GenerationNumber} R"));
                doc1.Pages.Dictionary["/Kids"] = $"[{kidsRefs}]";
                doc1.Pages.Dictionary["/Count"] = allPages.Count.ToString();
            }

            // Combine all objects
            var allObjects = new List<PDFObject>();
            allObjects.AddRange(doc1.Objects);
            allObjects.AddRange(doc2.Objects);

            // Build the merged PDF content
            var mergedContent = new StringBuilder();
            
            // Escribir el header del PDF1
            mergedContent.Append(doc1.Header);
            if (!doc1.Header.EndsWith("\r\n"))
            {
                mergedContent.Append("\r\n");
            }

            // Escribir todos los objetos en orden
            foreach (var obj in allObjects.OrderBy(o => o.ObjectNumber))
            {
                mergedContent.Append(RebuildObject(obj));
            }

            // Calcular offsets para la tabla XRef
            var xrefEntries = new List<KeyValuePair<int, long>>();
            long currentOffset = 0;
            string currentContent = mergedContent.ToString();
            byte[] currentBytes = pdfEncoding.GetBytes(currentContent);

            foreach (var obj in allObjects.OrderBy(o => o.ObjectNumber))
            {
                string objPattern = $"{obj.ObjectNumber} {obj.GenerationNumber} obj";
                int objIndex = currentContent.IndexOf(objPattern);
                if (objIndex >= 0)
                {
                    long byteOffset = pdfEncoding.GetByteCount(currentContent.Substring(0, objIndex));
                    xrefEntries.Add(new KeyValuePair<int, long>(obj.ObjectNumber, byteOffset));
                }
            }

            // Escribir la tabla XRef
            string xrefTable = BuildXrefTable(xrefEntries, allObjects.Count);
            mergedContent.Append(xrefTable);

            // Calcular el offset del startxref
            long startXrefOffset = pdfEncoding.GetByteCount(mergedContent.ToString());

            // Escribir el trailer
            string rootRef = $"{doc1.Root.ObjectNumber} {doc1.Root.GenerationNumber} R";
            string trailer = BuildTrailer(allObjects.Count, rootRef, startXrefOffset);
            mergedContent.Append(trailer);

            // Validar el contenido final
            string finalContent = mergedContent.ToString();
            
            // Asegurar que el contenido termina correctamente
            finalContent = finalContent.TrimEnd('\r', '\n', ' '); // Eliminar espacios y saltos de línea al final
            if (!finalContent.EndsWith("%%EOF"))
            {
                finalContent += "\r\n%%EOF";
            }

            // Escribir el archivo final
            byte[] finalBytes = pdfEncoding.GetBytes(finalContent);
            
            // Verificar que el archivo termina correctamente
            byte[] eofBytes = new byte[] { 0x25, 0x25, 0x45, 0x4F, 0x46 }; // %%EOF en bytes
            if (!finalBytes.EndsWith(eofBytes))
            {
                Console.WriteLine("Warning: PDF file might not end correctly, attempting to fix...");
                // Asegurar que termina con %%EOF
                finalContent = finalContent.TrimEnd('\r', '\n', ' ') + "\r\n%%EOF";
                finalBytes = pdfEncoding.GetBytes(finalContent);
            }

            File.WriteAllBytes(outputPath, finalBytes);

            // Verificación final del archivo
            if (File.Exists(outputPath))
            {
                byte[] writtenBytes = File.ReadAllBytes(outputPath);
                if (!writtenBytes.EndsWith(eofBytes))
                {
                    Console.WriteLine("Warning: Written file might not end correctly, attempting final fix...");
                    // Último intento de corrección
                    using (var writer = new StreamWriter(outputPath, true, pdfEncoding))
                    {
                        writer.Write("\r\n%%EOF");
                    }
                }
            }

            // Verificar que el archivo resultante es un PDF válido
            if (!IsValidPDF(outputPath))
            {
                throw new Exception("The generated PDF file is not valid.");
            }

            Console.WriteLine($"PDF merge completed successfully: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during PDF merge: {ex.Message}");
            // Si hay un error, intentar eliminar el archivo corrupto
            if (File.Exists(outputPath))
            {
                try
                {
                    File.Delete(outputPath);
                }
                catch { }
            }
            throw;
        }
    }

    private string GetHeader(string pdfString)
    {
        int firstObjIndex = pdfString.IndexOf("obj");
        if (firstObjIndex > 0 && firstObjIndex < 30)
        {
             int endOfHeader = pdfString.LastIndexOf("%PDF-", firstObjIndex);
             if(endOfHeader != -1) {
                int newlineAfterHeader = pdfString.IndexOf("\n", endOfHeader);
                if(newlineAfterHeader != -1 && newlineAfterHeader < firstObjIndex)
                    return pdfString.Substring(0, newlineAfterHeader + 1);
             }
        }
        if (pdfString.StartsWith("%PDF-")) {
            int newline = pdfString.IndexOfAny(new char[] {'\r', '\n'});
            return pdfString.Substring(0, newline + (pdfString[newline] == '\r' && pdfString.Length > newline+1 && pdfString[newline+1] == '\n' ? 2:1) );
        }
        return "%PDF-1.7\r\n%âãÏÓ\r\n";
    }

    private string BuildXrefTable(List<KeyValuePair<int, long>> entries, int totalObjects)
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

    private string BuildTrailer(int numObjects, string rootRef, long startXrefByteOffsetValue)
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

    private PDFDocument ParsePDFDocument(string pdfContent, Encoding encoding)
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

    private void ParseDictionary(string dictContent, PDFObject obj)
    {
        var entries = Regex.Matches(dictContent, @"/(\w+)\s+([^/]+?)(?=/\w+|$)");
        foreach (Match entry in entries)
        {
            obj.Dictionary[entry.Groups[1].Value] = entry.Groups[2].Value.Trim();
        }
    }

    private string RebuildObject(PDFObject obj)
    {
        var sb = new StringBuilder();
        
        // Asegurar que el número de objeto y generación estén correctos
        sb.Append($"{obj.ObjectNumber} {obj.GenerationNumber} obj\r\n");
        
        if (obj.Dictionary.Any())
        {
            sb.Append("<<\r\n");
            foreach (var entry in obj.Dictionary)
            {
                // Asegurar que las entradas del diccionario estén correctamente formateadas
                string value = entry.Value.Trim();
                if (value.StartsWith("[") || value.StartsWith("(") || value.StartsWith("/"))
                {
                    sb.Append($"/{entry.Key} {value}\r\n");
                }
                else
                {
                    sb.Append($"/{entry.Key} {value}\r\n");
                }
            }
            sb.Append(">>\r\n");
        }

        if (obj.IsStream)
        {
            sb.Append("stream\r\n");
            // Si es un stream, usar el contenido directamente
            if (!string.IsNullOrEmpty(obj.Content))
            {
                sb.Append(obj.Content);
                // Actualizar /Length si existe
                if (obj.Dictionary.ContainsKey("/Length"))
                {
                    obj.Dictionary["/Length"] = obj.Content.Length.ToString();
                }
            }
            sb.Append("\r\nendstream\r\n");
        }
        else
        {
            // Para contenido no-stream, asegurar que esté correctamente formateado
            string content = obj.Content.Trim();
            if (!string.IsNullOrEmpty(content))
            {
                sb.Append(content);
                if (!content.EndsWith("\r\n"))
                {
                    sb.Append("\r\n");
                }
            }
        }

        sb.Append("endobj\r\n");
        return sb.ToString();
    }

    private void UpdateObjectReferences(PDFObject obj, Dictionary<int, int> objectNumberMap)
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

    public bool TestMergePDF(string pdfPath1, string pdfPath2, string outputPath)
    {
        try
        {
            Console.WriteLine("Iniciando prueba de merge de PDFs...");
            
            // Verificar que los archivos existen
            if (!File.Exists(pdfPath1))
            {
                Console.WriteLine($"Error: No se encuentra el archivo {pdfPath1}");
                return false;
            }
            if (!File.Exists(pdfPath2))
            {
                Console.WriteLine($"Error: No se encuentra el archivo {pdfPath2}");
                return false;
            }

            // Verificar que los archivos son PDFs válidos
            if (!IsValidPDF(pdfPath1))
            {
                Console.WriteLine($"Error: {pdfPath1} no es un PDF válido");
                return false;
            }
            if (!IsValidPDF(pdfPath2))
            {
                Console.WriteLine($"Error: {pdfPath2} no es un PDF válido");
                return false;
            }

            // Obtener información de los PDFs originales
            var pdf1Info = GetPDFInfo(pdfPath1);
            var pdf2Info = GetPDFInfo(pdfPath2);
            
            Console.WriteLine($"PDF1: {pdf1Info.PageCount} páginas, {pdf1Info.ObjectCount} objetos");
            Console.WriteLine($"PDF2: {pdf2Info.PageCount} páginas, {pdf2Info.ObjectCount} objetos");

            // Realizar el merge
            MergePDF(pdfPath1, pdfPath2, outputPath);

            // Verificar que el archivo resultante existe
            if (!File.Exists(outputPath))
            {
                Console.WriteLine("Error: No se generó el archivo de salida");
                return false;
            }

            // Verificar que el PDF resultante es válido
            if (!IsValidPDF(outputPath))
            {
                Console.WriteLine("Error: El PDF resultante no es válido");
                return false;
            }

            // Obtener información del PDF resultante
            var resultInfo = GetPDFInfo(outputPath);
            Console.WriteLine($"PDF Resultante: {resultInfo.PageCount} páginas, {resultInfo.ObjectCount} objetos");

            // Verificar que el número de páginas es correcto
            if (resultInfo.PageCount != pdf1Info.PageCount + pdf2Info.PageCount)
            {
                Console.WriteLine($"Error: El número de páginas no coincide. Esperado: {pdf1Info.PageCount + pdf2Info.PageCount}, Obtenido: {resultInfo.PageCount}");
                return false;
            }

            Console.WriteLine("Prueba completada exitosamente");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error durante la prueba: {ex.Message}");
            return false;
        }
    }

    private bool IsValidPDF(string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < 8) return false; // Mínimo tamaño para un PDF válido

            // Verificar la firma del PDF
            string header = Encoding.ASCII.GetString(bytes, 0, 5);
            if (!header.StartsWith("%PDF-")) return false;

            // Verificar que termina con %%EOF
            string content = Encoding.ASCII.GetString(bytes);
            int eofIndex = content.LastIndexOf("%%EOF");
            if (eofIndex == -1) return false;

            // Verificar que %%EOF está al final o seguido por un salto de línea
            if (eofIndex + 5 < bytes.Length)
            {
                // Debe haber solo un salto de línea después de %%EOF
                byte[] remainingBytes = new byte[bytes.Length - (eofIndex + 5)];
                Array.Copy(bytes, eofIndex + 5, remainingBytes, 0, remainingBytes.Length);
                string remaining = Encoding.ASCII.GetString(remainingBytes).Trim();
                if (!string.IsNullOrEmpty(remaining)) return false;
            }

            // Verificar que hay un trailer
            if (!content.Contains("trailer")) return false;

            // Verificar que hay un startxref
            if (!content.Contains("startxref")) return false;

            // Verificar que hay al menos un objeto
            if (!content.Contains("obj")) return false;

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating PDF: {ex.Message}");
            return false;
        }
    }

    private (int PageCount, int ObjectCount) GetPDFInfo(string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            string content = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);

            // Contar objetos
            int objectCount = Regex.Matches(content, @"\d+\s+\d+\s+obj").Count;

            // Contar páginas - múltiples métodos
            int pageCount = 0;

            // Método 1: Buscar en el objeto Pages
            var pagesMatch = Regex.Match(content, @"/Type\s*/Pages[^>]*?/Count\s*(\d+)");
            if (pagesMatch.Success)
            {
                int.TryParse(pagesMatch.Groups[1].Value, out pageCount);
                Console.WriteLine($"Found page count in Pages object: {pageCount}");
            }

            // Método 2: Contar objetos Page
            if (pageCount == 0)
            {
                var pageObjects = Regex.Matches(content, @"/Type\s*/Page\b");
                pageCount = pageObjects.Count;
                Console.WriteLine($"Found {pageCount} Page objects");
            }

            // Método 3: Buscar en Kids
            if (pageCount == 0)
            {
                var kidsMatch = Regex.Match(content, @"/Kids\s*\[(.*?)\]", RegexOptions.Singleline);
                if (kidsMatch.Success)
                {
                    var kidsContent = kidsMatch.Groups[1].Value;
                    var kidRefs = Regex.Matches(kidsContent, @"\d+\s+\d+\s+R");
                    pageCount = kidRefs.Count;
                    Console.WriteLine($"Found {pageCount} page references in Kids array");
                }
            }

            // Método 4: Buscar en el trailer
            if (pageCount == 0)
            {
                var trailerMatch = Regex.Match(content, @"trailer\s*<<(.*?)>>", RegexOptions.Singleline);
                if (trailerMatch.Success)
                {
                    var trailerContent = trailerMatch.Groups[1].Value;
                    var pagesRefMatch = Regex.Match(trailerContent, @"/Pages\s*(\d+)\s+\d+\s+R");
                    if (pagesRefMatch.Success)
                    {
                        int pagesObjNum = int.Parse(pagesRefMatch.Groups[1].Value);
                        var pagesObjMatch = Regex.Match(content, $@"{pagesObjNum}\s+\d+\s+obj.*?/Count\s*(\d+)", RegexOptions.Singleline);
                        if (pagesObjMatch.Success)
                        {
                            int.TryParse(pagesObjMatch.Groups[1].Value, out pageCount);
                            Console.WriteLine($"Found page count in Pages object from trailer: {pageCount}");
                        }
                    }
                }
            }

            // Verificación adicional
            if (pageCount == 0)
            {
                Console.WriteLine("Warning: Could not determine page count using standard methods");
                Console.WriteLine("Content preview for analysis:");
                Console.WriteLine(content.Substring(0, Math.Min(1000, content.Length)) + "...");
            }

            return (pageCount, objectCount);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting PDF info: {ex.Message}");
            return (0, 0);
        }
    }
}
