namespace PDFDanNativo.Models;

/// <summary>
/// Márgenes de la página
/// </summary>
public class Margins
{
    public float Left { get; set; }
    public float Right { get; set; }
    public float Top { get; set; }
    public float Bottom { get; set; }

    public Margins(float left, float right, float top, float bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }
}