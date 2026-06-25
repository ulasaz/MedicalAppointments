using Shared;

namespace Orders.Helpers;

public class MenuClient
{
    private readonly HttpClient _client;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MenuClient(HttpClient client, IHttpContextAccessor httpContextAccessor)
    {
        _client = client;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<MenuItemDto?> GetMenuItem(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/menu/items/{id}");

        var tenantId = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].ToString();
        if (!string.IsNullOrEmpty(tenantId))
        {
            request.Headers.Add("X-Tenant-Id", tenantId);
        }

        var response = await _client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<MenuItemDto>();
    }
}
