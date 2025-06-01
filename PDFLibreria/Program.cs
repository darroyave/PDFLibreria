namespace PDFLibreria;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Native PDF Merger Test");

        NativePDF merger = new NativePDF();

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dummy1Path = Path.Combine(baseDir, "dummy1.pdf");
        string dummy2Path = Path.Combine(baseDir, "dummy2.pdf");
        string outputPath = Path.Combine(baseDir, "merged_native.pdf");

        // Ensure dummy files exist (optional, but good for testing)
        if (!File.Exists(dummy1Path))
        {
            Console.WriteLine($"Error: {dummy1Path} not found. Make sure it's in the output directory (e.g., bin/Debug/netX.X).");
            // Attempt to locate them relative to project structure for easier local dev run
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            dummy1Path = Path.Combine(projectRoot, "dummy1.pdf"); // Corrected path
            dummy2Path = Path.Combine(projectRoot, "dummy2.pdf"); // Corrected path
             if (!File.Exists(dummy1Path)) {
                Console.WriteLine($"Error: Also not found at {dummy1Path}");
                return;
             }
             Console.WriteLine($"Found files in project directory: {dummy1Path}");

        }
         if (!File.Exists(dummy2Path))
        {
             Console.WriteLine($"Error: {dummy2Path} not found. Make sure it's in the output directory.");
             // Attempt to locate them relative to project structure
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            // dummy2Path is already corrected by the logic for dummy1Path if it's reached,
            // but for clarity and robustness, ensure it's also explicitly corrected here
            // if the initial check for dummy1Path passed but dummy2Path failed.
            dummy2Path = Path.Combine(projectRoot, "dummy2.pdf"); // Corrected path
             if (!File.Exists(dummy2Path)) {
                Console.WriteLine($"Error: Also not found at {dummy2Path}");
                return;
             }
        }


        Console.WriteLine($"Input PDF 1: {dummy1Path}");
        Console.WriteLine($"Input PDF 2: {dummy2Path}");
        Console.WriteLine($"Output PDF: {outputPath}");

        merger.MergePDF(dummy1Path, dummy2Path, outputPath);

        Console.WriteLine("Native merge process finished. Check the output file.");
    }
}
