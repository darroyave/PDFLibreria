namespace PDFLibreria.Models;

public class PDFDocument
{
    public string Header { get; set; }
    public List<PDFObject> Objects { get; set; }
    public PDFObject Root { get; set; }
    public PDFObject Pages { get; set; }
    public List<PDFObject> PageObjects { get; set; }
    public string Version { get; set; }
    public int PageCount => PageObjects.Count;

    public PDFDocument()
    {
        Objects = new List<PDFObject>();
        PageObjects = new List<PDFObject>();
        Version = "";
        Header = "";
    }
} 