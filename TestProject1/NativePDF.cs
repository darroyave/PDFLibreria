using System;
using System.Security.Cryptography;
using System.Text;

namespace TestProject1;

public interface INativePDF
{
    void CreatePdfFromText(string filePath, string texto);
    bool IsValidPdf(string filePath);
    int GetPageCount(string filePath);
    void MergePdfs(string pdf1Path, string pdf2Path, string outputPath);
    void CreatePdfWithFormFields(string filePath);
    List<string> GetPdfFormFieldNames(string filePath);
    void FlattenPdfFormFields(string inputPath, string outputPath, Dictionary<string, string> values);
    void AttachFileToPdf(string pdfPath, string txtPath, string outputPdfPath);
    void CreatePdfWithImageCentered(string pdfPath, string imagePath);
    void AddCenteredImageToPdf(string inputPdf, string imagePath, string outputPdf);
    void EncryptPdf(string inputPdf, string outputPdf, string userPassword, string ownerPassword = "owner");
}

public class NativePDF: INativePDF
{
    public void CreatePdfFromText(string filePath, string texto)
    {
        string header = "%PDF-1.4\n";
        string obj1 = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
        string obj2 = "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n";
        string obj3 = "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n";
        
        // Escapamos paréntesis si el texto los trae
        string textEscaped = texto.Replace("(", "\\(").Replace(")", "\\)");
        string obj4_stream = $"BT\n70 100 TD\n/F1 24 Tf\n({textEscaped}) Tj\nET";
        string obj4 = $"4 0 obj\n<< /Length {obj4_stream.Length} >>\nstream\n{obj4_stream}\nendstream\nendobj\n";
        string obj5 = "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n";

        // Calcula los offsets
        var contentList = new[] { header, obj1, obj2, obj3, obj4, obj5 };
        var offsets = new int[contentList.Length + 1];
        int offset = 0;
        offsets[0] = 0;
        for (int i = 0; i < contentList.Length; i++)
        {
            offset += Encoding.ASCII.GetByteCount(contentList[i]);
            offsets[i + 1] = offset;
        }

        // Construye el xref dinámicamente
        StringBuilder xref = new StringBuilder();
        xref.AppendLine("xref");
        xref.AppendLine($"0 {contentList.Length + 1}");
        xref.AppendLine("0000000000 65535 f "); // objeto 0 siempre es free
        for (int i = 1; i <= contentList.Length; i++)
        {
            xref.AppendLine($"{offsets[i - 1]:D10} 00000 n ");
        }

        // Trailer y startxref
        string trailer = $"trailer\n<< /Size {contentList.Length + 1} /Root 1 0 R >>\nstartxref\n{offset}\n%%EOF\n";

        // Escritura
        using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        using (StreamWriter sw = new StreamWriter(fs, Encoding.ASCII))
        {
            foreach (var part in contentList)
                sw.Write(part);
            sw.Write(xref.ToString());
            sw.Write(trailer);
        }
    }

