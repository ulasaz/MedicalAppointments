using Finbuckle.MultiTenant.Abstractions;

namespace Menu.Models;

[MultiTenant]
public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    
    public int SortOrder { get; set; }
}
