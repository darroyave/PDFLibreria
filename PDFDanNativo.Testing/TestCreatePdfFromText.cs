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
            string filePath = "test.pdf";
            string texto = "Este es un texto de prueba para generar un PDF.";

            _pdfNativo.CreatePdfFromText(filePath, texto);

            // Assert
            Assert.IsTrue(_pdfCore.IsValidPdf("HolaPDF.pdf"), "El PDF creado no es válido.");
            Assert.IsTrue(_pdfCore.GetPageCount("HolaPDF.pdf") == 1, "El número de páginas no es correcto.");

        }
    }
}
