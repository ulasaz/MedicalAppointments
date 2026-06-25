using Menu.Models;

namespace Menu.Interfaces;

public interface IMenuParser
{
    List<MenuItem> Parse(string text);
}