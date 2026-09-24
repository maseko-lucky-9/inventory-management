using System.Text.Json;

namespace Inventory.IntegrationTests;

/// <summary>A Problem Details body, read after checking the envelope every error carries (ADR-004).</summary>
public sealed record Problem(string Code, string Detail, JsonElement Body)
{
    public static async Task<Problem> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement.Clone();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        string detail = body.TryGetProperty("detail", out JsonElement value) ? value.GetString() ?? "" : "";
        return new Problem(body.GetProperty("code").GetString() ?? "", detail, body);
    }
}
