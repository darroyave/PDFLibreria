using PDFDanNativo.Core;
using PDFDanNativo.Models;

namespace PDFDanNativo.Testing
{
    [TestClass]
    public sealed class TestCreatePdfFromText
    {
        private readonly IPDFCore _pdfCore;
        private readonly IPDFCreateFromTextNativo _pdfNativo;

        public TestCreatePdfFromText()
        {
            _pdfCore = new PDFCore();
            _pdfNativo = new PDFCreateFromTextNativo(_pdfCore);
        }


        [TestMethod]
        public void TestMethodCreatePdfFromText()
        {
            // Arrange

            string filePath = "PaginaPDF2.pdf";
            string texto = "Pagina 2";

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
            Assert.IsTrue(_pdfCore.IsValidPdf("PaginaPDF2.pdf"), "El PDF creado no es válido.");

            Assert.AreEqual(1, _pdfCore.GetPageCount("PaginaPDF2.pdf"), "El número de páginas no es correcto.");

        }
    }
}
