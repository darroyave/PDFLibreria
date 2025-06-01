using PDFLibreria.Services;

namespace PDFLibreria;

public static class PDFTestUtils
{
    public static bool TestMergePDF(NativePDF pdfMerger, string pdfPath1, string pdfPath2, string outputPath)
    {
        try
        {
            Console.WriteLine("Iniciando prueba de merge de PDFs...");

            // Verificar que los archivos existen
            if (!File.Exists(pdfPath1))
            {
                Console.WriteLine($"Error: No se encuentra el archivo {pdfPath1}");
                return false;
            }
            if (!File.Exists(pdfPath2))
            {
                Console.WriteLine($"Error: No se encuentra el archivo {pdfPath2}");
                return false;
            }

            // Verificar que los archivos son PDFs válidos
            if (!PdfValidationUtils.IsValidPDF(pdfPath1))
            {
                Console.WriteLine($"Error: {pdfPath1} no es un PDF válido");
                return false;
            }
            if (!PdfValidationUtils.IsValidPDF(pdfPath2))
            {
                Console.WriteLine($"Error: {pdfPath2} no es un PDF válido");
                return false;
            }

            // Obtener información de los PDFs originales
            var pdf1Info = PdfValidationUtils.GetPDFInfo(pdfPath1);
            var pdf2Info = PdfValidationUtils.GetPDFInfo(pdfPath2);

            Console.WriteLine($"PDF1: {pdf1Info.PageCount} páginas, {pdf1Info.ObjectCount} objetos");
            Console.WriteLine($"PDF2: {pdf2Info.PageCount} páginas, {pdf2Info.ObjectCount} objetos");

            // Realizar el merge
            pdfMerger.MergePDF(pdfPath1, pdfPath2, outputPath);

            // Verificar que el archivo resultante existe
            if (!File.Exists(outputPath))
            {
                Console.WriteLine("Error: No se generó el archivo de salida");
                return false;
            }

            // Verificar que el PDF resultante es válido
            if (!PdfValidationUtils.IsValidPDF(outputPath))
            {
                Console.WriteLine("Error: El PDF resultante no es válido");
                return false;
            }

            // Obtener información del PDF resultante
            var resultInfo = PdfValidationUtils.GetPDFInfo(outputPath);
            Console.WriteLine($"PDF Resultante: {resultInfo.PageCount} páginas, {resultInfo.ObjectCount} objetos");

            // Verificar que el número de páginas es correcto
            if (resultInfo.PageCount != pdf1Info.PageCount + pdf2Info.PageCount)
            {
                Console.WriteLine($"Error: El número de páginas no coincide. Esperado: {pdf1Info.PageCount + pdf2Info.PageCount}, Obtenido: {resultInfo.PageCount}");
                return false;
            }

            Console.WriteLine("Prueba completada exitosamente");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error durante la prueba: {ex.Message}");
            return false;
        }
    }

    public static bool TestCreatePDF(NativePDF pdfMerger, string outputPath)
    {
        try
        {
            Console.WriteLine("Iniciando prueba de create PDF...");


            // Realizar el merge
            pdfMerger.CreatePdfFromText("Hola PDF", outputPath);

            // Verificar que el archivo resultante existe
            if (!File.Exists(outputPath))
            {
                Console.WriteLine("Error: No se generó el archivo de salida");
                return false;
            }

            // Verificar que el PDF resultante es válido
            if (!PdfValidationUtils.IsValidPDF(outputPath))
            {
                Console.WriteLine("Error: El PDF resultante no es válido");
                return false;
            }

            // Obtener información del PDF resultante
            var resultInfo = PdfValidationUtils.GetPDFInfo(outputPath);
            Console.WriteLine($"PDF Resultante: {resultInfo.PageCount} páginas, {resultInfo.ObjectCount} objetos");

            Console.WriteLine("Prueba completada exitosamente");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error durante la prueba: {ex.Message}");
            return false;
        }
    }
}