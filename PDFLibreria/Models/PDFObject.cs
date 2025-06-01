namespace PDFLibreria.Models;

public class PDFObject
{
    public int ObjectNumber { get; set; }
    public int GenerationNumber { get; set; }
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public byte[]? RawContent { get; set; }  // Para contenido comprimido
    public bool IsStream { get; set; }
    public Dictionary<string, string> Dictionary { get; set; } = new Dictionary<string, string>();
    public List<int> References { get; set; } = new List<int>();
    public long Offset { get; set; }

    public PDFObject()
    {
        Dictionary = new Dictionary<string, string>();
        References = new List<int>();
        Type = "";
        Content = "";
    }
}