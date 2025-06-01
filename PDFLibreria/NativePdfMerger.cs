using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions; // For Regex
using System.Globalization; // For NumberStyles

namespace PDFLibreria
{
    public class NativePdfMerger
    {
        // ReadPdfBytes, FindObjects, FindXref, FindTrailer methods from previous steps
        // These are kept for continuity but new Merge logic has its own parsing.
        private byte[] ReadPdfBytes(string filePath)
        {
            try
            {
                return File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
                return null;
            }
        }

        private List<string> FindObjects(byte[] pdfBytes)
        {
            string content;
            try
            {
                content = Encoding.ASCII.GetString(pdfBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting PDF bytes to string: {ex.Message}.");
                return new List<string>();
            }
            var objects = new List<string>();
            int lastPos = 0;
            while (lastPos < content.Length)
            {
                int objStart = content.IndexOf("obj", lastPos);
                if (objStart == -1) break;
                int scanBack = objStart - 1;
                while(scanBack > 0 && content[scanBack] == ' ') scanBack--;
                if(scanBack > 0 && content[scanBack] == 'R')
                {
                    lastPos = objStart + 3;
                    continue;
                }
                int objEnd = content.IndexOf("endobj", objStart);
                if (objEnd == -1) break;
                objects.Add(content.Substring(objStart - 7, objEnd + 6 - (objStart - 7)).Trim());
                lastPos = objEnd + 6;
            }
            Console.WriteLine($"Found {objects.Count} potential 'obj'...'endobj' blocks (highly unreliable method).");
            return objects;
        }

        private string FindXref(byte[] pdfBytes)
        {
            string content = Encoding.ASCII.GetString(pdfBytes);
            int xrefPos = content.LastIndexOf("xref");
            if (xrefPos == -1) return null;
            int eofPos = content.LastIndexOf("%%EOF");
            if (eofPos == -1) eofPos = content.Length;
            return content.Substring(xrefPos, eofPos - xrefPos).Trim();
        }

        private string FindTrailer(byte[] pdfBytes)
        {
            string content = Encoding.ASCII.GetString(pdfBytes);
            int trailerPos = content.LastIndexOf("trailer");
            if (trailerPos == -1) return null;
            int eofPos = content.LastIndexOf("%%EOF");
            if (eofPos == -1) eofPos = content.Length;
            string potentialTrailer = content.Substring(trailerPos, eofPos - trailerPos).Trim();
            if (potentialTrailer.Contains("startxref")) {
                return potentialTrailer;
            }
            return null;
        }


        public void Merge(string pdfPath1, string pdfPath2, string outputPath)
        {
            Console.WriteLine($"Attempting to merge {pdfPath1} and {pdfPath2} into {outputPath}");

            byte[] pdf1Bytes = ReadPdfBytes(pdfPath1);
            byte[] pdf2Bytes = ReadPdfBytes(pdfPath2);

            if (pdf1Bytes == null || pdf2Bytes == null)
            {
                Console.WriteLine("Error reading one or both PDF files.");
                return;
            }

            Encoding pdfEncoding = Encoding.GetEncoding("ISO-8859-1");

            string pdf1String = pdfEncoding.GetString(pdf1Bytes);
            string pdf2String = pdfEncoding.GetString(pdf2Bytes);

            // 1. Attempt to find the maximum object number in PDF1
            int maxObjNumPdf1 = FindMaxObjectNumber(pdf1String);
            Console.WriteLine($"Max object number found in PDF1: {maxObjNumPdf1}");

            // 2. Separate header, body for PDF1
            string pdf1Header = GetHeader(pdf1String);
            string pdf1Body = GetBody(pdf1String, pdfEncoding);

            // 3. Separate body for PDF2
            string pdf2Body = GetBody(pdf2String, pdfEncoding);

            // 4. Attempt to renumber objects in PDF2's body and update references
            string renumberedPdf2Body = RenumberPdfObjectsAndReferences(pdf2Body, maxObjNumPdf1);
            Console.WriteLine($"PDF2 body renumbering attempted. Original length: {pdf2Body.Length}, New length: {renumberedPdf2Body.Length}");

            // 5. Concatenate: PDF1 header + PDF1 body + Renumbered PDF2 body
            StringBuilder mergedPdfContentBuilder = new StringBuilder();
            mergedPdfContentBuilder.Append(pdf1Header);
            mergedPdfContentBuilder.Append(pdf1Body);
            if (!pdf1Body.EndsWith("\r\n") && !pdf1Body.EndsWith("\n"))
            {
                 mergedPdfContentBuilder.Append("\r\n");
            }
            mergedPdfContentBuilder.Append(renumberedPdf2Body);

            // 6. Update the /Pages object (very naively)
            string tempMergedContent = mergedPdfContentBuilder.ToString();
            tempMergedContent = UpdatePagesObjectInMergedContent(tempMergedContent, maxObjNumPdf1, pdf1String, pdf2String, pdfEncoding);

            // 7. Reconstruct XRef Table and Trailer
            byte[] finalMergedBodyBytes = pdfEncoding.GetBytes(tempMergedContent);
            List<KeyValuePair<int, long>> xrefEntries = CalculateXrefEntries(finalMergedBodyBytes, pdfEncoding);

            int totalObjectsInFinalPdf = 0;
            if (xrefEntries.Any())
            {
                totalObjectsInFinalPdf = xrefEntries.Max(kvp => kvp.Key) + 1;
            }

            string newXrefTable = BuildXrefTable(xrefEntries, totalObjectsInFinalPdf);

            long startXrefOffset = finalMergedBodyBytes.Length;
            if (!tempMergedContent.EndsWith("\r\n") && !tempMergedContent.EndsWith("\n")) {
                startXrefOffset += pdfEncoding.GetByteCount("\r\n");
            }

            string rootObjectRef = GetRootObjectRef(pdf1String);
            string newTrailer = BuildTrailer(totalObjectsInFinalPdf, rootObjectRef, startXrefOffset);

            // 8. Write to output file
            try
            {
                using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(finalMergedBodyBytes, 0, finalMergedBodyBytes.Length);
                    if (!tempMergedContent.EndsWith("\r\n") && !tempMergedContent.EndsWith("\n"))
                    {
                        byte[] newlineBytes = pdfEncoding.GetBytes("\r\n");
                        fs.Write(newlineBytes, 0, newlineBytes.Length);
                    }

                    byte[] xrefBytes = pdfEncoding.GetBytes(newXrefTable);
                    fs.Write(xrefBytes, 0, xrefBytes.Length);

                    byte[] trailerBytes = pdfEncoding.GetBytes(newTrailer);
                    fs.Write(trailerBytes, 0, trailerBytes.Length);
                }
                Console.WriteLine($"Rudimentary PDF merge saved to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing merged PDF: {ex.Message}");
            }
        }

