namespace PDFLibreria;

class Program
{
    static void Main(string[] args)
    {

        string pdf1 = "c:\\Developer\\.Net Beta\\PDFLibreria\\PDFLibreria\\samples\\dummy1.pdf";
        string pdf2 = "c:\\Developer\\.Net Beta\\PDFLibreria\\PDFLibreria\\samples\\dummy2.pdf";
        string output = "c:\\Developer\\.Net Beta\\PDFLibreria\\PDFLibreria\\samples\\unido.pdf";

        var nativePDF = new NativePDF();

        /*
        Console.WriteLine("=== Iniciando prueba de merge de PDFs ===");
        bool success = PDFTestUtils.TestMergePDF(nativePDF, pdf1, pdf2, output);
        
        if (success)
        {
            Console.WriteLine("=== Merge completado exitosamente ===");
        }
        else
        {
            Console.WriteLine("=== El merge falló ===");
        }
        */

        bool success = PDFTestUtils.TestCreatePDF(nativePDF, output);
        if (success)
        {
            Console.WriteLine("=== Create completado exitosamente ===");
        }
        else
        {
            Console.WriteLine("=== El create falló ===");
        }
    }
}
