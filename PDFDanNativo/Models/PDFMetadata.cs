namespace PDFDanNativo.Models;

/// <summary>
/// Metadatos del documento PDF
/// </summary>
public class PDFMetadata
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Creator { get; set; } = "darroyave";
    public DateTime CreationDate { get; set; } = DateTime.Now;
}