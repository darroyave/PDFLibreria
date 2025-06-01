namespace PDFDanNativo.Models;

/// <summary>
/// Configuración para la generación del PDF
/// </summary>
public class PDFConfig
{
    /// <summary>
    /// Tamaño de página del PDF
    /// </summary>
    public PageSize PageSize { get; set; } = PageSize.A4;

    /// <summary>
    /// Orientación de la página
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>
    /// Márgenes de la página en puntos (1 punto = 1/72 pulgada)
    /// </summary>
    public Margins Margins { get; set; } = new Margins(72, 72, 72, 72); // 1 pulgada por defecto

    /// <summary>
    /// Nombre de la fuente a utilizar
    /// </summary>
    public string FontName { get; set; } = "Helvetica";

    /// <summary>
    /// Tamaño de la fuente en puntos
    /// </summary>
    public float FontSize { get; set; } = 12;

    /// <summary>
    /// Metadatos del documento PDF
    /// </summary>
    public PDFMetadata Metadata { get; set; } = new PDFMetadata();
}

