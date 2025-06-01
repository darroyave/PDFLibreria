namespace PDFLibreria.Services;

public static class PdfFileUtils
{
    // ReadPdfBytes, FindObjects, FindXref, FindTrailer methods from previous steps
    // These are kept for continuity but new Merge logic has its own parsing.
    public static byte[]? ReadPdfBytes(string filePath)
    {
        try
        {
            return File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
            return null;
        }
    }
}