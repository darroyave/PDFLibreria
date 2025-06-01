using PDFDanNativo.Core;
using PDFDanNativo.Models;

namespace PDFDanNativo;

public interface IPDFCreateWithFormFieldsNativo
{
    /// <summary>
    /// Crea un archivo PDF con campos de formulario
    /// </summary>
    /// <param name="filePath">Ruta donde se guardará el archivo PDF</param>
    /// <param name="fields">Diccionario con los nombres de los campos y sus valores iniciales</param>
    /// <param name="config">Configuración opcional para personalizar el PDF</param>
    /// <exception cref="ArgumentException">Cuando el diccionario de campos está vacío</exception>
    /// <exception cref="IOException">Cuando hay errores de lectura/escritura</exception>
    void CreatePdfWithFormFields(string filePath, Dictionary<string, string> fields, PDFConfig? config = null);
}

public class PDFCreateWithFormFieldsNativo(
    IPDFCore pdfCore) : IPDFCreateWithFormFieldsNativo
{
    public void CreatePdfWithFormFields(string filePath, Dictionary<string, string> fields, PDFConfig? config = null)
    {
        config ??= new PDFConfig(); // Usar configuración por defecto si no se proporciona

        if (fields == null || fields.Count == 0)
            throw new ArgumentException("Debe proporcionar al menos un campo de formulario", nameof(fields));

        try
        {
            // 1. CABECERA con metadatos y configuración
            string header = pdfCore.BuildPdfHeader(config);

            // 2. CUERPO con campos de formulario
            var formObjects = pdfCore.BuildPdfFormObjects(fields, config);

            // 3. TABLA DE REFERENCIA CRUZADA (xref)
            var contentParts = new[] { header }.Concat(formObjects).ToArray();
            int[] offsets = pdfCore.CalculateOffsets(contentParts);
            string xref = pdfCore.BuildXrefTable(contentParts, offsets);

            // 4. TRAILER con metadatos
            string trailer = pdfCore.BuildTrailer(contentParts.Length, offsets[^1], config);

            // Escribir todo al archivo
            pdfCore.WritePdfFile(filePath, header, formObjects, xref, trailer);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new IOException($"Error al crear el archivo PDF con campos de formulario: {ex.Message}", ex);
        }
    }
}