    public bool IsValidPdf(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        byte[] header = new byte[5];
        try
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Leer los primeros 5 bytes
                if (fs.Read(header, 0, 5) != 5)
                    return false;

                string headerStr = Encoding.ASCII.GetString(header);
                if (!headerStr.StartsWith("%PDF-"))
                    return false;

                // Leer los últimos 20 bytes para buscar %%EOF
                fs.Seek(-20, SeekOrigin.End);
                byte[] tail = new byte[20];
                fs.Read(tail, 0, 20);
                string tailStr = Encoding.ASCII.GetString(tail);

                return tailStr.Contains("%%EOF");
            }
        }
        catch
        {
            return false;
        }
    }

    public int GetPageCount(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("El archivo PDF no existe.", filePath);

        string contenido;
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (StreamReader sr = new StreamReader(fs, Encoding.ASCII))
        {
            contenido = sr.ReadToEnd();
        }

        var idx = contenido.IndexOf("/Count");
        if (idx == -1)
            return -1; // No se encontró

        idx += "/Count".Length;
        while (idx < contenido.Length && (contenido[idx] == ' ' || contenido[idx] == '\t'))
            idx++;

        StringBuilder sb = new StringBuilder();
        while (idx < contenido.Length && char.IsDigit(contenido[idx]))
        {
            sb.Append(contenido[idx]);
            idx++;
        }

        if (int.TryParse(sb.ToString(), out int count))
            return count;

        return -1; // No se pudo extraer el número
    }

    // Helper para extraer objetos PDF sencillos, uno por uno
   
    public void MergePdfs(string pdf1Path, string pdf2Path, string outputPath)
    {
        string pdf1 = File.ReadAllText(pdf1Path, Encoding.ASCII);
        string pdf2 = File.ReadAllText(pdf2Path, Encoding.ASCII);

        string[] objs1 = NativePDFHelper.ExtractPdfObjects(pdf1, 5);
        string[] objs2 = NativePDFHelper.ExtractPdfObjects(pdf2, 5);

        // Aseguramos salto de línea al final de cada objeto
        for (int i = 0; i < objs1.Length; i++)
            if (!objs1[i].EndsWith("\n")) objs1[i] += "\n";
        for (int i = 0; i < objs2.Length; i++)
            if (!objs2[i].EndsWith("\n")) objs2[i] += "\n";

        // Objetos nuevos
        StringBuilder[] objects = new StringBuilder[8];

        // 1 0 obj: Catalog
        objects[0] = new StringBuilder("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // 2 0 obj: Pages con 2 hijos
        objects[1] = new StringBuilder("2 0 obj\n<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>\nendobj\n");

        // 3 0 obj: Page 1 (ajusta /Parent, /Contents y /Font)
        objects[2] = new StringBuilder(objs1[2]
            .Replace("3 0 obj", "3 0 obj")
            .Replace("/Parent 2 0 R", "/Parent 2 0 R")
            .Replace("/Contents 4 0 R", "/Contents 4 0 R")
            .Replace("/F1 5 0 R", "/F1 5 0 R")
        );

        // 4 0 obj: Contents 1
        objects[3] = new StringBuilder(objs1[3].Replace("4 0 obj", "4 0 obj"));

        // 5 0 obj: Font 1
        objects[4] = new StringBuilder(objs1[4].Replace("5 0 obj", "5 0 obj"));

        // 6 0 obj: Page 2 (ajusta referencias a los objetos correctos)
        objects[5] = new StringBuilder(objs2[2]
            .Replace("3 0 obj", "6 0 obj")
            .Replace("/Parent 2 0 R", "/Parent 2 0 R")
            .Replace("/Contents 4 0 R", "/Contents 7 0 R")
            .Replace("/F1 5 0 R", "/F1 8 0 R")
        );

        // 7 0 obj: Contents 2
        objects[6] = new StringBuilder(objs2[3].Replace("4 0 obj", "7 0 obj"));

        // 8 0 obj: Font 2
        objects[7] = new StringBuilder(objs2[4].Replace("5 0 obj", "8 0 obj"));

        // Construir el PDF completo
        StringBuilder pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");

        // Calcula offsets
        int[] offsets = new int[objects.Length + 1];
        offsets[0] = pdf.Length;
        for (int i = 0; i < objects.Length; i++)
        {
            pdf.Append(objects[i]);
            offsets[i + 1] = pdf.Length;
        }

        // xref
        int xrefPosition = pdf.Length;
        pdf.Append("xref\n");
        pdf.AppendFormat("0 {0}\n", objects.Length + 1);
        pdf.Append("0000000000 65535 f \n");
        for (int i = 1; i <= objects.Length; i++)
            pdf.AppendFormat("{0:D10} 00000 n \n", offsets[i - 1]);

        // trailer
        pdf.Append("trailer\n");
        pdf.AppendFormat("<< /Size {0} /Root 1 0 R >>\n", objects.Length + 1);
        pdf.Append("startxref\n");
        pdf.AppendFormat("{0}\n", xrefPosition);
        pdf.Append("%%EOF\n");

        File.WriteAllText(outputPath, pdf.ToString(), Encoding.ASCII);
    }

    public void CreatePdfWithFormFields(string filePath)
    {
        // OBJETOS
        string header = "%PDF-1.4\n";
        // 1 0 obj: Catalog
        string obj1 = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R /AcroForm 9 0 R >>\nendobj\n";
        // 2 0 obj: Pages
        string obj2 = "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n";
        // 3 0 obj: Page
        string obj3 = "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\n/Annots [6 0 R 7 0 R]\nendobj\n";
        // 4 0 obj: Contents (posiciona los textos "Document:" y "Name:")
        string stream = "BT /F1 12 Tf 50 160 Td (Document:) Tj ET\nBT /F1 12 Tf 50 110 Td (Name:) Tj ET";
        string obj4 = $"4 0 obj\n<< /Length {stream.Length} >>\nstream\n{stream}\nendstream\nendobj\n";
        // 5 0 obj: Fuente
        string obj5 = "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n";
        // 6 0 obj: Form Field Widget para "Document"
        string obj6 = "6 0 obj\n<< /Type /Annot /Subtype /Widget /Rect [130 150 350 170] /FT /Tx /T (Document) /F 4 /V () /DA (/F1 12 Tf 0 g) /P 3 0 R >>\nendobj\n";
        // 7 0 obj: Form Field Widget para "Name"
        string obj7 = "7 0 obj\n<< /Type /Annot /Subtype /Widget /Rect [130 100 350 120] /FT /Tx /T (Name) /F 4 /V () /DA (/F1 12 Tf 0 g) /P 3 0 R >>\nendobj\n";
        // 8 0 obj: Fields Array
        string obj8 = "8 0 obj\n[6 0 R 7 0 R]\nendobj\n";
        // 9 0 obj: AcroForm
        string obj9 = "9 0 obj\n<< /Fields 8 0 R >>\nendobj\n";

        var contentList = new[] { header, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8, obj9 };
        var offsets = new int[contentList.Length + 1];
        int offset = 0;
        offsets[0] = 0;
        for (int i = 0; i < contentList.Length; i++)
        {
            offset += Encoding.ASCII.GetByteCount(contentList[i]);
            offsets[i + 1] = offset;
        }

        // Construir xref
        StringBuilder xref = new StringBuilder();
        xref.AppendLine("xref");
        xref.AppendLine($"0 {contentList.Length}");
        xref.AppendLine("0000000000 65535 f ");
        for (int i = 1; i < contentList.Length; i++)
            xref.AppendLine($"{offsets[i - 1]:D10} 00000 n ");

        string trailer = $"trailer\n<< /Size {contentList.Length} /Root 1 0 R >>\nstartxref\n{offset}\n%%EOF\n";

        using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        using (StreamWriter sw = new StreamWriter(fs, Encoding.ASCII))
        {
            foreach (var part in contentList)
                sw.Write(part);
            sw.Write(xref.ToString());
            sw.Write(trailer);
        }
    }

    public List<string> GetPdfFormFieldNames(string filePath)
    {
        var fieldNames = new List<string>();
        var lines = File.ReadAllLines(filePath, Encoding.ASCII);

        foreach (var line in lines)
        {
            int idx = line.IndexOf("/T (");
            if (idx >= 0)
            {
                int start = idx + 4; // después de "/T ("
                int end = line.IndexOf(")", start);
                if (end > start)
                {
                    string name = line.Substring(start, end - start);
                    fieldNames.Add(name);
                }
            }
        }
        return fieldNames;
    }

    public void FlattenPdfFormFields(string inputPath, string outputPath, Dictionary<string, string> values)
    {
        string pdf = File.ReadAllText(inputPath, Encoding.ASCII);

        foreach (var kv in values)
        {
            string fieldName = kv.Key;
            string fieldValue = kv.Value.Replace("(", "\\(").Replace(")", "\\)");
            int tIndex = pdf.IndexOf($"/T ({fieldName})");
            if (tIndex == -1) continue;

            // Busca inicio y fin del objeto
            int objStart = pdf.LastIndexOf("obj", tIndex);
            int objEnd = pdf.IndexOf("endobj", tIndex);
            if (objStart == -1 || objEnd == -1) continue;

            // Extrae el objeto
            string objText = pdf.Substring(objStart, objEnd + 6 - objStart);

            // Encuentra la posición del campo (Rect [x1 y1 x2 y2])
            var rectMatch = System.Text.RegularExpressions.Regex.Match(objText, @"\/Rect\s*\[([^\]]+)\]");
            string rect = rectMatch.Success ? rectMatch.Groups[1].Value : null;

            // Borra el objeto del PDF
            pdf = pdf.Remove(objStart, objEnd + 6 - objStart);

            // Ahora agrega el texto en el contenido (objeto 4 0 obj en nuestro PDF)
            // (Sólo funciona para PDFs generados por nuestra clase)
            string contentObj = "4 0 obj";
            int contIdx = pdf.IndexOf(contentObj);
            int streamIdx = pdf.IndexOf("stream", contIdx);
            int endstreamIdx = pdf.IndexOf("endstream", contIdx);
            if (streamIdx != -1 && endstreamIdx != -1)
            {
                // Usamos la posición Y del campo Rect
                string[] rectVals = rect?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int x = 130, y = 150; // default
                if (rectVals != null && rectVals.Length >= 4)
                {
                    int.TryParse(rectVals[0], out x);
                    int.TryParse(rectVals[1], out y);
                }

                string newText = $"\nBT /F1 12 Tf {x + 5} {y + 2} Td ({fieldValue}) Tj ET";
                pdf = pdf.Insert(streamIdx + 6, newText);
            }
        }

        File.WriteAllText(outputPath, pdf, Encoding.ASCII);
    }

    public void AttachFileToPdf(string pdfPath, string txtPath, string outputPdfPath)
    {
        string pdf = File.ReadAllText(pdfPath, Encoding.ASCII);
        byte[] fileBytes = File.ReadAllBytes(txtPath);
        string fileName = Path.GetFileName(txtPath);

        var objects = NativePDFHelper.ExtractAllPdfObjects(pdf);

        // === Paso 1: Objeto EmbeddedFile (objX)
        int objNum_EmbeddedFile = objects.Count + 1;
        string fileStreamObj =
            $"{objNum_EmbeddedFile} 0 obj\n" +
            $"<< /Type /EmbeddedFile /Length {fileBytes.Length} >>\n" +
            "stream\n" +
            Encoding.ASCII.GetString(fileBytes) + "\n" +
            "endstream\nendobj\n";

        // === Paso 2: FileSpec (objX+1)
        int objNum_FileSpec = objNum_EmbeddedFile + 1;
        string fileSpecObj =
            $"{objNum_FileSpec} 0 obj\n" +
            $"<< /Type /Filespec /F ({fileName}) /EF <</F {objNum_EmbeddedFile} 0 R>> >>\n" +
            "endobj\n";

        // === Paso 3: Names dictionary para adjuntos (objX+2)
        int objNum_Names = objNum_EmbeddedFile + 2;
        string namesObj =
            $"{objNum_Names} 0 obj\n" +
            $"<< /Names [({fileName}) {objNum_FileSpec} 0 R] >>\n" +
            "endobj\n";

        // === Paso 4: EmbeddedFiles dictionary (objX+3)
        int objNum_EmbeddedFiles = objNum_EmbeddedFile + 3;
        string embeddedFilesObj =
            $"{objNum_EmbeddedFiles} 0 obj\n" +
            $"<< /EmbeddedFiles {objNum_Names} 0 R >>\n" +
            "endobj\n";

        // === Paso 5: Modifica el Catálogo para agregar /Names {objX+3} 0 R
        // El objeto catálogo suele ser el primero (índice 0). Si no tiene /Names, lo agregamos.
        if (!objects[0].Contains("/Names"))
        {
            int insertAt = objects[0].IndexOf(">>");
            objects[0] = objects[0].Insert(insertAt, $" /Names {objNum_EmbeddedFiles} 0 R");
        }
        else
        {
            // Si ya hay /Names, esto es raro para nuestros PDFs, pero puedes expandir aquí si lo necesitas
        }

        // === Opcional: Añadir anotación visual (clip) en la página ===
        int objNum_Annot = objNum_EmbeddedFile + 4;
        string annotObj =
            $"{objNum_Annot} 0 obj\n" +
            "<< /Type /Annot /Subtype /FileAttachment\n" +
            "   /Rect [10 10 30 30]\n" +
            $"   /FS {objNum_FileSpec} 0 R\n" +
            $"   /Contents ({fileName})\n" +
            "   /Name PushPin\n" +
            "   /T (Adjunto)\n" +
            ">>\n" +
            "endobj\n";

        // Añadimos la anotación al objeto Page (índice 2)
        if (!objects[2].Contains("/Annots"))
        {
            int insert = objects[2].LastIndexOf("endobj");
            objects[2] = objects[2].Insert(insert, $" /Annots [{objNum_Annot} 0 R]\n");
        }
        else
        {
            int annotsStart = objects[2].IndexOf("/Annots [") + "/Annots [".Length;
            objects[2] = objects[2].Insert(annotsStart, $"{objNum_Annot} 0 R ");
        }

        // --- Agrega los objetos nuevos ---
        objects.Add(fileStreamObj);
        objects.Add(fileSpecObj);
        objects.Add(namesObj);
        objects.Add(embeddedFilesObj);
        objects.Add(annotObj);

        // === Reconstruye el PDF ===
        string header = "%PDF-1.4\n";
        var offsets = new int[objects.Count + 1];
        int offset = header.Length;
        offsets[0] = 0;
        StringBuilder pdfFinal = new StringBuilder(header);
        for (int i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = offset;
            pdfFinal.Append(objects[i]);
            offset = pdfFinal.Length;
        }

        int xrefPos = pdfFinal.Length;
        pdfFinal.Append("xref\n");
        pdfFinal.AppendFormat("0 {0}\n", objects.Count + 1);
        pdfFinal.Append("0000000000 65535 f \n");
        for (int i = 1; i <= objects.Count; i++)
            pdfFinal.AppendFormat("{0:D10} 00000 n \n", offsets[i]);

        pdfFinal.Append("trailer\n");
        pdfFinal.AppendFormat("<< /Size {0} /Root 1 0 R >>\n", objects.Count + 1);
        pdfFinal.Append("startxref\n");
        pdfFinal.AppendFormat("{0}\n", xrefPos);
        pdfFinal.Append("%%EOF\n");

        File.WriteAllText(outputPdfPath, pdfFinal.ToString(), Encoding.ASCII);
    }

    public void CreatePdfWithImageCentered(string pdfPath, string imagePath)
    {
        byte[] imgBytes = File.ReadAllBytes(imagePath);
        if (!NativePDFHelper.GetJpegDimensions(imgBytes, out int imgWidth, out int imgHeight))
            throw new Exception("No se pudieron leer las dimensiones del JPEG.");

        // Tamaño de la página y de la imagen en el PDF (ajusta a tu gusto)
        int pdfWidth = 500, pdfHeight = 700;
        float scale = Math.Min(pdfWidth / (float)imgWidth, pdfHeight / (float)imgHeight) * 0.8f; // 80% de la hoja
        int imgDisplayWidth = (int)(imgWidth * scale);
        int imgDisplayHeight = (int)(imgHeight * scale);
        int imgPosX = (pdfWidth - imgDisplayWidth) / 2;
        int imgPosY = (pdfHeight - imgDisplayHeight) / 2;

        string header = "%PDF-1.4\n";
        string obj1 = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
        string obj2 = "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n";
        string obj3 = $"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pdfWidth} {pdfHeight}] /Contents 4 0 R /Resources << /XObject <</Im1 5 0 R>> >> >>\nendobj\n";

        // El stream pinta la imagen centrada y escalada
        string contentStream = $@"q
            {imgDisplayWidth} 0 0 {imgDisplayHeight} {imgPosX} {imgPosY} cm
            /Im1 Do
            Q";
        string obj4 = $"4 0 obj\n<< /Length {contentStream.Length} >>\nstream\n{contentStream}\nendstream\nendobj\n";

        // El objeto imagen declara el tamaño REAL del JPEG
        string obj5 =
            $"5 0 obj\n" +
            $"<< /Type /XObject /Subtype /Image /Width {imgWidth} /Height {imgHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {imgBytes.Length} >>\n" +
            "stream\n";
        string obj5_end = "\nendstream\nendobj\n";

        var stringObjs = new[] { header, obj1, obj2, obj3, obj4, obj5 };
        var offsets = new int[stringObjs.Length + 1];
        int offset = 0;
        offsets[0] = 0;
        for (int i = 0; i < stringObjs.Length; i++)
        {
            offset += Encoding.ASCII.GetByteCount(stringObjs[i]);
            offsets[i + 1] = offset;
        }
        int imgOffset = offset;
        offset += imgBytes.Length + Encoding.ASCII.GetByteCount(obj5_end);
        offsets[6] = offset;

        using (var fs = new FileStream(pdfPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            // Header y objetos
            foreach (var s in stringObjs)
                bw.Write(Encoding.ASCII.GetBytes(s));
            // Imagen JPEG binaria
            bw.Write(imgBytes);
            // Terminar el objeto imagen
            bw.Write(Encoding.ASCII.GetBytes(obj5_end));
            // XREF
            int xrefPos = (int)fs.Position;
            bw.Write(Encoding.ASCII.GetBytes($"xref\n0 6\n0000000000 65535 f \n"));
            for (int i = 1; i <= 5; i++)
                bw.Write(Encoding.ASCII.GetBytes($"{offsets[i]:D10} 00000 n \n"));
            // Trailer
            bw.Write(Encoding.ASCII.GetBytes($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n"));
        }
    }

    public void AddCenteredImageToPdf(string inputPdf, string imagePath, string outputPdf)
    {
        string pdf = File.ReadAllText(inputPdf, Encoding.ASCII);
        byte[] imgBytes = File.ReadAllBytes(imagePath);
        if (!NativePDFHelper.GetJpegDimensions(imgBytes, out int imgWidth, out int imgHeight))
            throw new Exception("No se pudieron leer las dimensiones del JPEG.");

        var objects = NativePDFHelper.ExtractAllPdfObjects(pdf);

        // Encuentra el objeto Page
        int pageObjIdx = -1;
        int pageWidth = 0, pageHeight = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i].Contains("/MediaBox"))
            {
                pageObjIdx = i;
                var match = System.Text.RegularExpressions.Regex.Match(objects[i], @"/MediaBox\s*\[\s*\d+\s+\d+\s+(\d+)\s+(\d+)\s*\]");
                if (match.Success)
                {
                    pageWidth = int.Parse(match.Groups[1].Value);
                    pageHeight = int.Parse(match.Groups[2].Value);
                }
                break;
            }
        }
        if (pageObjIdx == -1)
            throw new Exception("No se encontró el objeto Page con /MediaBox.");

        // Encuentra el objeto de contenido original (/Contents x 0 R)
        int contentObjNum = 0;
        var matchContent = System.Text.RegularExpressions.Regex.Match(objects[pageObjIdx], @"/Contents\s+(\d+)\s+0\s+R");
        if (matchContent.Success)
            contentObjNum = int.Parse(matchContent.Groups[1].Value);
        else
            throw new Exception("No se encontró el objeto /Contents.");

        // Calcula escala y posición centrada
        float scale = Math.Min(pageWidth / (float)imgWidth, pageHeight / (float)imgHeight) * 0.8f; // 80%
        int imgDisplayWidth = (int)(imgWidth * scale);
        int imgDisplayHeight = (int)(imgHeight * scale);
        int imgPosX = (pageWidth - imgDisplayWidth) / 2;
        int imgPosY = (pageHeight - imgDisplayHeight) / 2;

        // NUEVO: El objeto imagen (XObject)
        int imgObjNum = objects.Count + 1;
        string imgObj =
            $"{imgObjNum} 0 obj\n" +
            $"<< /Type /XObject /Subtype /Image /Width {imgWidth} /Height {imgHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {imgBytes.Length} >>\n" +
            "stream\n";
        string imgObjEnd = "\nendstream\nendobj\n";

        // NUEVO: El objeto contenido que dibuja la imagen
        int newContentObjNum = objects.Count + 2;
        string contentStream = $@"q
            {imgDisplayWidth} 0 0 {imgDisplayHeight} {imgPosX} {imgPosY} cm
            /Im1 Do
            Q";
        string contentObj =
            $"{newContentObjNum} 0 obj\n<< /Length {contentStream.Length} >>\nstream\n{contentStream}\nendstream\nendobj\n";

        // Modifica el objeto Page:
        // 1. Agrega la imagen a /Resources /XObject <</Im1 x 0 R>>
        string pageObj = objects[pageObjIdx];
        if (pageObj.Contains("/XObject"))
        {
            pageObj = System.Text.RegularExpressions.Regex.Replace(
                pageObj,
                @"/XObject\s*<<",
                $"/XObject << /Im1 {imgObjNum} 0 R ");
        }
        else
        {
            pageObj = System.Text.RegularExpressions.Regex.Replace(
                pageObj,
                @"/Resources\s*<<",
                $"/Resources << /XObject <</Im1 {imgObjNum} 0 R>> ");
        }

        // 2. Haz que /Contents apunte a ambos streams, en un array
        //    (Asegura que NO borra el contenido previo, sino lo suma)
        pageObj = System.Text.RegularExpressions.Regex.Replace(
            pageObj,
            @"/Contents\s+\d+\s+0\s+R",
            $"/Contents [{contentObjNum} 0 R {newContentObjNum} 0 R]");

        objects[pageObjIdx] = pageObj;

        // Agrega los nuevos objetos
        objects.Add(imgObj);
        objects.Add(imgObjEnd);
        objects.Add(contentObj);

        // Reconstruye PDF (igual que antes)
        string header = "%PDF-1.4\n";
        List<byte[]> objectBytes = new List<byte[]>();
        int[] offsets = new int[objects.Count + 1];
        int offset = header.Length;
        offsets[0] = 0;

        for (int i = 0; i < objects.Count; i++)
        {
            // Si es nuestro objeto imagen, lo partimos antes de 'stream'
            if (i == objects.Count - 3)
            {
                int split = objects[i].IndexOf("stream\n") + "stream\n".Length;
                var beforeStream = Encoding.ASCII.GetBytes(objects[i].Substring(0, split));
                var afterStream = Encoding.ASCII.GetBytes(objects[i + 1]);
                objectBytes.Add(beforeStream);
                objectBytes.Add(imgBytes);
                objectBytes.Add(afterStream);
                offsets[i + 1] = offset;
                offset += beforeStream.Length + imgBytes.Length + afterStream.Length;
                i++;
            }
            else
            {
                var bytes = Encoding.ASCII.GetBytes(objects[i]);
                objectBytes.Add(bytes);
                offsets[i + 1] = offset;
                offset += bytes.Length;
            }
        }

        int xrefPos = offset;
        StringBuilder trailerSb = new StringBuilder();
        trailerSb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (int i = 1; i <= objects.Count; i++)
            trailerSb.Append($"{offsets[i]:D10} 00000 n \n");
        trailerSb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");

        using (var fs = new FileStream(outputPdf, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(Encoding.ASCII.GetBytes(header));
            foreach (var arr in objectBytes)
                bw.Write(arr);
            bw.Write(Encoding.ASCII.GetBytes(trailerSb.ToString()));
        }
    }

    public void EncryptPdf(string inputPdf, string outputPdf, string userPassword, string ownerPassword = "owner")
    {
        // 1. Lee el PDF original como binario
        byte[] pdfBytes = File.ReadAllBytes(inputPdf);

        // 2. Calcula los valores U, O y el key (ver specs PDF)
        // (Aquí, versión simple, no soporta permisos personalizados ni contraseñas muy largas)
        byte[] userPass = PadOrTruncatePassword(userPassword);
        byte[] ownerPass = PadOrTruncatePassword(ownerPassword);
        byte[] O = ComputeO(userPass, ownerPass);
        byte[] encryptionKey = ComputeEncryptionKey(userPass, O);

        byte[] U = ComputeU(encryptionKey);

        // 3. Construye el objeto Encrypt (obj N 0 obj)
        // Vamos a ponerlo al final, por lo que su número es el siguiente al último objeto
        string pdfText = Encoding.ASCII.GetString(pdfBytes);
        var objects = NativePDFHelper.ExtractAllPdfObjects(pdfText);
        int encryptObjNum = objects.Count + 1;

        string encryptObj =
            $"{encryptObjNum} 0 obj\n" +
            $"<< /Filter /Standard /V 1 /R 2 /O <{ToHex(O)}> /U <{ToHex(U)}> /P -4 /Length 40 >>\nendobj\n";

        // 4. Modifica el trailer para agregar /Encrypt N 0 R
        // (Lo agregaremos al reconstruir el PDF)

        // 5. Cifra los streams y strings (solo ciframos streams simples)
        // (Versión mínima: solo cifra los streams de los objetos de contenido y de imagen)
        List<byte[]> newObjectBytes = new List<byte[]>();
        string header = "%PDF-1.4\n";
        int[] offsets = new int[objects.Count + 2];
        int offset = header.Length;
        offsets[0] = 0;

        for (int i = 0; i < objects.Count; i++)
        {
            string obj = objects[i];
            // Busca streams
            if (obj.Contains("stream\n"))
            {
                int streamIdx = obj.IndexOf("stream\n") + "stream\n".Length;
                int endStreamIdx = obj.IndexOf("endstream", streamIdx);
                if (endStreamIdx > streamIdx)
                {
                    byte[] before = Encoding.ASCII.GetBytes(obj.Substring(0, streamIdx));
                    byte[] stream = Encoding.ASCII.GetBytes(obj.Substring(streamIdx, endStreamIdx - streamIdx));
                    byte[] after = Encoding.ASCII.GetBytes(obj.Substring(endStreamIdx));
                    // Cifra el stream con RC4 usando la key
                    byte[] encryptedStream = RC4(stream, encryptionKey);
                    newObjectBytes.Add(before);
                    newObjectBytes.Add(encryptedStream);
                    newObjectBytes.Add(after);
                    offsets[i + 1] = offset;
                    offset += before.Length + encryptedStream.Length + after.Length;
                    continue;
                }
            }
            var bytes = Encoding.ASCII.GetBytes(obj);
            newObjectBytes.Add(bytes);
            offsets[i + 1] = offset;
            offset += bytes.Length;
        }

        // Agrega el objeto Encrypt
        var encryptObjBytes = Encoding.ASCII.GetBytes(encryptObj);
        newObjectBytes.Add(encryptObjBytes);
        offsets[objects.Count + 1] = offset;
        offset += encryptObjBytes.Length;

        // 6. Reconstruye el xref y el trailer con /Encrypt
        int xrefPos = offset;
        StringBuilder trailerSb = new StringBuilder();
        trailerSb.Append($"xref\n0 {objects.Count + 2}\n0000000000 65535 f \n");
        for (int i = 1; i <= objects.Count + 1; i++)
            trailerSb.Append($"{offsets[i]:D10} 00000 n \n");
        trailerSb.Append(
            $"trailer\n<< /Size {objects.Count + 2} /Root 1 0 R /Encrypt {encryptObjNum} 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");

        // 7. Escribe el PDF final
        using (var fs = new FileStream(outputPdf, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(Encoding.ASCII.GetBytes(header));
            foreach (var arr in newObjectBytes)
                bw.Write(arr);
            bw.Write(Encoding.ASCII.GetBytes(trailerSb.ToString()));
        }
    }

    // --- Helpers para PDF encryption RC4 40bits ---
    private byte[] PadOrTruncatePassword(string pwd)
    {
        byte[] pad = {
        0x28,0xBF,0x4E,0x5E,0x4E,0x75,0x8A,0x41,
        0x64,0x00,0x4E,0x56,0xFF,0xFA,0x01,0x08,
        0x2E,0x2E,0x00,0xB6,0xD0,0x68,0x3E,0x80,
        0x2F,0x0C,0xA9,0xFE,0x64,0x53,0x69,0x7A
    };
        byte[] pwdBytes = Encoding.ASCII.GetBytes(pwd ?? "");
        byte[] outBytes = new byte[32];
        int len = Math.Min(pwdBytes.Length, 32);
        Array.Copy(pwdBytes, outBytes, len);
        if (len < 32) Array.Copy(pad, 0, outBytes, len, 32 - len);
        return outBytes;
    }
    private byte[] ComputeO(byte[] user, byte[] owner)
    {
        // owner = RC4(ownerpad, userpad)
        return RC4(owner, user);
    }
    private  byte[] ComputeEncryptionKey(byte[] userPad, byte[] O)
    {
        // key = MD5(userpad + O + P + id + 0s)
        // Para PDF básico, solo 5 bytes de MD5(userPad+O+P+4bytes+0)
        using (var md5 = MD5.Create())
        {
            byte[] P = BitConverter.GetBytes(-4);
            byte[] input = new byte[32 + 32 + 4];
            Array.Copy(userPad, 0, input, 0, 32);
            Array.Copy(O, 0, input, 32, 32);
            Array.Copy(P, 0, input, 64, 4);
            var hash = md5.ComputeHash(input);
            byte[] key = new byte[5]; // 40bits
            Array.Copy(hash, key, 5);
            return key;
        }
    }
    private  byte[] ComputeU(byte[] key)
    {
        // U = RC4(key, pad)
        byte[] pad = PadOrTruncatePassword("");
        return RC4(pad, key);
    }
    private  string ToHex(byte[] data)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in data)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
    private  byte[] RC4(byte[] data, byte[] key)
    {
        byte[] s = new byte[256];
        for (int i = 0; i < 256; i++) s[i] = (byte)i;
        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            byte temp = s[i]; s[i] = s[j]; s[j] = temp;
        }
        byte[] output = new byte[data.Length];
        int iidx = 0, jidx = 0;
        for (int k = 0; k < data.Length; k++)
        {
            iidx = (iidx + 1) & 0xFF;
            jidx = (jidx + s[iidx]) & 0xFF;
            byte temp = s[iidx]; s[iidx] = s[jidx]; s[jidx] = temp;
            output[k] = (byte)(data[k] ^ s[(s[iidx] + s[jidx]) & 0xFF]);
        }
        return output;
    }
}