using System;

namespace PDFLibreria;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Uso: PDFLibreria.exe <pdf1> <pdf2> <output>");
            return;
        }

        string pdf1 = args[0];
        string pdf2 = args[1];
        string output = args[2];

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
