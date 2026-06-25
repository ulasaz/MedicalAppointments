using HtmlAgilityPack;
using Menu.Interfaces;
using Microsoft.Playwright;

namespace Menu.Scrappers;

public class FacebookScraper : IScraper
{
    private readonly ILogger<FacebookScraper> _logger;

    public FacebookScraper(ILogger<FacebookScraper> logger)
    {
        _logger = logger;
    }

    public async Task<string> GetSource(string url, string restaurantLocation)
    {
        _logger.LogInformation("Starting Playwright scraping: {Url}", url);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-dev-shm-usage"]
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(url);

        try
        {
            var cookiesButton = page.Locator("//div[contains(@aria-label,'Decline optional cookies') or contains(@aria-label,'Odrzuć opcjonalne pliki cookie')]");
            await cookiesButton.First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
            _logger.LogInformation("Cookies popup closed.");
        }
        catch (Exception)
        {
            _logger.LogInformation("Cookies popup did not appear.");
        }

        try
        {
            var closeButton = page.Locator("//div[contains(@aria-label,'Close') or contains(@aria-label,'Zamknij')]");
            await closeButton.First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
            _logger.LogInformation("Login popup closed.");
        }
        catch (Exception)
        {
            _logger.LogInformation("Login popup did not appear.");
        }

        var postLocator = page.Locator("//div[@data-ad-rendering-role='story_message']");
        await postLocator.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var initialLength = (await postLocator.First.InnerTextAsync()).Length;

        try
        {
            await page.EvaluateAsync(@"() => {
                const postEl = document.querySelector('[data-ad-rendering-role=""story_message""]');
                if (!postEl) return;
                for (const el of postEl.querySelectorAll('*')) {
                    const text = (el.innerText || '').trim();
                    if (text === 'Ещё' || text === 'Wyświetl więcej' || text === 'See more' || text === 'Показать ещё') {
                        el.click();
                        return;
                    }
                }
            }");
            
            await page.WaitForFunctionAsync(
                $"() => (document.querySelector('[data-ad-rendering-role=\"story_message\"]')?.innerText?.length ?? 0) > {initialLength}",
                new PageWaitForFunctionOptions { Timeout = 5000 });

            _logger.LogInformation("Post expanded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogInformation("See more not clicked or post already expanded: {Message}", ex.Message);
        }

        try
        {
            var html = await postLocator.First.InnerHTMLAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var textDivs = doc.DocumentNode.SelectNodes(".//div[@dir='auto']");
            if (textDivs != null && textDivs.Count > 0)
            {
                return string.Join("\n", textDivs
                    .Select(d => d.InnerText.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t)));
            }

            return await postLocator.First.InnerTextAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting Facebook post text");
            throw;
        }
    }
}
