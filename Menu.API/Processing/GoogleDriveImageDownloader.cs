using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Menu.DTOs;
using Menu.Interfaces;

namespace Menu.Processing
{
    public class GoogleDriveImageDownloader : IImageDownloader
    {
        public async Task<List<DownloadedImage>> DownloadImages(string folderId)
        {
            var downloadedFiles = new List<DownloadedImage>();
            
            UserCredential credential;
            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    new[] { DriveService.Scope.Drive, DriveService.Scope.DriveReadonly },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("token.json", true));
            }

            var driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Menu OCR Parser"
            });
            
            var listRequest = driveService.Files.List();
            listRequest.Q = $"'{folderId}' in parents and trashed = false";
            listRequest.Fields = "files(id, name)";
            var files = (await listRequest.ExecuteAsync()).Files;
            

            string tempDir = Path.Combine(Path.GetTempPath(), "LunchOrders");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            foreach (var file in files)
            {
                string uniqueFileName = $"{Guid.NewGuid()}_{file.Name}";
                string filePath = Path.Combine(tempDir, uniqueFileName);

                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
    
                var getRequest = driveService.Files.Get(file.Id);
                await getRequest.DownloadAsync(fileStream);
    
                downloadedFiles.Add(new DownloadedImage { FilePath = filePath });
            }

            return downloadedFiles;
        }
    }
}