using PDFDanNativo.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace PDFDanNativo;

public interface IPDFFillWithFormFieldsNativo
{
    /// <summary>
    /// Rellena los campos de un formulario PDF existente con los valores proporcionados
    /// </summary>
    /// <param name="inputPath">Ruta del archivo PDF de entrada con campos de formulario</param>
    /// <param name="outputPath">Ruta donde se guardará el PDF con los campos rellenados</param>
    /// <param name="fields">Diccionario con los nombres de los campos y sus valores</param>
    /// <param name="config">Configuración opcional para personalizar el PDF</param>
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

            foreach (var kv in fields)
            {
                string fieldName = kv.Key;
                string fieldValue = kv.Value.Replace("(", "\\(").Replace(")", "\\)");
                int tIndex = pdf.IndexOf($"/T ({fieldName})");
                if (tIndex == -1) continue;

                // Busca inicio y fin del objeto
                int objStart = pdf.LastIndexOf("obj", tIndex);
                int objEnd = pdf.IndexOf("endobj", tIndex);
                if (objStart == -1 || objEnd == -1) continue;

                // Extrae el objeto
                string objText = pdf.Substring(objStart, objEnd + 6 - objStart);

                // Encuentra la posición del campo (Rect [x1 y1 x2 y2])
                var rectMatch = Regex.Match(objText, @"\/Rect\s*\[([^\]]+)\]");
                string rect = rectMatch.Success ? rectMatch.Groups[1].Value : null;

                // Borra el objeto del PDF
                pdf = pdf.Remove(objStart, objEnd + 6 - objStart);

                // Ahora agrega el texto en el contenido (objeto 4 0 obj en nuestro PDF)
                // (Sólo funciona para PDFs generados por nuestra clase)
                string contentObj = "4 0 obj";
                int contIdx = pdf.IndexOf(contentObj);
                int streamIdx = pdf.IndexOf("stream", contIdx);
                int endstreamIdx = pdf.IndexOf("endstream", contIdx);
                if (streamIdx != -1 && endstreamIdx != -1)
                {
                    // Usamos la posición Y del campo Rect
                    string[] rectVals = rect?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int x = 130, y = 150; // default
                    if (rectVals != null && rectVals.Length >= 4)
                    {
                        int.TryParse(rectVals[0], out x);
                        int.TryParse(rectVals[1], out y);
                    }

                    string newText = $"\nBT /F1 12 Tf {x + 5} {y + 2} Td ({fieldValue}) Tj ET";
                    pdf = pdf.Insert(streamIdx + 6, newText);
                }
            }

            File.WriteAllText(outputPath, pdf, Encoding.ASCII);

        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new IOException($"Error al rellenar los campos del formulario PDF: {ex.Message}", ex);
        }
    }
}