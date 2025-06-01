namespace TestProject1;

[TestClass]
public sealed class TestNativePDF
{
    private readonly INativePDF _nativePDF;

    public TestNativePDF()
    {
        _nativePDF = new NativePDF();
    }

    [TestMethod]
    public void TestMethodCreatePDF()
    {
        _nativePDF.CreatePdfFromText("HolaPDF.pdf", "Hola Mundo");

        Assert.IsTrue(_nativePDF.IsValidPdf("HolaPDF.pdf"), "El PDF creado no es válido.");
        Assert.IsTrue(_nativePDF.GetPageCount("HolaPDF.pdf") == 1, "El número de páginas no es correcto." );
    }

    [TestMethod]
    public void TestMethodMergePDF()
    {
        _nativePDF.MergePdfs("HolaPDF1.pdf", "HolaPDF2.pdf", "unido.pdf");

        Assert.IsTrue(_nativePDF.IsValidPdf("unido.pdf"), "El PDF creado no es válido.");
        Assert.IsTrue(_nativePDF.GetPageCount("unido.pdf") == 2, "El número de páginas no es correcto.");
    }

    [TestMethod]
    public void TestMethodCreatePdfWithFormFields()
    {
        _nativePDF.CreatePdfWithFormFields("formfields.pdf");

        Assert.IsTrue(_nativePDF.IsValidPdf("formfields.pdf"), "El PDF creado no es válido.");
        Assert.IsTrue(_nativePDF.GetPageCount("formfields.pdf") == 1, "El número de páginas no es correcto.");
    }

    [TestMethod]
    public void TestMethodGetPdfFormFieldNames()
    {
        var list = _nativePDF.GetPdfFormFieldNames("formfields.pdf");

        Assert.IsTrue(list.Count == 2, "El número de campos de formulario no es correcto.");
    }

    [TestMethod]
    public void TestMethodFlattenPdfFormFields()
    {
        _nativePDF.FlattenPdfFormFields(
            "formfields.pdf",
            "formfields_flat.pdf",
            new Dictionary<string, string> {
                { "Document", "98589982" },
                { "Name", "Dannover Arroyave M." }
            });

        Assert.IsTrue(_nativePDF.IsValidPdf("formfields_flat.pdf"), "El PDF creado no es válido.");
    }

    [TestMethod]
    public void TestMethodAttachFileToPdf()
    {
        _nativePDF.AttachFileToPdf("HolaPDF1.pdf", "archivo.txt", "pdf_con_adjunto.pdf");

        Assert.IsTrue(_nativePDF.IsValidPdf("pdf_con_adjunto.pdf"), "El PDF creado no es válido.");
    }

    [TestMethod]
    public void TestMethodCreatePdfWithImageCentered()
    {
        _nativePDF.CreatePdfWithImageCentered("pdf_con_imagen.pdf", "thumb.jpg");

        Assert.IsTrue(_nativePDF.IsValidPdf("pdf_con_imagen.pdf"), "El PDF creado no es válido.");
    }

    [TestMethod]
    public void TestMethodAddCenteredImageToPdf()
    {
        _nativePDF.AddCenteredImageToPdf("HolaPDF1.pdf", "thumb.jpg", "pdf_con_imagen2.pdf");

        Assert.IsTrue(_nativePDF.IsValidPdf("pdf_con_imagen2.pdf"), "El PDF creado no es válido.");
    }

    [TestMethod]
    public void TestMethodEncryptPdf()
    {
        _nativePDF.EncryptPdf("HolaPDF1.pdf", "encriptado.pdf", "1234");

        Assert.IsTrue(_nativePDF.IsValidPdf("encriptado.pdf"), "El PDF creado no es válido.");
    }
}
