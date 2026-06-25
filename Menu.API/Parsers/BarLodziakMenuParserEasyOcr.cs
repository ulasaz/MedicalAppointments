using System.Text.RegularExpressions;
using Menu.Interfaces;
using Menu.Models;

namespace Menu.Parsers
{
    public partial class BarLodziakMenuParserEasyOcr : IMenuParser
    {
        private static readonly Regex CategoryRegex = CategoryLineRegex();
        private static readonly Regex PriceRegex = PriceLineRegex();

        public List<MenuItem> Parse(string input)
        {
            input = input.Replace("ΚΑΝΑΡΚΙ", "KANAPKI");

            var lines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                             .Select(l => l.Trim())
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .ToList();

            var result = new List<MenuItem>();
            string currentCategory = "";
            string? pendingName = null;
            string? pendingDescription = null;
            var categoryCache = new Dictionary<string, Category>();

            foreach (var line in lines)
            {
                if (CategoryRegex.IsMatch(line) && line.Length > 3)
                {
                    currentCategory = line;
                    pendingName = null;
                    pendingDescription = null;
                    continue;
                }

                if (line == "SOSY")
                {
                    currentCategory = line;
                    pendingName = null;
                    pendingDescription = null;
                    continue;
                }

                var priceMatch = PriceRegex.Match(line);
                if (priceMatch.Success && !string.IsNullOrEmpty(pendingName))
                {
                    var textPart = line.Substring(0, priceMatch.Index).Trim();
                    if (!string.IsNullOrEmpty(textPart))
                    {
                        pendingDescription = pendingDescription == null
                            ? textPart
                            : pendingDescription + " " + textPart;
                    }

                    if (decimal.TryParse(priceMatch.Groups[1].Value, out var price))
                    {
                        string categoryName = DetectType(currentCategory);

                        if (!categoryCache.TryGetValue(categoryName, out var categoryObj))
                        {
                            categoryObj = new Category { Id = Guid.NewGuid(), Name = categoryName };
                            categoryCache[categoryName] = categoryObj;
                        }

                        result.Add(new MenuItem
                        {
                            Id = Guid.NewGuid(),
                            CategoryId = categoryObj.Id,
                            Category = categoryObj,
                            Name = FixDishName(pendingName.Trim()),
                            Description = pendingDescription?.Trim() ?? "",
                            PriceMinor = (int)(price * 100),
                            IsAvailable = true,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    pendingName = null;
                    pendingDescription = null;
                    continue;
                }

                if (IsValidDishName(line))
                {
                    if (pendingName == null)
                    {
                        var lowerLine = line.ToLowerInvariant();

                        if (currentCategory.Contains("SOSY") && !lowerLine.StartsWith("sos"))
                        {
                            pendingName = "Sos " + line;
                        }
                        else if (currentCategory.Contains("KANAPKI") && !lowerLine.StartsWith("kanapka"))
                        {
                            pendingName = "Kanapka " + line;
                        }
                        else
                        {
                            pendingName = line;
                        }
                    }
                    else
                    {
                        pendingDescription = pendingDescription == null
                            ? line
                            : pendingDescription + " " + line;
                    }
                }
                else if (pendingName != null && IsValidDescriptionLine(line))
                {

                    pendingDescription = pendingDescription == null
                        ? line
                        : pendingDescription + " " + line;
                }
            }

            return result;
        }

        private static bool IsValidDishName(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length > 50) return false;
            if (Regex.IsMatch(line, @"^\d")) return false;
            if (line.Contains(",")) return false; 
            if (line == "Z" || line == "***") return false;
            
            var lowerLine = line.ToLowerInvariant();
            
            if (line.StartsWith("-")) return false;
            
            string[] blackList = { "mix sałat", "pasta jajeczna", "bekon", "cebula", 
                                   "pieczarka", "i boczkiem", "do wyboru", "2x kiełbasa", "sos autorski" };
            
            foreach (var word in blackList)
            {
                if (lowerLine.Contains(word)) return false;
            }
            
            if (lowerLine.StartsWith("sos ") && !lowerLine.Contains("sos pieczeniowy") && !lowerLine.Contains("sos truflowy") && !lowerLine.Contains("sos czosnkowy")) 
            {
                if (line.Contains(",")) return false; 
            }

            return true;
        }

        private static bool IsValidDescriptionLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            if (Regex.IsMatch(line, @"^\d")) return false;
            if (line == "***" || line == "Z") return false;
            if (line.StartsWith("-")) return false;
            return true;
        }

        private static string DetectType(string? category)
        {
            if (string.IsNullOrWhiteSpace(category)) return "SecondCourse";

            category = category.ToLowerInvariant();
            return category switch
            {
                var c when c.Contains("śniadaniowe") || c.Contains("śniadaniowy") || c.Contains("kanapki") => "Breakfast",
                var c when c.Contains("zupy") => "FirstCourse",
                var c when c.Contains("mięsne") || c.Contains("jarskie") || c.Contains("rybne") || c.Contains("mączne") => "SecondCourse",
                var c when c.Contains("napoje") => "Drinks",
                var c when c.Contains("desery") || c.Contains("deser") => "Desserts",
                var c when c.Contains("dodatki") || c.Contains("surówki") || c.Contains("sosy") => "Sides",
                var c when c.Contains("zestaw obiadowy") => "TwoCourseMeal",
                _ => "SecondCourse"
            };
        }

        private static string FixDishName(string name)
        {
            return Regex.Replace(name, @"\([^)]*\)|\b2\b", m =>
            {
                if (m.Value.StartsWith("(") && m.Value.EndsWith(")")) return m.Value;
                return m.Value.Replace("2", "z");
            });
        }

        [GeneratedRegex(@"^[A-ZĄĆĘŁŃÓŚŹŻ\s]+(?:\s*\([0-9a-zA-Z\s]*\))?$", RegexOptions.Compiled)]
        private static partial Regex CategoryLineRegex();

        [GeneratedRegex(@"(\d+)", RegexOptions.Compiled)]
        private static partial Regex PriceLineRegex();
    }
}