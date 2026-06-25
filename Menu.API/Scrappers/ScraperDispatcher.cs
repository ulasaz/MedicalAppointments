using Menu.Interfaces;

namespace Menu.Scrappers;

public class ScraperDispatcher : IScraper
{
    private readonly GoogleDriveScraper _googleDriveScraper;
    private readonly FacebookScraper _facebookScraper;

    public ScraperDispatcher(GoogleDriveScraper googleDriveScraper, FacebookScraper facebookScraper)
    {
        _googleDriveScraper = googleDriveScraper;
        _facebookScraper = facebookScraper;
    }

    public Task<string> GetSource(string input, string source) => source switch
    {
        "Facebook" => _facebookScraper.GetSource(input, source),
        _          => _googleDriveScraper.GetSource(input, source),
    };
}
