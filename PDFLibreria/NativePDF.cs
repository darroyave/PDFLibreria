using System.Text;
using System.Text.RegularExpressions; // For Regex

namespace PDFLibreria;

public class PDFObject
{
    public int ObjectNumber { get; set; }
    public int GenerationNumber { get; set; }
    public string Content { get; set; }
    public bool IsStream { get; set; }
    public Dictionary<string, string> Dictionary { get; set; }
    public long Offset { get; set; }
    public List<int> References { get; set; }

    public PDFObject()
    {
        Dictionary = new Dictionary<string, string>();
        References = new List<int>();
    }
}

public class PDFDocument
{
    public string Header { get; set; }
    public List<PDFObject> Objects { get; set; }
    public PDFObject Root { get; set; }
    public PDFObject Pages { get; set; }
    public List<PDFObject> PageObjects { get; set; }

    public PDFDocument()
    {
        Objects = new List<PDFObject>();
        PageObjects = new List<PDFObject>();
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

        byte[] pdf1Bytes = ReadPdfBytes(pdfPath1);
        byte[] pdf2Bytes = ReadPdfBytes(pdfPath2);

        if (pdf1Bytes == null || pdf2Bytes == null)
        {
            Console.WriteLine("Error reading one or both PDF files.");
            return;
        }

        Encoding pdfEncoding = Encoding.GetEncoding("ISO-8859-1");

        // Parse both PDFs into document structures
        PDFDocument doc1 = ParsePDFDocument(pdfEncoding.GetString(pdf1Bytes), pdfEncoding);
        PDFDocument doc2 = ParsePDFDocument(pdfEncoding.GetString(pdf2Bytes), pdfEncoding);

        if (doc1.Pages == null || doc2.Pages == null)
        {
            Console.WriteLine("Error: One or both PDFs are missing required page structure.");
            return;
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
            // Update dictionary entries
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

            // Update content references
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
        mergedContent.Append(doc1.Header);

        // Write all objects
        foreach (var obj in allObjects.OrderBy(o => o.ObjectNumber))
        {
            mergedContent.Append(RebuildObject(obj));
        }

        // Calculate and write xref table
        var xrefEntries = allObjects
            .OrderBy(o => o.ObjectNumber)
            .Select(o => new KeyValuePair<int, long>(o.ObjectNumber, o.Offset))
            .ToList();

        string xrefTable = BuildXrefTable(xrefEntries, allObjects.Count);
        mergedContent.Append(xrefTable);

        // Write trailer
        long startXrefOffset = pdfEncoding.GetByteCount(mergedContent.ToString());
        string rootRef = $"{doc1.Root.ObjectNumber} {doc1.Root.GenerationNumber} R";
        string trailer = BuildTrailer(allObjects.Count, rootRef, startXrefOffset);
        mergedContent.Append(trailer);

        // Write to output file
        try
        {
            File.WriteAllBytes(outputPath, pdfEncoding.GetBytes(mergedContent.ToString()));
            Console.WriteLine($"PDF merge completed successfully: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing merged PDF: {ex.Message}");
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
        StringBuilder xrefBuilder = new StringBuilder();
        xrefBuilder.AppendLine("xref");

        if (!entries.Any() && totalObjects == 0)
        {
            xrefBuilder.AppendLine("0 1");
            xrefBuilder.AppendLine("0000000000 65535 f ");
            return xrefBuilder.ToString();
        }

        xrefBuilder.AppendLine($"0 {totalObjects}");
        xrefBuilder.AppendLine("0000000000 65535 f ");

        var entryDict = entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        for (int i = 1; i < totalObjects; i++)
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
        StringBuilder trailer = new StringBuilder();
        trailer.AppendLine("trailer");
        trailer.AppendLine("<<");
        trailer.AppendLine($"  /Size {numObjects}");
        trailer.AppendLine($"  /Root {rootRef}");
        trailer.AppendLine(">>");
        trailer.AppendLine("startxref");
        trailer.AppendLine(startXrefByteOffsetValue.ToString());
        trailer.AppendLine("%%EOF");
        return trailer.ToString();
    }

    private PDFDocument ParsePDFDocument(string pdfContent, Encoding encoding)
    {
        var doc = new PDFDocument();
        doc.Header = GetHeader(pdfContent);
        
        // Parse all objects
        var objectMatches = Regex.Matches(pdfContent, @"(\d+)\s+(\d+)\s+obj\s*(.*?)(?=\d+\s+\d+\s+obj|$)", RegexOptions.Singleline);
        
        foreach (Match match in objectMatches)
        {
            var obj = new PDFObject
            {
                ObjectNumber = int.Parse(match.Groups[1].Value),
                GenerationNumber = int.Parse(match.Groups[2].Value),
                Content = match.Groups[3].Value.Trim(),
                Offset = encoding.GetByteCount(pdfContent.Substring(0, match.Index))
            };

            // Check if it's a stream
            if (obj.Content.Contains("stream") && obj.Content.Contains("endstream"))
            {
                obj.IsStream = true;
                // Extract stream dictionary
                var dictMatch = Regex.Match(obj.Content, @"<<(.*?)>>\s*stream", RegexOptions.Singleline);
                if (dictMatch.Success)
                {
                    ParseDictionary(dictMatch.Groups[1].Value, obj);
                }
            }
            else
            {
                // Parse dictionary if exists
                var dictMatch = Regex.Match(obj.Content, @"<<(.*?)>>", RegexOptions.Singleline);
                if (dictMatch.Success)
                {
                    ParseDictionary(dictMatch.Groups[1].Value, obj);
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

        // Find Root and Pages objects
        doc.Root = doc.Objects.FirstOrDefault(o => o.Dictionary.ContainsKey("/Type") && o.Dictionary["/Type"] == "/Catalog");
        if (doc.Root != null && doc.Root.Dictionary.ContainsKey("/Pages"))
        {
            var pagesRef = doc.Root.Dictionary["/Pages"];
            var pagesMatch = Regex.Match(pagesRef, @"(\d+)\s+(\d+)\s+R");
            if (pagesMatch.Success)
            {
                doc.Pages = doc.Objects.FirstOrDefault(o => o.ObjectNumber == int.Parse(pagesMatch.Groups[1].Value));
            }
        }

        // Find all page objects
        if (doc.Pages != null)
        {
            var kidsRef = doc.Pages.Dictionary.GetValueOrDefault("/Kids", "");
            var kidMatches = Regex.Matches(kidsRef, @"(\d+)\s+(\d+)\s+R");
            foreach (Match kidMatch in kidMatches)
            {
                var pageObj = doc.Objects.FirstOrDefault(o => o.ObjectNumber == int.Parse(kidMatch.Groups[1].Value));
                if (pageObj != null)
                {
                    doc.PageObjects.Add(pageObj);
                }
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
        sb.Append($"{obj.ObjectNumber} {obj.GenerationNumber} obj\r\n");
        
        if (obj.Dictionary.Any())
        {
            sb.Append("<<\r\n");
            foreach (var entry in obj.Dictionary)
            {
                sb.Append($"/{entry.Key} {entry.Value}\r\n");
            }
            sb.Append(">>\r\n");
        }

        if (obj.IsStream)
        {
            sb.Append("stream\r\n");
            // Here we would handle stream content
            sb.Append("endstream\r\n");
        }
        else
        {
            sb.Append(obj.Content);
        }

        sb.Append("\r\nendobj\r\n");
        return sb.ToString();
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
            if (bytes.Length < 5) return false;

            // Verificar la firma del PDF
            string header = Encoding.ASCII.GetString(bytes, 0, 5);
            if (!header.StartsWith("%PDF-")) return false;

            // Verificar que termina con %%EOF
            string footer = Encoding.ASCII.GetString(bytes, bytes.Length - 6, 6);
            if (!footer.Contains("%%EOF")) return false;

            return true;
        }
        catch
        {
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

            // Contar páginas
            int pageCount = 0;
            var pagesMatch = Regex.Match(content, @"/Type\s*/Pages[^>]*?/Count\s*(\d+)");
            if (pagesMatch.Success)
            {
                int.TryParse(pagesMatch.Groups[1].Value, out pageCount);
            }

            return (pageCount, objectCount);
        }
        catch
        {
            return (0, 0);
        }
    }
}
