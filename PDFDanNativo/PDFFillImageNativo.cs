using PDFDanNativo.Core;
using PDFDanNativo.Models;
using System.Text;

namespace PDFDanNativo;

public interface IPDFFillImageNativo
{
    /// <summary>
    /// Agrega una imagen centrada a un PDF existente
    /// </summary>
    /// <param name="inputPdf">Ruta del archivo PDF de entrada</param>
    /// <param name="imagePath">Ruta de la imagen JPEG a insertar</param>
    /// <param name="outputPdf">Ruta donde se guardará el PDF con la imagen</param>
    /// <param name="scale">Escala de la imagen (0.0 a 1.0, por defecto 0.8)</param>
    /// <param name="config">Configuración opcional para personalizar el PDF</param>
    void AddImageToPdf(string inputPdf, string imagePath, string outputPdf, float scale = 0.8f, PDFConfig? config = null);
}

public class PDFFillImageNativo(
    IPDFCore pdfCore) : IPDFFillImageNativo
{
    public void AddImageToPdf(string inputPdf, string imagePath, string outputPdf, float scale = 0.8f, PDFConfig? config = null)
    {
        config ??= new PDFConfig();

        if (!File.Exists(inputPdf))
            throw new ArgumentException("El archivo PDF de entrada no existe", nameof(inputPdf));

        if (!File.Exists(imagePath))
            throw new ArgumentException("El archivo de imagen no existe", nameof(imagePath));

        if (scale <= 0 || scale > 1)
            throw new ArgumentException("La escala debe estar entre 0 y 1", nameof(scale));

        try
        {
            // Leer el PDF de entrada y procesar la imagen
            string pdf = File.ReadAllText(inputPdf, Encoding.ASCII);

            var (updatedObjects, _) = pdfCore.ProcessImage(pdf, imagePath, scale);

            // Reconstruir el PDF
            string header = "%PDF-1.4\n";

            int[] offsets = pdfCore.CalculateOffsets(updatedObjects.ToArray());

            string xref = pdfCore.BuildXrefTable(updatedObjects.ToArray(), offsets);

            string trailer = pdfCore.BuildTrailer(updatedObjects.Count, offsets[^1], config);

            // Escribir el PDF final
            pdfCore.WritePdfFile(outputPdf, header, updatedObjects.ToArray(), xref, trailer);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new IOException($"Error al agregar la imagen al PDF: {ex.Message}", ex);
        }
    }
} 