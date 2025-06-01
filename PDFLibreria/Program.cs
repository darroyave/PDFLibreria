namespace PDFLibreria;

class Program
{
    static void Main(string[] args)
    {

        string pdf1 = "c:\\Developer\\.Net Beta\\PDFLibreria\\PDFLibreria\\dummy1.pdf";
        string pdf2 = "c:\\Developer\\.Net Beta\\PDFLibreria\\PDFLibreria\\dummy2.pdf";
        string output = "c:\\Developer\\.Net Beta\\PDFLibreria\\PDFLibreria\\unido.pdf";

        var pdfMerger = new NativePDF();
        
        Console.WriteLine("=== Iniciando prueba de merge de PDFs ===");
        bool success = pdfMerger.TestMergePDF(pdf1, pdf2, output);
        
        if (success)
        {
            Console.WriteLine("=== Merge completado exitosamente ===");
        }
        else
        {
            Console.WriteLine("=== El merge falló ===");
        }
    }
}
