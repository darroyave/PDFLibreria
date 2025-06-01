using PDFDanNativo.Core;
using System.Text;

namespace PDFDanNativo;

public interface IPDFFillWithFormFieldsNativo
{
    /// <summary>
    /// Rellena los campos de un formulario PDF existente con los valores proporcionados
    /// </summary>
    /// <param name="inputPath">Ruta del archivo PDF de entrada con campos de formulario</param>
    /// <param name="outputPath">Ruta donde se guardará el PDF con los campos rellenados</param>
    /// <param name="fields">Diccionario con los nombres de los campos y sus valores</param>
    void FillPdfWithFormFields(string inputPath, string outputPath, Dictionary<string, string> fields);
}

public class PDFFillWithFormFieldsNativo(
    IPDFCore pdfCore) : IPDFFillWithFormFieldsNativo
{
    public void FillPdfWithFormFields(string inputPath, string outputPath, Dictionary<string, string> fields)
    {
        if (!File.Exists(inputPath))
            throw new ArgumentException("El archivo PDF de entrada no existe", nameof(inputPath));

        if (fields == null || fields.Count == 0)
            throw new ArgumentException("Debe proporcionar al menos un campo de formulario", nameof(fields));

        try
        {
            // Leer el PDF de entrada
            string pdf = File.ReadAllText(inputPath, Encoding.ASCII);

            // Procesar los campos del formulario
            pdf = pdfCore.ProcessFormFields(pdf, fields);

            // Guardar el PDF modificado
            File.WriteAllText(outputPath, pdf, Encoding.ASCII);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new IOException($"Error al rellenar los campos del formulario PDF: {ex.Message}", ex);
        }
    }
}