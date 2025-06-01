using PDFDanNativo.Models;

namespace PDFDanNativo.Testing
{
    [TestClass]
    public sealed class TestCreatePdfFromText
    {
        private readonly IPDFCore _pdfCore;
        private readonly IPDFCreateNativo _pdfNativo;

        public TestCreatePdfFromText()
        {
            _pdfCore = new PDFCore();
            _pdfNativo = new PDFCreateNativo(_pdfCore);
        }


        [TestMethod]
        public void TestMethodCreatePdfFromText()
        {
            // Arrange

            string filePath = "HolaPDF.pdf";
            string texto = "Hola PDF.";

            // Uso con configuración personalizada
            var config = new PDFConfig
            {
                PageSize = PageSize.Letter,
                Orientation = PageOrientation.Portrait,
                Margins = new Margins(36, 36, 36, 36), // 0.5 pulgadas
                FontName = "Times-Roman",
                FontSize = 14,
                Metadata = new PDFMetadata
                {
                    Title = "Mi Documento",
                    Author = "Dannover Arroyave M.",
                    Subject = "Documento de ejemplo",
                    Keywords = "PDF, ejemplo, personalización"
                }
            };

            _pdfNativo.CreatePdfFromText(filePath, texto, config);

            // Assert
            Assert.IsTrue(_pdfCore.IsValidPdf("HolaPDF.pdf"), "El PDF creado no es válido.");

            Assert.AreEqual(1, _pdfCore.GetPageCount("HolaPDF.pdf"), "El número de páginas no es correcto.");

        }
    }
}
