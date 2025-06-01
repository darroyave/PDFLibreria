using PDFDanNativo.Models;

namespace PDFDanNativo;

public interface IPDFNativo
{
    /// <summary>
    /// Crea un archivo PDF a partir del texto proporcionado
    /// </summary>
    /// <param name="filePath">Ruta donde se guardará el archivo PDF</param>
    /// <param name="texto">Contenido del texto a incluir en el PDF</param>
    /// <param name="config">Configuración opcional para personalizar el PDF</param>
    void CreatePdfFromText(string filePath, string texto, PDFConfig? config = null);
}

public class PDFNativo(
    IPDFCore pdfCore) : IPDFNativo
{

    /// <summary>
    /// Creates a PDF file from the provided text content.
    /// </summary>
    /// <param name="filePath">The path where the PDF file will be saved. Must be a valid path with .pdf extension.</param>
    /// <param name="texto">The text content to be included in the PDF. Cannot be null or empty.</param>
    /// <param name="config">Configuración opcional para personalizar el PDF</param>
    public void CreatePdfFromText(string filePath, string texto, PDFConfig? config = null)
    {
        config ??= new PDFConfig(); // Usar configuración por defecto si no se proporciona

        try
        {
            // 1. CABECERA con metadatos y configuración
            string header = pdfCore.BuildPdfHeader(config);

            // 2. CUERPO con formato personalizado
            var bodyObjects = pdfCore.BuildPdfBodyObjects(texto, config);

            // 3. TABLA DE REFERENCIA CRUZADA (xref)
            var contentParts = new[] { header }.Concat(bodyObjects).ToArray();
            int[] offsets = pdfCore.CalculateOffsets(contentParts);
            string xref = pdfCore.BuildXrefTable(contentParts, offsets);

            // 4. TRAILER con metadatos
            string trailer = pdfCore.BuildTrailer(contentParts.Length, offsets[^1], config);

            // Escribir todo al archivo
            pdfCore.WritePdfFile(filePath, header, bodyObjects, xref, trailer);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not ArgumentNullException)
        {
            throw new IOException($"Error al crear el archivo PDF: {ex.Message}", ex);
        }
    }
}