        private string GetHeader(string pdfString)
        {
            int firstObjIndex = pdfString.IndexOf("obj");
            if (firstObjIndex > 0 && firstObjIndex < 30)
            {
                 int endOfHeader = pdfString.LastIndexOf("%PDF-", firstObjIndex);
                 if(endOfHeader != -1) {
                    int newlineAfterHeader = pdfString.IndexOf("\n", endOfHeader);
                    if(newlineAfterHeader != -1 && newlineAfterHeader < firstObjIndex)
                        return pdfString.Substring(0, newlineAfterHeader + 1);
                 }
            }
            if (pdfString.StartsWith("%PDF-")) {
                int newline = pdfString.IndexOfAny(new char[] {'\r', '\n'});
                return pdfString.Substring(0, newline + (pdfString[newline] == '\r' && pdfString.Length > newline+1 && pdfString[newline+1] == '\n' ? 2:1) );
            }
            return "%PDF-1.7\r\n%âãÏÓ\r\n";
        }

        private string GetBody(string pdfString, Encoding enc)
        {
            int lastXref = pdfString.LastIndexOf("xref");
            int startOfBody = 0;
            if (pdfString.StartsWith("%PDF-")) {
                startOfBody = pdfString.IndexOfAny(new char[] {'\r', '\n'});
                if(startOfBody != -1) startOfBody += (pdfString[startOfBody] == '\r' && pdfString.Length > startOfBody+1 && pdfString[startOfBody+1] == '\n' ? 2:1); else startOfBody =0;
            }

            if (lastXref == -1) return pdfString.Substring(startOfBody);

            string upToXref = pdfString.Substring(startOfBody, lastXref - startOfBody);
            int lastEndObj = upToXref.LastIndexOf("endobj");
            if(lastEndObj != -1)
            {
                return upToXref.Substring(0, lastEndObj + "endobj".Length) + "\r\n";
            }
            return upToXref;
        }

