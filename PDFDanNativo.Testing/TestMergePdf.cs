using PDFDanNativo.Models;

namespace PDFDanNativo.Testing
{
    [TestClass]
    public sealed class TestMergePdf
    {
        private readonly IPDFCore _pdfCore;
        private readonly IPDFMergeNativo _pdfNativo;

        public TestMergePdf()
        {
            _pdfCore = new PDFCore();
            _pdfNativo = new PDFMergeNativo(_pdfCore);
        }


        [TestMethod]
        public void TestMethodMergePdf()
        {
            // Arrange

            string filePath1 = "PaginaPDF1.pdf";
            string filePath2 = "PaginaPDF2.pdf";
            string filePath3 = "MergedPDF.pdf";

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

            _pdfNativo.MergePdfs(filePath1, filePath2, filePath3, config);

            // Assert
            Assert.IsTrue(_pdfCore.IsValidPdf("MergedPDF.pdf"), "El PDF creado no es válido.");

            Assert.AreEqual(2, _pdfCore.GetPageCount("MergedPDF.pdf"), "El número de páginas no es correcto.");

        }
    }
}
