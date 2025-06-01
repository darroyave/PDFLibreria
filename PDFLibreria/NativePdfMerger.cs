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
            tempMergedContent = UpdatePagesObjectInMergedContent(tempMergedContent, maxObjNumPdf1, pdf1String, pdf2String, pdfEncoding); // Updated Call

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

        // Add these helper methods within the NativePdfMerger class:
        private string GetObjectContent(string pdfString, string objNumAndGen)
        {
            // objNumAndGen should be like "10 0"
            // Regex to find "X Y obj ... endobj"
            // Need to handle cases where objNumAndGen might be part of a different string by ensuring "obj" keyword follows.
            Match objMatch = Regex.Match(pdfString, @"(^|\s)" + Regex.Escape(objNumAndGen) + @"\s+obj(.*?)endobj", RegexOptions.Singleline);
            if (objMatch.Success)
            {
                return objMatch.Groups[2].Value.Trim(); // Content between "obj" and "endobj"
            }
            return null;
        }

        private string ExtractDictionaryValue(string dictContent, string keyName)
        {
            const string VALUE_REGEX_PATTERN = @"(\d+\s+\d+\s+R|\[[^\]]*\]|\<\<[^>]*\>\>|\([\s\S]*?\)|/[^ /\[<>()]+|\d[\d.]*|true|false)";
            // Order matters: indirect ref, array, dict, literal string, name, number, boolean
            // This is still simplified (e.g. literal string parsing, hex strings <...>).

            Match valMatch = Regex.Match(dictContent, @"/" + Regex.Escape(keyName) + @"\s*(" + VALUE_REGEX_PATTERN + ")", RegexOptions.Singleline);

            if (valMatch.Success)
            {
                // Group 0 is the whole match, e.g. "/Count 1"
                // Group 1 is the start of VALUE_REGEX_PATTERN, which is the first alternative (indirect ref)
                // Group 2 is the actual captured value by one of the alternatives in VALUE_REGEX_PATTERN
                // We need to find which group in the alternation actually captured.
                // The overall capture for VALUE_REGEX_PATTERN is Groups[1] if not nested,
                // but Regex.Match stores captures by their group index in the pattern.
                // The "VALUE_REGEX_PATTERN" is wrapped in an outer capture group by C# automatically for the string.
                // So Groups[1] will be the content matched by the entire VALUE_REGEX_PATTERN string.
                return valMatch.Groups[1].Value.Trim();
            }
            return null;
        }

        private List<string> ExtractArrayElements(string arrayString)
        {
            var elements = new List<string>();
            if (string.IsNullOrWhiteSpace(arrayString) || !arrayString.StartsWith("[") || !arrayString.EndsWith("]"))
            {
                return elements;
            }
            string content = arrayString.Substring(1, arrayString.Length - 2).Trim();

            // Regex to find "X Y R" references or other simple elements.
            // This is simplified; arrays can contain various types.
            Regex elementRegex = new Regex(@"(\d+\s+\d+\s+R|\S+)");
            MatchCollection matches = elementRegex.Matches(content);
            foreach (Match match in matches)
            {
                elements.Add(match.Value);
            }
            return elements;
        }


        // Replacement for UpdatePagesObjectInMergedContent
        private string UpdatePagesObjectInMergedContent(string mergedContent, int pdf2ObjectOffset, string pdf1OriginalString, string pdf2OriginalString, Encoding enc)
        {
            Console.WriteLine("Attempting to update Pages object in merged content...");

            // 1. Find PDF1's Catalog (Root object) to then find its Pages object
            string pdf1RootRef = GetRootObjectRef(pdf1OriginalString); // e.g., "1 0 R"
            if (pdf1RootRef == null) {
                Console.WriteLine("WARNING (UpdatePagesObject): PDF1 Root object reference not found. Skipping page tree update.");
                return mergedContent;
            }
            string pdf1RootObjNum = pdf1RootRef.Replace(" R", ""); // e.g., "1 0"
            string pdf1RootContent = GetObjectContent(pdf1OriginalString, pdf1RootObjNum);
            if (pdf1RootContent == null) {
                Console.WriteLine($"WARNING (UpdatePagesObject): PDF1 Root object content for '{pdf1RootObjNum}' not found. Skipping page tree update.");
                return mergedContent;
            }
            string pdf1PagesRef = ExtractDictionaryValue(pdf1RootContent, "Pages"); // e.g., "2 0 R"
            if (pdf1PagesRef == null || !pdf1PagesRef.EndsWith(" R")) {
                Console.WriteLine($"WARNING (UpdatePagesObject): PDF1 /Pages reference in Root object '{pdf1RootObjNum}' not found or not an indirect reference. Skipping page tree update.");
                return mergedContent;
            }
            string pdf1PagesObjNum = pdf1PagesRef.Replace(" R", ""); // e.g., "2 0"
            //string pdf1PagesOriginalContent = GetObjectContent(pdf1OriginalString, pdf1PagesObjNum); // Full content of Pages obj

            // We need to find this same Pages object in the *mergedContent* because its definition might be there.
            // The object number (pdf1PagesObjNum) should still be valid as we assume PDF1 objects are not renumbered.
            string pdf1PagesCurrentContentWithWrapper = GetObjectContentWithWrapper(mergedContent, pdf1PagesObjNum);
            if (pdf1PagesCurrentContentWithWrapper == null) {
                 Console.WriteLine($"WARNING (UpdatePagesObject): PDF1 /Pages object {pdf1PagesObjNum} not found in *merged* content. Skipping page tree update.");
                return mergedContent;
            }

            string pdf1PagesDict = GetObjectContent(mergedContent, pdf1PagesObjNum); // Just the dictionary part from merged
             if (pdf1PagesDict == null) {
                Console.WriteLine($"WARNING (UpdatePagesObject): PDF1 /Pages dictionary for {pdf1PagesObjNum} could not be extracted from *merged* content. Skipping page tree update.");
                return mergedContent;
            }

            string pdf1KidsArrayStr = ExtractDictionaryValue(pdf1PagesDict, "Kids");
            string pdf1CountStr = ExtractDictionaryValue(pdf1PagesDict, "Count");

            if (pdf1KidsArrayStr == null || !pdf1KidsArrayStr.StartsWith("[")) {
                Console.WriteLine($"WARNING (UpdatePagesObject): PDF1 /Kids array for Pages object {pdf1PagesObjNum} not found in merged content. Skipping page tree update.");
                return mergedContent;
            }
            if (pdf1CountStr == null) {
                Console.WriteLine($"WARNING (UpdatePagesObject): PDF1 /Count for Pages object {pdf1PagesObjNum} not found in merged content. Skipping page tree update.");
                return mergedContent;
            }

            List<string> pdf1Kids = ExtractArrayElements(pdf1KidsArrayStr);
            int.TryParse(pdf1CountStr, out int pdf1Count);
            Console.WriteLine($"DEBUG: PDF1 Count: string='{pdf1CountStr}', parsed='{pdf1Count}', kids_array_count='{pdf1Kids.Count}'");

            // 2. Find PDF2's Pages object, its Kids, and Count (from pdf2OriginalString)
            string pdf2RootRef = GetRootObjectRef(pdf2OriginalString);
            if (pdf2RootRef == null) {
                Console.WriteLine("WARNING (UpdatePagesObject): PDF2 Root object reference not found. Cannot get its pages.");
                return mergedContent; // No pages from PDF2 to add if its root is not found
            }
            string pdf2RootObjNum = pdf2RootRef.Replace(" R", "");
            string pdf2RootContent = GetObjectContent(pdf2OriginalString, pdf2RootObjNum);
            if (pdf2RootContent == null) {
                 Console.WriteLine($"WARNING (UpdatePagesObject): PDF2 Root object content for '{pdf2RootObjNum}' not found. Cannot get its pages.");
                return mergedContent;
            }
            string pdf2PagesRef = ExtractDictionaryValue(pdf2RootContent, "Pages");
            if (pdf2PagesRef == null || !pdf2PagesRef.EndsWith(" R")) {
                Console.WriteLine($"WARNING (UpdatePagesObject): PDF2 /Pages reference in its Root object '{pdf2RootObjNum}' not found or not an indirect reference. Cannot get its pages.");
                return mergedContent;
            }
            string pdf2PagesObjNum = pdf2PagesRef.Replace(" R", "");
            string pdf2PagesOriginalContent = GetObjectContent(pdf2OriginalString, pdf2PagesObjNum); // Full content of Pages obj
            if (pdf2PagesOriginalContent == null) {
                 Console.WriteLine($"WARNING (UpdatePagesObject): PDF2 /Pages object content for '{pdf2PagesObjNum}' not found in original PDF2. Cannot get its pages.");
                return mergedContent;
            }

            string pdf2KidsArrayStr = ExtractDictionaryValue(pdf2PagesOriginalContent, "Kids");
            string pdf2CountStr = ExtractDictionaryValue(pdf2PagesOriginalContent, "Count");

            if (pdf2KidsArrayStr == null || !pdf2KidsArrayStr.StartsWith("[")) {
                Console.WriteLine($"WARNING (UpdatePagesObject): PDF2 /Kids array for its Pages object {pdf2PagesObjNum} not found. Cannot add its pages.");
                return mergedContent;
            }
            if (pdf2CountStr == null) {
                 Console.WriteLine($"WARNING (UpdatePagesObject): PDF2 /Count for its Pages object {pdf2PagesObjNum} not found. Cannot accurately add its pages.");
                // We could proceed but count would be wrong. For now, let's be strict.
                return mergedContent;
            }

            List<string> pdf2Kids = ExtractArrayElements(pdf2KidsArrayStr);
            int.TryParse(pdf2CountStr, out int pdf2CountVal);
            Console.WriteLine($"DEBUG: PDF2 Count: string='{pdf2CountStr}', parsed='{pdf2CountVal}', kids_array_count='{pdf2Kids.Count}'");

            // 3. Renumber PDF2's Kids and combine
            StringBuilder renumberedPdf2KidsBuilder = new StringBuilder();
            foreach (string kidRef in pdf2Kids) // kidRef is like "X Y R"
            {
                Match m = Regex.Match(kidRef, @"(\d+)\s+(\d+\s+R)");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int kidObjNum))
                {
                    renumberedPdf2KidsBuilder.Append($"{kidObjNum + pdf2ObjectOffset} {m.Groups[2].Value} ");
                }
                // Else: kid might not be an indirect reference, or parsing failed. Skip for now.
            }
            string renumberedPdf2KidsString = renumberedPdf2KidsBuilder.ToString().TrimEnd();

            // 4. Construct the new Kids array and Count for PDF1's Pages object
            string combinedKidsString = (string.Join(" ", pdf1Kids) + " " + renumberedPdf2KidsString).Trim();
            int newTotalCount = pdf1Count + pdf2CountVal; // Use actual count from PDF2 if available

            // 5. Replace the /Kids and /Count in PDF1's Pages object *within the mergedContent*
            // This is tricky. We need to replace values within the existing << ... >> dictionary.
            // We'll replace the old /Kids [...] and /Count X an entire new definition.

            string newPdf1PagesDict = pdf1PagesDict;
            // Replace Kids
            newPdf1PagesDict = Regex.Replace(newPdf1PagesDict, @"/Kids\s*\[[^\]]*\]", $"/Kids [{combinedKidsString}]", RegexOptions.Singleline);
            // Replace Count
            newPdf1PagesDict = Regex.Replace(newPdf1PagesDict, @"/Count\s*\d+", $"/Count {newTotalCount}", RegexOptions.Singleline);

            // Construct the full new object string for PDF1's Pages
            string updatedPdf1PagesObjectFull = $"{pdf1PagesObjNum} obj\r\n{newPdf1PagesDict}\r\nendobj"; // Ensure newlines like typical PDF

            // Replace the old Pages object string (identified by pdf1PagesCurrentContentWithWrapper) with the new one.
            string finalMergedContent = mergedContent.Replace(pdf1PagesCurrentContentWithWrapper, updatedPdf1PagesObjectFull);

            Console.WriteLine($"Successfully updated Pages object {pdf1PagesObjNum}. Original Kids: {pdf1Kids.Count}, Added Kids from PDF2: {pdf2Kids.Count}, New Total Kids: {newTotalCount}");
            return finalMergedContent;
        }

        // Helper to get the full object definition string: "X Y obj ... endobj"
        private string GetObjectContentWithWrapper(string pdfString, string objNumAndGen)
        {
            Match objMatch = Regex.Match(pdfString, @"(^|\s)" + Regex.Escape(objNumAndGen) + @"\s+obj.*?endobj", RegexOptions.Singleline);
            if (objMatch.Success)
            {
                // If match starts with a space (because of (^|\s)), trim it.
                return objMatch.Value.TrimStart();
            }
            return null;
        }

        private string GetObjectNumberAndGen(string objectDefinitionStart) { //This method seems to be unused now.
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
