namespace Menu.Interfaces;

public interface IOcrProcessor
{
    Task<string> GetTextFromImage(string imagePath, string restaurantLocation, int variant);
}