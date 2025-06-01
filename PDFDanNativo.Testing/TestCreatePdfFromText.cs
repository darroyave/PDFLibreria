namespace PDFDanNativo.Testing
{
    [TestClass]
    public sealed class TestCreatePdfFromText
    {
        private readonly IPDFCore _pdfCore;
        private readonly IPDFNativo _pdfNativo;

        public TestCreatePdfFromText()
        {
            _pdfCore = new PDFCore();
            _pdfNativo = new PDFNativo(_pdfCore);
        }


        [TestMethod]
        public void TestMethodCreatePdfFromText()
        {
            // Arrange
            string filePath = "HolaPDF.pdf";
            string texto = "Hola PDF.";

            // Create PDF from text
            _pdfNativo.CreatePdfFromText(filePath, texto);

            // Assert
            Assert.IsTrue(_pdfCore.IsValidPdf("HolaPDF.pdf"), "El PDF creado no es válido.");
            Assert.AreEqual(1, _pdfCore.GetPageCount("HolaPDF.pdf"), "El número de páginas no es correcto.");
        }
    }
}
