using System.Text;
using System.Text.RegularExpressions;

namespace PDFLibreria.Services;

public static class PdfValidationUtils
{
    public static bool IsValidPDF(string filePath)
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

    public static (int PageCount, int ObjectCount) GetPDFInfo(string filePath)
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