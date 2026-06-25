using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Orders.Api.Tests.Infrastructure;

namespace Orders.Api.Tests.Tests;

[Collection("LunchOrdering")]
public class OrderLifecycleTests
{
    private readonly HttpClient _identityClient;
    private readonly HttpClient _menuClient;
    private readonly HttpClient _ordersClient;

    public OrderLifecycleTests(LunchOrderingFixture fixture)
    {
        _identityClient = fixture.IdentityFactory.CreateClient();
        _menuClient = fixture.MenuFactory.CreateClient();
        _ordersClient = fixture.OrdersFactory.CreateClient();
    }

    // ─── helpers ───────────────────────────────────────────────────────────────

    private async Task<string> RegisterAndLoginAsync(string displayName)
    {
        var email = $"{Guid.NewGuid()}@test.com";
        var password = "Test1234!";

        await _identityClient.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        });

        var loginResponse = await _identityClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = password
        });

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var loginResponse = await _identityClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = password
        });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    private async Task<Guid> GetFirstMenuItemIdAsync()
    {
        var response = await _menuClient.GetAsync("/api/menu/items");
        var items = await response.Content.ReadFromJsonAsync<List<MenuItemDto>>();
        return items!.First(i => i.IsAvailable).Id;
    }

    private static HttpRequestMessage AuthRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<Guid> CreateOrderAsync(string customerToken, Guid menuItemId, string? note = null)
    {
        var request = AuthRequest(HttpMethod.Post, "/api/orders", customerToken);
        request.Content = JsonContent.Create(new
        {
            Note = note,
            Lines = new[] { new { MenuItemId = menuItemId, Quantity = 1 } }
        });

        var response = await _ordersClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        return order!.Id;
    }

    // ─── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullOrderLifecycle_CustomerRegisters_CafeProcesses_CustomerCompletes()
    {
        var customerToken = await RegisterAndLoginAsync("E2E Customer");
        var cafeToken = await LoginAsync("cafe@demo.local", "CafePassword123!");
        var menuItemId = await GetFirstMenuItemIdAsync();

        // Customer creates an order
        var orderId = await CreateOrderAsync(customerToken, menuItemId, "Extra hot please");

        // Order status: Pending
        var getRequest = AuthRequest(HttpMethod.Get, $"/api/orders/{orderId}", customerToken);
        var order = (await (await _ordersClient.SendAsync(getRequest))
            .Content.ReadFromJsonAsync<OrderDto>())!;
        order.Status.Should().Be("Pending");
        order.Lines.Should().HaveCount(1);

        // Cafe confirms
        var confirmRequest = AuthRequest(HttpMethod.Post, $"/api/orders/{orderId}/confirm", cafeToken);
        var confirmResponse = await _ordersClient.SendAsync(confirmRequest);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Order status: Confirmed
        var afterConfirm = await GetOrderAsync(orderId, cafeToken);
        afterConfirm.Status.Should().Be("Confirmed");
        afterConfirm.ConfirmedAt.Should().NotBeNull();

        // Cafe marks ready
        var readyRequest = AuthRequest(HttpMethod.Post, $"/api/orders/{orderId}/ready", cafeToken);
        var readyResponse = await _ordersClient.SendAsync(readyRequest);
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Order status: Ready
        var afterReady = await GetOrderAsync(orderId, cafeToken);
        afterReady.Status.Should().Be("Ready");

        // Customer completes
        var completeRequest = AuthRequest(HttpMethod.Post, $"/api/orders/{orderId}/complete", customerToken);
        var completeResponse = await _ordersClient.SendAsync(completeRequest);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Order status: Completed
        var finalOrder = await GetOrderAsync(orderId, customerToken);
        finalOrder.Status.Should().Be("Completed");
        finalOrder.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CustomerCanCancelPendingOrder()
    {
        var customerToken = await RegisterAndLoginAsync("Cancel Customer");
        var menuItemId = await GetFirstMenuItemIdAsync();

        var orderId = await CreateOrderAsync(customerToken, menuItemId);

        // Customer cancels while Pending
        var cancelRequest = AuthRequest(HttpMethod.Post, $"/api/orders/{orderId}/cancel", customerToken);
        var cancelResponse = await _ordersClient.SendAsync(cancelRequest);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await GetOrderAsync(orderId, customerToken);
        order.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CafeCanRejectOrder_WithReason()
    {
        var customerToken = await RegisterAndLoginAsync("Reject Customer");
        var cafeToken = await LoginAsync("cafe@demo.local", "CafePassword123!");
        var menuItemId = await GetFirstMenuItemIdAsync();

        var orderId = await CreateOrderAsync(customerToken, menuItemId);

        // Cafe rejects with reason
        var rejectRequest = AuthRequest(
            HttpMethod.Post,
            $"/api/orders/{orderId}/reject?reason=Item+not+available",
            cafeToken);
        var rejectResponse = await _ordersClient.SendAsync(rejectRequest);
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await GetOrderAsync(orderId, cafeToken);
        order.Status.Should().Be("Rejected");
    }

    [Fact]
    public async Task CustomerCanViewOwnOrders()
    {
        var customerToken = await RegisterAndLoginAsync("My Orders Customer");
        var menuItemId = await GetFirstMenuItemIdAsync();

        await CreateOrderAsync(customerToken, menuItemId, "First order");
        await CreateOrderAsync(customerToken, menuItemId, "Second order");

        var request = AuthRequest(HttpMethod.Get, "/api/orders/mine", customerToken);
        var response = await _ordersClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
        orders.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task CafeCanViewAllOrders_FilteredByStatus()
    {
        var customerToken = await RegisterAndLoginAsync("Filter Customer");
        var cafeToken = await LoginAsync("cafe@demo.local", "CafePassword123!");
        var menuItemId = await GetFirstMenuItemIdAsync();

        await CreateOrderAsync(customerToken, menuItemId);

        var request = AuthRequest(HttpMethod.Get, "/api/orders?status=Pending", cafeToken);
        var response = await _ordersClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
        orders.Should().NotBeEmpty();
        orders!.Should().OnlyContain(o => o.Status == "Pending");
    }

    [Fact]
    public async Task CustomerCannotAccessAnotherCustomersOrder()
    {
        var customer1Token = await RegisterAndLoginAsync("Customer One");
        var customer2Token = await RegisterAndLoginAsync("Customer Two");
        var menuItemId = await GetFirstMenuItemIdAsync();

        var orderId = await CreateOrderAsync(customer1Token, menuItemId);

        var request = AuthRequest(HttpMethod.Get, $"/api/orders/{orderId}", customer2Token);
        var response = await _ordersClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateOrder_WithUnavailableItem_Returns400()
    {
        var customerToken = await RegisterAndLoginAsync("Unavailable Item Customer");
        var nonExistentId = Guid.NewGuid();

        var request = AuthRequest(HttpMethod.Post, "/api/orders", customerToken);
        request.Content = JsonContent.Create(new
        {
            Note = (string?)null,
            Lines = new[] { new { MenuItemId = nonExistentId, Quantity = 1 } }
        });

        var response = await _ordersClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CafeSeesPendingOrder_AfterCustomerPlacesIt()
    {
        var customerToken = await RegisterAndLoginAsync("Pending Order Customer");
        var cafeToken = await LoginAsync("cafe@demo.local", "CafePassword123!");
        var menuItemId = await GetFirstMenuItemIdAsync();

        var orderId = await CreateOrderAsync(customerToken, menuItemId);

        var request = AuthRequest(HttpMethod.Get, $"/api/orders/{orderId}", cafeToken);
        var response = await _ordersClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order!.Status.Should().Be("Pending");
        order.TotalMinor.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateOrder_WithoutToken_Returns401()
    {
        var menuItemId = await GetFirstMenuItemIdAsync();

        var response = await _ordersClient.PostAsJsonAsync("/api/orders", new
        {
            Note = (string?)null,
            Lines = new[] { new { MenuItemId = menuItemId, Quantity = 1 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── private helpers ───────────────────────────────────────────────────────

    private async Task<OrderDto> GetOrderAsync(Guid orderId, string token)
    {
        var request = AuthRequest(HttpMethod.Get, $"/api/orders/{orderId}", token);
        var response = await _ordersClient.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    // ─── local DTOs for JSON deserialization ──────────────────────────────────

    private record AuthResponse(string Token);

    private record MenuItemDto(Guid Id, string Name, int PriceMinor, bool IsAvailable);

    private record OrderLineDto(Guid Id, Guid MenuItemId, string ItemNameSnapshot, int UnitPriceMinorSnapshot, int Quantity, int LineTotalMinor);

    private record OrderDto(
        Guid Id,
        Guid CustomerId,
        string CustomerName,
        string Status,
        DateTimeOffset PlacedAt,
        DateTime? ConfirmedAt,
        DateTime? CompletedAt,
        string? Note,
        int TotalMinor,
        List<OrderLineDto> Lines);
}
