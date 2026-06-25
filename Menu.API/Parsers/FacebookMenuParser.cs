using System.Text.RegularExpressions;
using Menu.Interfaces;
using Menu.Models;

namespace Menu.Parsers;

public partial class FacebookMenuParser : IMenuParser
{
    [GeneratedRegex(@"^(.*)\((\d+)\s*zł(?:,\s*w\s*zestawie\s*(\d+)\s*zł)?\)$", RegexOptions.Compiled)]
    private static partial Regex PriceRegex();

    private static readonly Regex WhitespaceNorm = new(@"[^\S\r\n]+", RegexOptions.Compiled);

    public List<MenuItem> Parse(string input)
    {
        var cutOff = FindFirstOf(input, "Показать меньше", "Pokazać mniej", "Pokaż mniej", "See less");
        if (cutOff > 0) input = input[..cutOff];

        var result     = new List<MenuItem>();
        var cache      = new Dictionary<string, Category>();
        var currentCat = "SecondCourse";

        var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in lines)
        {
            var line = WhitespaceNorm.Replace(raw, " ").Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            if (line.EndsWith(':'))
            {
                currentCat = MapCategory(line.TrimEnd(':').Trim());
                continue;
            }
            
            if (line.StartsWith('•') || line.StartsWith('·') || line.StartsWith('–') || line.StartsWith('-') || line.StartsWith('*'))
            {
                var itemText = line.TrimStart('•', '·', '–', '-', '*').Trim();
                
                if (!cache.TryGetValue(currentCat, out var category))
                {
                    category = new Category { Id = Guid.NewGuid(), Name = currentCat };
                    cache[currentCat] = category;
                }

                var priceMatch = PriceRegex().Match(itemText);
                
                if (priceMatch.Success)
                {
                    var nameAndDesc = priceMatch.Groups[1].Value.Trim().TrimEnd(',').Trim();
                    var mainPriceMinor = int.Parse(priceMatch.Groups[2].Value) * 100;
                    
                    ExtractNameAndDesc(nameAndDesc, out string name, out string description);

                    result.Add(CreateItem(category, name, description, mainPriceMinor));
                    
                    if (priceMatch.Groups[3].Success)
                    {
                        var setPriceMinor = int.Parse(priceMatch.Groups[3].Value) * 100;
                        result.Add(CreateItem(category, $"{name} (w zestawie)", description, setPriceMinor));
                    }
                }
                else
                {
                    ExtractNameAndDesc(itemText, out string name, out string description);
                    result.Add(CreateItem(category, name, description, 0));
                }
            }
        }

        return result;
    }

    private static void ExtractNameAndDesc(string input, out string name, out string description)
    {
        var comma = input.IndexOf(',');
        if (comma > 0)
        {
            name = input[..comma].Trim();
            description = input[(comma + 1)..].Trim();
        }
        else
        {
            name = input;
            description = string.Empty;
        }
    }

    private static MenuItem CreateItem(Category category, string name, string description, int priceMinor)
    {
        return new MenuItem
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Category = category,
            Name = name,
            Description = description,
            PriceMinor = priceMinor,
            IsAvailable = true,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static int FindFirstOf(string text, params string[] markers)
    {
        var idx = -1;
        foreach (var marker in markers)
        {
            var pos = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (pos >= 0 && (idx < 0 || pos < idx))
                idx = pos;
        }
        return idx;
    }

    private static string MapCategory(string header) => header.ToLowerInvariant() switch
    {
        var h when h.Contains("zupa")    => "FirstCourse",
        var h when h.Contains("drugie")  => "SecondCourse",
        var h when h.Contains("deser")   => "Desserts",
        var h when h.Contains("napoje")  || h.Contains("napój") => "Drinks",
        var h when h.Contains("dodatki") || h.Contains("surówk") => "Sides",
        _ => "SecondCourse"
    };
}