        private int FindMaxObjectNumber(string pdfString)
        {
            int maxNum = 0;
            Regex objRegex = new Regex(@"^\s*(\d+)\s+\d+\s+obj", RegexOptions.Multiline);
            foreach (Match match in objRegex.Matches(pdfString))
            {
                if (int.TryParse(match.Groups[1].Value, out int num))
                {
                    if (num > maxNum) maxNum = num;
                }
            }
            return maxNum;
        }

        private string RenumberPdfObjectsAndReferences(string pdfBody, int offset)
        {
            if (offset == 0) return pdfBody;

            var objectNumbersToUpdate = new SortedDictionary<int, bool>(Comparer<int>.Create((a, b) => b.CompareTo(a)));

            Regex objDefRegex = new Regex(@"^\s*(\d+)\s+(\d+)\s+obj", RegexOptions.Multiline);
            foreach (Match match in objDefRegex.Matches(pdfBody))
            {
                if (int.TryParse(match.Groups[1].Value, out int num)) objectNumbersToUpdate[num] = true;
            }

            Regex objRefRegex = new Regex(@"(\d+)\s+(\d+)\s+R");
            foreach (Match match in objRefRegex.Matches(pdfBody))
            {
                if (int.TryParse(match.Groups[1].Value, out int num)) objectNumbersToUpdate[num] = true;
            }

            string currentBody = pdfBody;
            foreach (var numEntry in objectNumbersToUpdate)
            {
                int oldNum = numEntry.Key;
                int newNum = oldNum + offset;
                currentBody = Regex.Replace(currentBody, $@"(?<!\d){oldNum}\s+(\d+\s+R)", $"{newNum} $1");
                currentBody = Regex.Replace(currentBody, $@"^(\s*){oldNum}\s+(\d+\s+obj)", $"$1{newNum} $2", RegexOptions.Multiline);
            }
            return currentBody;
        }

        private string UpdatePagesObjectInMergedContent(string mergedContent, int pdf2Offset, string pdf1OriginalString, string pdf2OriginalString, Encoding enc)
        {
            string pagesPattern = @"(\d+\s+\d+\s+obj\s*<<[^>]*?/Type\s*/Pages[^>]*?)/Kids\s*\[([^\]]*)\]([^>]*?/Count\s*)(\d+)([^>]*?>>\s*endobj)";

            Match pdf1PagesMatch = Regex.Match(mergedContent, pagesPattern, RegexOptions.Singleline);

            if (!pdf1PagesMatch.Success) {
                Console.WriteLine("WARNING (UpdatePagesObject): PDF1 main /Pages object not found or pattern mismatch. Skipping page tree update.");
                return mergedContent;
            }

            string pdf1KidsString = pdf1PagesMatch.Groups[2].Value.Trim();
            int pdf1Count = int.TryParse(pdf1PagesMatch.Groups[4].Value, out var c1) ? c1 : 0;

            Match pdf2PagesMatch = Regex.Match(pdf2OriginalString, pagesPattern, RegexOptions.Singleline);
             if (!pdf2PagesMatch.Success) {
                Console.WriteLine("WARNING (UpdatePagesObject): PDF2 main /Pages object not found or pattern mismatch. Skipping page tree update.");
                return mergedContent;
            }
            string pdf2KidsString = pdf2PagesMatch.Groups[2].Value.Trim();
            int pdf2Count = int.TryParse(pdf2PagesMatch.Groups[4].Value, out var c2) ? c2 : 0;

            StringBuilder renumberedPdf2Kids = new StringBuilder();
            Regex kidRefRegex = new Regex(@"(\d+)\s+(\d+)\s+R");
            foreach(Match kidMatch in kidRefRegex.Matches(pdf2KidsString))
            {
                if (int.TryParse(kidMatch.Groups[1].Value, out int kidObjNum))
                {
                    renumberedPdf2Kids.Append($"{kidObjNum + pdf2Offset} {kidMatch.Groups[2].Value} R ");
                }
            }

            string combinedKids = (pdf1KidsString + " " + renumberedPdf2Kids.ToString()).Trim();
            int newTotalCount = pdf1Count + pdf2Count;

            string updatedPagesObject =
                $"{pdf1PagesMatch.Groups[1].Value}/Kids [{combinedKids}] {pdf1PagesMatch.Groups[3].Value}{newTotalCount}{pdf1PagesMatch.Groups[5].Value}";
            return mergedContent.Replace(pdf1PagesMatch.Groups[0].Value, updatedPagesObject);
        }

