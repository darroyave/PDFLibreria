using PDFDanNativo.Core;

namespace PDFDanNativo.Testing
{
    [TestClass]
    public sealed class TestFillPdfWithFormFields
    {
        private readonly IPDFCore _pdfCore;
        private readonly IPDFFillWithFormFieldsNativo _pdfNativo;

        public TestFillPdfWithFormFields()
        {
            _pdfCore = new PDFCore();
            _pdfNativo = new PDFFillWithFormFieldsNativo(_pdfCore);
        }


        [TestMethod]
        public void TestMethodCreatePdfWithFormFields()
        {
            // Arrange

            string filePath = "PaginaWithFormFieldsPDF.pdf";
            string fileOutputPath = "FillPDF.pdf";

            _pdfNativo.FillPdfWithFormFields(filePath, fileOutputPath, 
                new Dictionary<string, string>
                {
                    { "Document", "CC 98589982" },
                    { "Name", "Dannover Arroyave M." }
                });
            
            // Assert
            Assert.IsTrue(_pdfCore.IsValidPdf("FillPDF.pdf"), "El PDF creado no es válido.");

            Assert.AreEqual(1, _pdfCore.GetPageCount("FillPDF.pdf"), "El número de páginas no es correcto.");

        }
    }
}
