using PDFDanNativo.Core;
using PDFDanNativo.Models;
using System.Text;

namespace PDFDanNativo;

public interface IPDFAttachNativo
{
    /// <summary>
    /// Adjunta uno o más archivos de texto a un PDF existente
    /// </summary>
    /// <param name="pdfPath">Ruta del archivo PDF de entrada</param>
    /// <param name="txtPaths">Rutas de los archivos de texto a adjuntar</param>
    /// <param name="outputPdfPath">Ruta donde se guardará el PDF con los archivos adjuntos</param>
    /// <param name="config">Configuración opcional para personalizar el PDF</param>
    void AttachFileToPdf(string pdfPath, string[] txtPaths, string outputPdfPath, PDFConfig? config = null);
}

public class PDFAttachNativo(
    IPDFCore pdfCore) : IPDFAttachNativo
{
    public void AttachFileToPdf(string pdfPath, string[] txtPaths, string outputPdfPath, PDFConfig? config = null)
    {
        config ??= new PDFConfig();

        if (!File.Exists(pdfPath))
            throw new ArgumentException("El archivo PDF de entrada no existe", nameof(pdfPath));

        if (txtPaths == null || txtPaths.Length == 0)
            throw new ArgumentException("Debe proporcionar al menos un archivo para adjuntar", nameof(txtPaths));

        try
        {
            // Leer el PDF de entrada y procesar adjuntos
            string pdf = File.ReadAllText(pdfPath, Encoding.ASCII);

            var (updatedObjects, _) = pdfCore.ProcessAttachments(pdf, txtPaths);

            // Reconstruir el PDF
            string header = "%PDF-1.4\n";

            int[] offsets = pdfCore.CalculateOffsets(updatedObjects.ToArray());

            string xref = pdfCore.BuildXrefTable(updatedObjects.ToArray(), offsets);

            string trailer = pdfCore.BuildTrailer(updatedObjects.Count, offsets[^1], config);

            // Escribir el PDF final
            pdfCore.WritePdfFile(outputPdfPath, header, updatedObjects.ToArray(), xref, trailer);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new IOException($"Error al adjuntar archivos al PDF: {ex.Message}", ex);
        }
    }
}