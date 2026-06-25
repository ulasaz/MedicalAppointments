using System.Text;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Menu.Interfaces;

namespace Menu.Processing
{
    public class AzureOcrProcessor : IOcrProcessor
    {
        private readonly string _endpoint = "https://lunchordering.cognitiveservices.azure.com/";
        private static readonly string _key1 = "B0";
        private static readonly string _key2 = "kHc2uEKBPvSViFhBfE5UYf0LrXZxcfpLfvcLjwmR7Jii73gzmlJQQJ99CFACYeBjFXJ3w3AAALACOGgKAj";

        private  string _apiKey = _key1 + _key2;
        public async Task<string> GetTextFromImage(string imagePath, string restaurantLocation, int variant)
        {
            var credential = new AzureKeyCredential(_apiKey);
            var client = new DocumentAnalysisClient(new Uri(_endpoint), credential);

            using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
            
            AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-layout", stream);
            AnalyzeResult result = operation.Value;

            var sb = new StringBuilder();
            
            foreach (DocumentPage page in result.Pages)
            {
                foreach (DocumentLine line in page.Lines)
                {
                    sb.AppendLine(line.Content);
                }
            }

            return sb.ToString().Trim();
        }
    }
}