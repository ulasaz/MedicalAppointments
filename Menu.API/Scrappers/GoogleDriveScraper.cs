using System.Text;
using Menu.DTOs;
using Menu.Interfaces;

namespace Menu.Scrappers
{
    public class GoogleDriveScraper : IScraper
    {
        private readonly ILogger<GoogleDriveScraper> _logger;
        private readonly IImageDownloader _imageDownloader;
        private readonly IOcrProcessor _ocrProcessor;
        
        public GoogleDriveScraper(IImageDownloader imageDownloader, IOcrProcessor ocrProcessor, ILogger<GoogleDriveScraper> logger)
        {
            _logger = logger;
            _imageDownloader = imageDownloader;
            _ocrProcessor = ocrProcessor;
        }

        public async Task<string> GetSource(string folderId, string restaurantLocation)
        {
            try
            {
                List<DownloadedImage> files = await _imageDownloader.DownloadImages(folderId);

                var sb = new StringBuilder();

                for (int i = 0; i < files.Count; i++)
                {
                    
                    var text = await _ocrProcessor.GetTextFromImage(files[i].FilePath, restaurantLocation, i + 1);
                    sb.AppendLine(text);
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}