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
<<<<<<< HEAD
            string filePath = "HolaPDF.pdf";
            string texto = "Hola PDF.";

            // Create PDF from text
=======
            string filePath = "test.pdf";
            string texto = "Este es un texto de prueba para generar un PDF.";

>>>>>>> 8ff81c719867b917629e3b005760c24b88e046ec
            _pdfNativo.CreatePdfFromText(filePath, texto);

            // Assert
            Assert.IsTrue(_pdfCore.IsValidPdf("HolaPDF.pdf"), "El PDF creado no es válido.");
<<<<<<< HEAD
            Assert.AreEqual(1, _pdfCore.GetPageCount("HolaPDF.pdf"), "El número de páginas no es correcto.");
=======
            Assert.IsTrue(_pdfCore.GetPageCount("HolaPDF.pdf") == 1, "El número de páginas no es correcto.");

>>>>>>> 8ff81c719867b917629e3b005760c24b88e046ec
        }
    }
}