        private string GetObjectNumberAndGen(string objectDefinitionStart) {
            Match m = Regex.Match(objectDefinitionStart, @"^\s*(\d+\s+\d+)\s+obj");
            return m.Success ? m.Groups[1].Value : null;
        }

        private List<KeyValuePair<int, long>> CalculateXrefEntries(byte[] pdfBytes, Encoding enc)
        {
            var entries = new List<KeyValuePair<int, long>>();
            string pdfString = enc.GetString(pdfBytes);

            Regex objRegex = new Regex(@"^\s*(\d+)\s+(\d+)\s+obj", RegexOptions.Multiline);
            MatchCollection matches = objRegex.Matches(pdfString);

            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int objNum))
                {
                    long byteOffset = enc.GetByteCount(pdfString.Substring(0, match.Index));
                    entries.Add(new KeyValuePair<int, long>(objNum, byteOffset));
                }
            }
            entries.Sort((a,b) => a.Key.CompareTo(b.Key));
            return entries;
        }

        private string BuildXrefTable(List<KeyValuePair<int, long>> entries, int totalObjects)
        {
            StringBuilder xrefBuilder = new StringBuilder();
            xrefBuilder.AppendLine("xref");

            if (!entries.Any() && totalObjects == 0)
            {
                xrefBuilder.AppendLine("0 1");
                xrefBuilder.AppendLine("0000000000 65535 f ");
                return xrefBuilder.ToString();
            }

            xrefBuilder.AppendLine($"0 {totalObjects}");
            xrefBuilder.AppendLine("0000000000 65535 f ");

            var entryDict = entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            for (int i = 1; i < totalObjects; i++)
            {
                if (entryDict.TryGetValue(i, out long offset))
                {
                    xrefBuilder.AppendLine($"{offset:D10} 00000 n ");
                }
                else
                {
                    xrefBuilder.AppendLine("0000000000 65535 f ");
                }
            }
            return xrefBuilder.ToString();
        }

        private string GetRootObjectRef(string pdfString)
        {
            int trailerPos = pdfString.LastIndexOf("trailer");
            if (trailerPos == -1) return "1 0 R";

            string relevantString = pdfString.Substring(trailerPos);
            Match rootMatch = Regex.Match(relevantString, @"/Root\s*(\d+\s+\d+\s*R)");
            if (rootMatch.Success)
            {
                return rootMatch.Groups[1].Value;
            }
            rootMatch = Regex.Match(pdfString, @"/Root\s*(\d+\s+\d+\s*R)");
            if (rootMatch.Success) return rootMatch.Groups[1].Value;

            return "1 0 R";
        }

        private string BuildTrailer(int numObjects, string rootRef, long startXrefByteOffsetValue)
        {
            StringBuilder trailer = new StringBuilder();
            trailer.AppendLine("trailer");
            trailer.AppendLine("<<");
            trailer.AppendLine($"  /Size {numObjects}");
            trailer.AppendLine($"  /Root {rootRef}");
            trailer.AppendLine(">>");
            trailer.AppendLine("startxref");
            trailer.AppendLine(startXrefByteOffsetValue.ToString());
            trailer.AppendLine("%%EOF");
            return trailer.ToString();
        }
    }
}
