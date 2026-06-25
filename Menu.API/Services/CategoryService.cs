using Menu.Database;
using Menu.DTOs;
using Menu.Interfaces;
using Menu.Models;
using Microsoft.EntityFrameworkCore;

namespace Menu.Services;

public class CategoryService : ICategoryService
{
    
    private readonly DatabaseContext _dbContext;

    public CategoryService(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
    {
        var categories = await _dbContext.Categories.AsNoTracking().ToListAsync();
        return categories;
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        var categoryToDelete = await _dbContext.Categories.FindAsync(id);
        
        if (categoryToDelete == null)
        {
            throw new KeyNotFoundException("Category does not exist"); 
        }
        
        _dbContext.Categories.Remove(categoryToDelete);
        await _dbContext.SaveChangesAsync();
        
        return true;
    }
    

    public async Task<Category> GetCategoryByIdAsync(Guid id)
    {
        var existingCategory = await _dbContext.Categories.FindAsync(id);
        
        if (existingCategory == null)
        {
            throw new KeyNotFoundException("Item does not exist"); 
        }
        
        return existingCategory;
    }
    

    public async Task<CategoryCreateDto> AddCategoryAsync(CategoryCreateDto category)
    {
        ArgumentNullException.ThrowIfNull(category);
        
        var existingCategory = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name == category.Name);
        
        if (existingCategory != null)
        {
            throw new ArgumentException("Category with this name already exist"); 
        }

        var newCategory = new Category
        {
            Name = category.Name
        };
        
        await _dbContext.AddAsync(newCategory);
        await _dbContext.SaveChangesAsync();
        
        return category;
    }
    

    public async Task<CategoryUpdateDto> UpdateCategoryAsync(Guid id, CategoryUpdateDto category)
    {
        ArgumentNullException.ThrowIfNull(category);
        
        var existingCategory = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id);
            
        if (existingCategory == null)
        {
            throw new KeyNotFoundException("Category does not exist"); 
        }
        
        existingCategory.Name = category.Name;

        await _dbContext.SaveChangesAsync();
        
        return category;
    }
}