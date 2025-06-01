namespace PDFDanNativo;

public interface IPDFNativo
{
    void CreatePdfFromText(string filePath, string texto);
}

public class PDFNativo(
    IPDFCore pdfCore) : IPDFNativo
{
    public void CreatePdfFromText(string filePath, string texto)
    {
        // 1. CABECERA
        string header = pdfCore.BuildPdfHeader();

        // 2. CUERPO
        var bodyObjects = pdfCore.BuildPdfBodyObjects(texto);

        // 3. TABLA DE REFERENCIA CRUZADA (xref)
        var contentParts = new[] { header }.Concat(bodyObjects).ToArray();
        int[] offsets = pdfCore.CalculateOffsets(contentParts);
        string xref = pdfCore.BuildXrefTable(contentParts, offsets);

        // 4. TRAILER
        string trailer = pdfCore.BuildTrailer(contentParts.Length, offsets[^1]);

        // Escribir todo al archivo, por partes y comentado
        pdfCore.WritePdfFile(filePath, header, bodyObjects, xref, trailer);
    }

}