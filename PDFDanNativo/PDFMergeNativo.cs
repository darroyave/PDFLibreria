using PDFDanNativo.Models;
using System.Text;

namespace PDFDanNativo;

public interface IPDFMergeNativo
{
    /// <summary>
    /// Combina dos archivos PDF en uno solo
    /// </summary>
    /// <param name="pdf1Path">Ruta del primer archivo PDF</param>
    /// <param name="pdf2Path">Ruta del segundo archivo PDF</param>
    /// <param name="outputPath">Ruta donde se guardará el PDF combinado</param>
    /// <param name="config">Configuración opcional para personalizar el PDF resultante</param>
    /// <exception cref="FileNotFoundException">Cuando alguno de los archivos PDF no existe</exception>
    /// <exception cref="InvalidOperationException">Cuando alguno de los archivos no es un PDF válido</exception>
    /// <exception cref="IOException">Cuando hay errores de lectura/escritura</exception>
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
            // Leer contenido de los PDFs
            string pdf1 = File.ReadAllText(pdf1Path, Encoding.ASCII);
            string pdf2 = File.ReadAllText(pdf2Path, Encoding.ASCII);

            // Extraer objetos de cada PDF
            string[] objs1 = pdfCore.ExtractPdfObjects(pdf1, 5);
            string[] objs2 = pdfCore.ExtractPdfObjects(pdf2, 5);

            // Combinar los objetos
            string[] mergedObjects = pdfCore.MergePdfObjects(objs1, objs2);

            // Construir el PDF combinado
            string mergedPdfContent = pdfCore.BuildMergedPdfContent(mergedObjects, config);

            // Guardar el resultado
            File.WriteAllText(outputPath, mergedPdfContent, Encoding.ASCII);
        }
        catch (Exception ex) when (ex is not FileNotFoundException && ex is not InvalidOperationException)
        {
            throw new IOException($"Error al combinar los archivos PDF: {ex.Message}", ex);
        }
    }
}