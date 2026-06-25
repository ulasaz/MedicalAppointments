using LunchBoards.Scheduler.Parsers;
using Menu.DTOs;
using Menu.Interfaces;
using Menu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using FacebookMenuParser = Menu.Parsers.FacebookMenuParser;

namespace Menu.Controllers;

[ApiController]
[Route("api/menu/items")]
public class MenuController : ControllerBase
{
  private readonly IStringLocalizer<MenuController> _localizer;
  private readonly IMenuService _menuService;
  private readonly IScraper _scraper;
  private readonly IMenuParser _parser;
  private readonly FacebookMenuParser _facebookParser;
  private readonly ILogger<MenuController> _logger;

  public MenuController(IMenuService menuService, IStringLocalizer<MenuController> localizer, IScraper scraper, IMenuParser parser, FacebookMenuParser facebookParser, ILogger<MenuController> logger)
  {
    _menuService = menuService;
    _localizer = localizer;
    _scraper = scraper;
    _parser = parser;
    _facebookParser = facebookParser;
    _logger = logger;
  }
  
  [AllowAnonymous]
  [HttpGet]
  public async Task<ActionResult<IEnumerable<MenuItem>>> GetAllMenuItems([FromQuery] string? categoryName, [FromQuery] bool? availability)
  {
      var response = await _menuService.GetAllMenuItemsAsync(categoryName, availability);
      return Ok(response);
  }
  
  [Authorize(Roles = "Cafe")]
  [HttpGet("all")]
  public async Task<ActionResult<IEnumerable<MenuItem>>> GetAllMenuItemsForManagement()
  {
    var response = await _menuService.GetAllMenuItemsUnscheduledAsync();
    return Ok(response);
  }

  [AllowAnonymous]
  [HttpGet("{id}")]
  public async Task<ActionResult<MenuItem>> GetMenuItemById(Guid id)
  {
    try
    {
      var response =await _menuService.GetMenuItemByIdAsync(id);
      return Ok(response);
    }
    catch (KeyNotFoundException)
    {
      return NotFound(new { message = _localizer["MenuItemNotFound"].Value });
    }
  }

  [Authorize(Roles = "Cafe")]
  [HttpPost]
  public async Task<ActionResult<MenuItemCreateDto>> AddMenuItem([FromBody] MenuItemCreateDto item)
  {
    try
    {
      var response = await _menuService.AddMenuItemAsync(item);
      return Ok(response);
    }
    catch (ArgumentException)
    {
      return BadRequest(new { message = _localizer["MenuItemInvalidData"].Value });
    }
  }

  [Authorize(Roles = "Cafe")]
  [HttpPatch("{id}")]
  public async Task<ActionResult<MenuItemUpdateDto>> UpdateMenuItem(Guid id, [FromBody] MenuItemUpdateDto item)
  {
    try
    {
      var response =await _menuService.UpdateMenuItemAsync(id, item);
      return Ok(response);
    }
    catch (KeyNotFoundException)
    {
      return NotFound(new { message = _localizer["MenuItemNotFound"].Value });
    }
    catch (ArgumentException)
    {
      return BadRequest(new { message = _localizer["MenuItemInvalidData"].Value });
    }
  }

  [Authorize(Roles = "Cafe")]
  [HttpPatch("{id}/availability")]
  public async Task<ActionResult<bool>> ToggleAvailability(Guid id)
  {
    try
    {
      var response = await _menuService.ToggleAvailabilityAsync(id);
      return Ok(response);
    }
    catch (KeyNotFoundException)
    {
      return NotFound(new { message = _localizer["MenuItemNotFound"].Value });
    }
  }

  [Authorize(Roles = "Cafe")]
  [HttpDelete("{id}")]
  public async Task<ActionResult<bool>> DeleteMenuItem(Guid id)
  {
    try
    {
      var response = await _menuService.DeleteMenuItemAsync(id);
      return Ok(response);
    }
    catch (KeyNotFoundException)
    {
      return NotFound(new { message = _localizer["MenuItemNotFound"].Value });
    }
  }
  
  [AllowAnonymous]
  [HttpGet("schedule/{day}")]
  public async Task<ActionResult<IEnumerable<MenuSchedule>>> GetScheduleForDay(DayOfWeek day)
  {
    var response = await _menuService.GetScheduleForDayAsync(day);
    return Ok(response);
  }

  [Authorize(Roles = "Cafe")]
  [HttpPost("schedule")]
  public async Task<ActionResult<MenuSchedule>> AddToSchedule([FromBody] MenuScheduleCreateDto dto)
  {
    try
    {
      var response = await _menuService.AddToScheduleAsync(dto);
      return Ok(response);
    }
    catch (KeyNotFoundException)
    {
      return NotFound(new { message = _localizer["MenuItemNotFound"].Value });
    }
    catch (ArgumentException)
    {
      return BadRequest(new { message = _localizer["ScheduleInvalidData"].Value });
    }
  }

  [Authorize(Roles = "Cafe")]
  [HttpDelete("schedule/{scheduleId}")]
  public async Task<ActionResult<bool>> RemoveFromSchedule(Guid scheduleId)
  {
    try
    {
      var response = await _menuService.RemoveFromScheduleAsync(scheduleId);
      return Ok(response);
    }
    catch (KeyNotFoundException)
    {
      return NotFound(new { message = _localizer["ScheduleNotFound"].Value });
    }
  }
  
  [Authorize(Roles = "Cafe")]
  [HttpGet("parser/GoogleDrive")]
  public async Task<ActionResult<IEnumerable<MenuItem>>> ParseMenuItemsFromGoogleDrive([FromQuery] string folderId)
  {
    var rawText = await _scraper.GetSource(folderId, "BarLodziak");
    var menuItems = _parser.Parse(rawText);
    return Ok(menuItems);
  }
  
  [Authorize(Roles = "Cafe")]
  [HttpGet("parser/Facebook/raw")]
  public async Task<ActionResult<string>> GetRawFacebookText([FromQuery] string url)
  {
    var rawText = await _scraper.GetSource(url, "Facebook");
    return Ok(rawText);
  }

  [Authorize(Roles = "Cafe")]
  [HttpGet("parser/Facebook")]
  public async Task<ActionResult<IEnumerable<MenuItem>>> ParseMenuItemsFromFacebook([FromQuery] string url)
  {
    var rawText = await _scraper.GetSource(url, "Facebook");
    var menuItems = _facebookParser.Parse(rawText);
    return Ok(menuItems);
  }

  [Authorize(Roles = "Cafe")]
  [HttpPost("parser/save")]
  public async Task<ActionResult<int>> SaveParsedMenuItems([FromBody] IEnumerable<MenuItemCreateDto> items)
  {
    var saved = 0;
    foreach (var item in items)
    {
      try
      {
        await _menuService.AddMenuItemAsync(item);
        saved++;
      }
      catch (ArgumentException ex)
      {
        _logger.LogWarning("Skipped parsed item '{Name}': {Reason}", item.Name, ex.Message);
      }
    }
    return Ok(saved);
  }
}