using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Runiq.Core.Studio;

/// <summary>
/// Dashboard API ve metadata endpoint'lerini API key tabanlı kimlik doğrulama ile koruyan middleware'dir.
/// Yalnızca <c>/api/</c> ve <c>/metadata/</c> path'lerine uygulanır;
/// SPA sayfaları ve statik asset'ler kimlik doğrulama gerektirmez.
/// </summary>
internal sealed class RuniqDashboardApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly string _basePath;
    private readonly byte[] _expectedKeyBytes;

    public RuniqDashboardApiKeyMiddleware(
        RequestDelegate next,
        string basePath,
        string apiKey)
    {
        _next = next;
        _basePath = basePath;
        _expectedKeyBytes = Encoding.UTF8.GetBytes(apiKey);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestPath = context.Request.Path;

        if (!requestPath.StartsWithSegments(_basePath, out var remainingPath))
        {
            await _next(context);
            return;
        }

        var remaining = remainingPath.Value ?? string.Empty;

        var isProtectedPath =
            remaining.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            remaining.StartsWith("/metadata/", StringComparison.OrdinalIgnoreCase) ||
            remaining.Equals("/api", StringComparison.OrdinalIgnoreCase) ||
            remaining.Equals("/metadata", StringComparison.OrdinalIgnoreCase);

        if (!isProtectedPath)
        {
            await _next(context);
            return;
        }

        var providedKey = ExtractApiKey(context.Request);

        if (providedKey is null || !IsValidKey(providedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";

            await context.Response.WriteAsync(
                """{"error":"Unauthorized. Provide a valid API key via 'Authorization: Bearer <key>' or 'X-Api-Key: <key>' header."}""",
                context.RequestAborted);

            return;
        }

        await _next(context);
    }

    private static string? ExtractApiKey(HttpRequest request)
    {
        // X-Api-Key header'ını kontrol et.
        var apiKeyHeader = request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return apiKeyHeader.Trim();
        }

        // Authorization: Bearer <key> header'ını kontrol et.
        var authHeader = request.Headers.Authorization.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader[BearerPrefix.Length..].Trim();

            if (token.Length > 0)
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>
    /// Sağlanan API anahtarını beklenen değerle zamanlama saldırılarına karşı güvenli biçimde karşılaştırır.
    /// </summary>
    private bool IsValidKey(string providedKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        return CryptographicOperations.FixedTimeEquals(
            providedBytes,
            _expectedKeyBytes);
    }
}
