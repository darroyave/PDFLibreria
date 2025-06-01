using PDFLibreria.Extensions;
using PDFLibreria.Models;
using PDFLibreria.Services;
using System.IO.Compression;
using System.Text;

namespace PDFLibreria;

public class NativePDF
{
    private readonly Encoding _pdfEncoding = Encoding.GetEncoding("ISO-8859-1");
    private const string PDF_HEADER = "%PDF-";
    private const string PDF_EOF = "%%EOF";
    private static readonly byte[] EOF_BYTES = new byte[] { 0x25, 0x25, 0x45, 0x4F, 0x46 }; // %%EOF

    public void MergePDF(string pdfPath1, string pdfPath2, string outputPath)
    {
        Console.WriteLine($"Attempting to merge {pdfPath1} and {pdfPath2} into {outputPath}");

        try
        {
            byte[] pdf1Bytes = PdfFileUtils.ReadPdfBytes(pdfPath1);
            byte[] pdf2Bytes = PdfFileUtils.ReadPdfBytes(pdfPath2);

        if (pdf1Bytes == null || pdf2Bytes == null)
        {
                throw new Exception("Error reading one or both PDF files.");
        }

        Encoding pdfEncoding = Encoding.GetEncoding("ISO-8859-1");

            // Parse both PDFs into document structures
            PDFDocument doc1 = PDFCore.ParsePDFDocument(pdfEncoding.GetString(pdf1Bytes), pdfEncoding);
            PDFDocument doc2 = PDFCore.ParsePDFDocument(pdfEncoding.GetString(pdf2Bytes), pdfEncoding);

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
                PDFCore.UpdateObjectReferences(obj, objectNumberMap);
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
                mergedContent.Append(PDFCore.RebuildObject(obj));
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
            string xrefTable = PDFCore.BuildXrefTable(xrefEntries, allObjects.Count);
            mergedContent.Append(xrefTable);

            // Calcular el offset del startxref
            long startXrefOffset = pdfEncoding.GetByteCount(mergedContent.ToString());

            // Escribir el trailer
            string rootRef = $"{doc1.Root.ObjectNumber} {doc1.Root.GenerationNumber} R";
            string trailer = PDFCore.BuildTrailer(allObjects.Count, rootRef, startXrefOffset);
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
            if (!finalBytes.EndsWith(EOF_BYTES))
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
                if (!writtenBytes.EndsWith(EOF_BYTES))
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
            if (!PdfValidationUtils.IsValidPDF(outputPath))
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

    private byte[] CompressContent(string content)
    {
        try
        {
            Console.WriteLine("Compressing content stream...");
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                {
                    byte[] contentBytes = _pdfEncoding.GetBytes(content);
                    gzip.Write(contentBytes, 0, contentBytes.Length);
                }
                var compressed = output.ToArray();
                Console.WriteLine($"Content compressed from {content.Length} to {compressed.Length} bytes");
                return compressed;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error compressing content: {ex.Message}");
            throw;
        }
    }

    public PDFDocument CreateDocumentFromText(string text)
    {
        Console.WriteLine("Creating PDF document structure...");
        
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Text cannot be null or empty");
        }

        var doc = new PDFDocument
        {
            Header = $"{PDF_HEADER}1.7\r\n%âãÏÓ\r\n",
            Version = "1.7"
        };

        // Crear y comprimir el contenido
        var contentText = CreateTextContent(text);
        Console.WriteLine($"Original content length: {contentText.Length} bytes");
        
        byte[] compressedContent = CompressContent(contentText);
        
        var contentObj = new PDFObject
        {
            ObjectNumber = 1,
            GenerationNumber = 0,
            Type = "Stream",
            IsStream = true,
            RawContent = compressedContent // Usar el contenido comprimido directamente
        };
        contentObj.Dictionary["/Length"] = compressedContent.Length.ToString();
        contentObj.Dictionary["/Filter"] = "/FlateDecode";
        contentObj.Dictionary["/DecodeParms"] = "<< /Predictor 12 /Colors 1 /BitsPerComponent 8 /Columns 1 >>";

        // Crear objeto de página con dimensiones estándar A4
        var pageObj = new PDFObject
        {
            ObjectNumber = 2,
            GenerationNumber = 0,
            Type = "Page",
            Content = ""
        };
        pageObj.Dictionary["/Type"] = "/Page";
        pageObj.Dictionary["/Parent"] = "3 0 R";
        pageObj.Dictionary["/Resources"] = "<< /Font << /F1 4 0 R >> >>";
        pageObj.Dictionary["/MediaBox"] = "[0 0 595 842]"; // A4 size in points
        pageObj.Dictionary["/Contents"] = "1 0 R";

        // Crear objeto Pages con validación de Kids
        var pagesObj = new PDFObject
        {
            ObjectNumber = 3,
            GenerationNumber = 0,
            Type = "Pages",
            Content = ""
        };
        pagesObj.Dictionary["/Type"] = "/Pages";
        pagesObj.Dictionary["/Kids"] = "[2 0 R]";
        pagesObj.Dictionary["/Count"] = "1";

        // Crear objeto Font con fuente estándar
        var fontObj = new PDFObject
        {
            ObjectNumber = 4,
            GenerationNumber = 0,
            Type = "Font",
            Content = ""
        };
        fontObj.Dictionary["/Type"] = "/Font";
        fontObj.Dictionary["/Subtype"] = "/Type1";
        fontObj.Dictionary["/BaseFont"] = "/Helvetica";
        fontObj.Dictionary["/Encoding"] = "/WinAnsiEncoding";

        // Crear objeto Catalog (Root) con validación de Pages
        var catalogObj = new PDFObject
        {
            ObjectNumber = 5,
            GenerationNumber = 0,
            Type = "Catalog",
            Content = ""
        };
        catalogObj.Dictionary["/Type"] = "/Catalog";
        catalogObj.Dictionary["/Pages"] = "3 0 R";

        // Agregar objetos al documento en orden
        doc.Objects.Add(contentObj);
        doc.Objects.Add(pageObj);
        doc.Objects.Add(pagesObj);
        doc.Objects.Add(fontObj);
        doc.Objects.Add(catalogObj);

        // Establecer referencias y validar
        doc.Root = catalogObj;
        doc.Pages = pagesObj;
        doc.PageObjects.Add(pageObj);

        // Validar estructura básica
        if (doc.Root == null || doc.Pages == null || doc.PageObjects.Count == 0)
        {
            throw new Exception("Invalid PDF structure: missing required objects");
        }

        Console.WriteLine("PDF document structure created successfully");
        Console.WriteLine($"Total objects: {doc.Objects.Count}");
        Console.WriteLine($"Page objects: {doc.PageObjects.Count}");
        
        return doc;
    }

    private string CreateTextContent(string text)
    {
        var content = new StringBuilder();
        
        // Iniciar bloque de texto
        content.AppendLine("BT");
        
        // Configurar fuente y tamaño
        content.AppendLine("/F1 12 Tf");
        
        // Posicionar texto (x y Tm)
        // Ajustar posición para mejor visibilidad y agregar más espacio
        content.AppendLine("1 0 0 1 50 750 Tm");
        
        // Dividir el texto en líneas para mejor manejo
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        float currentY = 750;
        
        foreach (var line in lines)
        {
            // Escapar y agregar texto
            var escapedText = EscapeText(line);
            content.AppendLine($"({escapedText}) Tj");
            
            // Mover a la siguiente línea
            currentY -= 20; // Espacio entre líneas
            content.AppendLine($"1 0 0 1 50 {currentY} Tm");
        }
        
        // Finalizar bloque de texto
        content.AppendLine("ET");
        
        var result = content.ToString();
        Console.WriteLine($"Generated text content length: {result.Length} bytes");
        return result;
    }

    private string EscapeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "()";
            
        // Escapar caracteres especiales según PDF spec
        return text.Replace("\\", "\\\\")
                  .Replace("(", "\\(")
                  .Replace(")", "\\)")
                  .Replace("\r", "\\r")
                  .Replace("\n", "\\n")
                  .Replace("\t", "\\t")
                  .Replace("\f", "\\f")
                  .Replace("\b", "\\b");
    }

