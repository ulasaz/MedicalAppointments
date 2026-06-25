using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Orders.DTOs;
using Orders.Interfaces;
using Orders.Models;
using System.Security.Claims;

namespace Orders.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
  private readonly IOrderService _orderService;
  private readonly IStringLocalizer<OrdersController> _localizer;

  public OrdersController(IOrderService orderService, IStringLocalizer<OrdersController> localizer)
  {
    _orderService = orderService;
    _localizer = localizer;
  }

  [HttpPost]
  [Authorize(Roles = "Customer")]
  public async Task<ActionResult<Order>> PlaceNewOrder([FromBody] OrderCreateDto request)
  {
    try
    {
      var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
      var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Unknown Customer";

      if (!Guid.TryParse(userIdString, out Guid userId))
      {
        return Unauthorized();
      }

      var response = await _orderService.AddOrderAsync(request, userId, userName);
      return CreatedAtAction(nameof(GetOrderById), new { id = response.Id }, response);
    }
    catch (ArgumentException)
    {
      return BadRequest(new { message = _localizer["InvalidOrderData"].Value });
    }
  }

  [HttpGet("{id}")]
  [Authorize(Roles = "Customer, Cafe")]
  public async Task<ActionResult<Order>> GetOrderById(Guid id)
  {
    try
    {
      var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (!Guid.TryParse(userIdString, out var userId))
      {
        return Unauthorized();
      }

      var response = await _orderService.GetOrderByIdAsync(id);

      if (!User.IsInRole("Cafe") && response.CustomerId != userId)
      {
        return Forbid();
      }

      return Ok(response);
    }
    catch (ArgumentException)
    {
      return NotFound(new { message = _localizer["OrderNotFound"].Value });
    }
  }

  [HttpGet("mine")]
  [Authorize(Roles = "Customer")]
  public async Task<ActionResult<IEnumerable<OrderInfoDto>>> GetMyOrders()
  {
    try
    {
      var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (!Guid.TryParse(userIdString, out var userId))
      {
        return Unauthorized();
      }
      var response = await _orderService.GetMyOrdersAsync(userId);
      return Ok(response);
    }
    catch (ArgumentException)
    {
      return NotFound(new { message = _localizer["OrderNotFound"].Value });
    }
  }

  [HttpPost("{id}/confirm")]
  [Authorize(Roles = "Cafe")]
  public async Task<ActionResult<bool>> ConfirmOrder(Guid id)
  {
    try
    {
      var response = await _orderService.ConfirmOrder(id);
      return Ok(response);
    }
    catch (ArgumentException)
    {
      return NotFound(new { message = _localizer["OrderNotFound"].Value });
    }
    catch (InvalidOperationException)
    {
      return Conflict(new { message = _localizer["OrderCannotBeConfirmed"].Value });
    }
  }

  [HttpPost("{id}/reject")]
  [Authorize(Roles = "Cafe")]
  public async Task<ActionResult<bool>> RejectOrder(Guid id, [FromQuery] string reason)
  {
    try
    {
      var response = await _orderService.RejectOrderAsync(id, reason);
      return Ok(response);
    }
    catch (ArgumentException)
    {
      return NotFound(new { message = _localizer["OrderNotFound"].Value });
    }
    catch (InvalidOperationException)
    {
      return Conflict(new { message = _localizer["OrderCannotBeRejected"].Value });
    }
  }

  [HttpPost("{id}/ready")]
  [Authorize(Roles = "Cafe")]
  public async Task<ActionResult<bool>> ReadyOrder(Guid id)
  {
    try
    {
      var response = await _orderService.ReadyOrderAsync(id);
      return Ok(response);
    }
    catch (ArgumentException)
    {
      return NotFound(new { message = _localizer["OrderNotFound"].Value });
    }
    catch (InvalidOperationException)
    {
      return Conflict(new { message = _localizer["OrderCannotBeMarkedReady"].Value });
    }
  }

  [HttpPost("{id}/complete")]
  [Authorize(Roles = "Customer")]
  public async Task<ActionResult<bool>> CompleteOrder(Guid id)
  {
    try
    {
      var response = await _orderService.CompleteOrderAsync(id);
      return Ok(response);
    }
    catch (ArgumentException)
    {
      return NotFound(new { message = _localizer["OrderNotFound"].Value });
    }
    catch (InvalidOperationException)
    {
      return Conflict(new { message = _localizer["OrderCannotBeCompleted"].Value });
    }
  }

  [HttpPost("{id}/cancel")]
  [Authorize(Roles = "Customer")]
  public async Task<ActionResult<bool>> CancelOrder(Guid id)
  {
    try
    {
      var response = await _orderService.CancelOrderAsync(id);
      return Ok(response);
    }
    catch (ArgumentException)
    {
      return NotFound(new { message = _localizer["OrderNotFound"].Value });
    }
    catch (InvalidOperationException)
    {
      return Conflict(new { message = _localizer["OrderCannotBeCancelled"].Value });
    }
  }

  [HttpGet]
  [Authorize(Roles = "Cafe")]
  public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders([FromQuery] OrderStatus? status)
  {
    var response = await _orderService.GetAllOrdersAsync(status);
    return Ok(response);
  }
}
