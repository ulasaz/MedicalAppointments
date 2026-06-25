using Menu.DTOs;
using Menu.Models;

namespace Menu.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<Category>> GetAllCategoriesAsync();
    Task<bool> DeleteCategoryAsync(Guid id);
    Task<CategoryCreateDto> AddCategoryAsync(CategoryCreateDto category);
    Task<CategoryUpdateDto> UpdateCategoryAsync(Guid id, CategoryUpdateDto category);
    Task<Category> GetCategoryByIdAsync(Guid id);
}