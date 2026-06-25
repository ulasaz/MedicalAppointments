using Menu.DTOs;

namespace Menu.Interfaces;

public interface IImageDownloader
{
    Task<List<DownloadedImage>> DownloadImages(string folderId);
}