using PDFDanNativo.Core;
using PDFDanNativo.Models;

namespace PDFDanNativo.Testing
{
    [TestClass]
    public sealed class TestCreatePdfWithFormFields
    {
        private readonly IPDFCore _pdfCore;
        private readonly IPDFCreateWithFormFieldsNativo _pdfNativo;

        public TestCreatePdfWithFormFields()
        {
            _pdfCore = new PDFCore();
            _pdfNativo = new PDFCreateWithFormFieldsNativo(_pdfCore);
        }


        [TestMethod]
        public void TestMethodCreatePdfWithFormFields()
        {
            // Arrange

            string filePath = "PaginaWithFormFieldsPDF.pdf";

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

            _pdfNativo.CreatePdfWithFormFields(filePath, ["Document", "Name"], config);
            
            // Assert
            Assert.IsTrue(_pdfCore.IsValidPdf("PaginaWithFormFieldsPDF.pdf"), "El PDF creado no es válido.");

            Assert.AreEqual(1, _pdfCore.GetPageCount("PaginaWithFormFieldsPDF.pdf"), "El número de páginas no es correcto.");

        }
    }
}
