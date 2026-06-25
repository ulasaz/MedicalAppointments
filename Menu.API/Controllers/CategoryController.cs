using Menu.DTOs;
using Menu.Interfaces;
using Menu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Menu.Controllers;

[ApiController]
[Route("api/menu/categories")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly IStringLocalizer<CategoryController> _localizer;

    public CategoryController(ICategoryService categoryService, IStringLocalizer<CategoryController> localizer)
    {
        _categoryService = categoryService;
        _localizer = localizer;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
    {
        var response = await _categoryService.GetAllCategoriesAsync();
        return Ok(response);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<Category>> GetCategoryById(Guid id)
    {
        try
        {
            var response = await _categoryService.GetCategoryByIdAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = _localizer["CategoryNotFound"].Value });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Cafe")]
    public async Task<ActionResult<CategoryCreateDto>> AddCategory([FromBody] CategoryCreateDto category)
    {
        try
        {
            var response = await _categoryService.AddCategoryAsync(category);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = _localizer["CategoryAlreadyExists"].Value });
        }
    }

    [HttpPatch("{id}")]
    [Authorize(Roles = "Cafe")]
    public async Task<ActionResult<CategoryUpdateDto>> UpdateCategory(Guid id, [FromBody] CategoryUpdateDto category)
    {
        try
        {
            var response = await _categoryService.UpdateCategoryAsync(id, category);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = _localizer["CategoryNotFound"].Value });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = _localizer["CategoryAlreadyExists"].Value });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Cafe")]
    public async Task<ActionResult<bool>> DeleteCategory(Guid id)
    {
        try
        {
            var response = await _categoryService.DeleteCategoryAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = _localizer["CategoryNotFound"].Value });
        }
    }
}
