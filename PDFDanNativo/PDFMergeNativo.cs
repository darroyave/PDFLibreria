using PDFDanNativo.Models;

namespace PDFDanNativo;

public interface IPDFMergeNativo
{
    /// <summary>
    /// Crea un archivo PDF a partir del texto proporcionado
    /// </summary>
    /// <param name="filePath">Ruta donde se guardará el archivo PDF</param>
    /// <param name="texto">Contenido del texto a incluir en el PDF</param>
    /// <param name="config">Configuración opcional para personalizar el PDF</param>
    void MergePdfs(string pdf1Path, string pdf2Path, string outputPath, PDFConfig? config = null);
}

public class PDFMergeNativo(
    IPDFCore pdfCore) : IPDFMergeNativo
{
    public void MergePdfs(string pdf1Path, string pdf2Path, string outputPath, PDFConfig? config = null)
    {
        config ??= new PDFConfig(); // Usar configuración por defecto si no se proporciona

        try
        {
           
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not ArgumentNullException)
        {
            throw new IOException($"Error al crear el archivo PDF: {ex.Message}", ex);
        }
    }
}