    public void CreatePdfFromText(string text, string outputPath)
    {
        try
        {
            Console.WriteLine($"Creating PDF from text and saving to {outputPath}");
            
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text cannot be null or empty");
            }
            
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException("Output path cannot be null or empty");
            }
            
            // Crear el documento
            var doc = CreateDocumentFromText(text);
            
            // Validar documento antes de guardar
            if (doc.Objects.Count == 0)
            {
                throw new Exception("Document has no objects");
            }
            
            if (doc.Root == null)
            {
                throw new Exception("Document is missing Root object");
            }
            
            if (doc.Pages == null)
            {
                throw new Exception("Document is missing Pages object");
            }
            
            if (doc.PageObjects.Count == 0)
            {
                throw new Exception("Document has no page objects");
            }
            
            // Guardar el documento
            PDFCore.SaveDocument(doc, outputPath);
            
            // Verificar que el archivo se creó correctamente
            if (!File.Exists(outputPath))
            {
                throw new Exception("Output file was not created");
            }
            
            var fileInfo = new FileInfo(outputPath);
            if (fileInfo.Length == 0)
            {
                throw new Exception("Output file is empty");
            }
            
            // Verificar que es un PDF válido
            if (!PdfValidationUtils.IsValidPDF(outputPath))
            {
                throw new Exception("Generated file is not a valid PDF");
            }
            
            Console.WriteLine($"PDF created successfully: {fileInfo.Length} bytes");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating PDF: {ex.Message}");
            if (File.Exists(outputPath))
            {
                try { File.Delete(outputPath); }
                catch { }
            }
            throw;
        }
    }
}