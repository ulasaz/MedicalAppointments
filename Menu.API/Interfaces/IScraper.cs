namespace Menu.Interfaces;

public interface IScraper
{
    Task<string> GetSource(string folderId, string restaurantLocation);
}