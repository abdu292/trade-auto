using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Brain.Web.Filters;

public sealed class TradeApiSecurityOptions
{
    public bool Enabled { get; set; } = true;

    public string ApiKeyHeaderName { get; set; } = "X-API-Key";

    public string? ApiKey { get; set; }

    public string[] AllowedIps { get; set; } = [];
}

public sealed class TradeApiSecurityFilter(
    IOptions<TradeApiSecurityOptions> options,
    ILogger<TradeApiSecurityFilter> logger) : IEndpointFilter
{
    private readonly TradeApiSecurityOptions _options = options.Value;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!_options.Enabled)
        {
            return await next(context);
        }

        var httpContext = context.HttpContext;
        if (!HasValidApiKey(httpContext.Request))
        {
            logger.LogWarning("Blocked request with invalid API key. Path: {Path}", httpContext.Request.Path.Value);
            return TypedResults.Unauthorized();
        }

        if (!IsAllowedIp(httpContext.Connection.RemoteIpAddress))
        {
            logger.LogWarning(
                "Blocked request from disallowed IP {Ip}. Path: {Path}",
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Path.Value);
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }

    private bool HasValidApiKey(HttpRequest request)
    {
        var configuredApiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            logger.LogError("Trade API security is enabled, but Security:ApiKey is not configured.");
            return false;
        }

        if (!request.Headers.TryGetValue(_options.ApiKeyHeaderName, out var providedApiKey))
        {
            return false;
        }

        return SecureEquals(configuredApiKey, providedApiKey.ToString());
    }

    private bool IsAllowedIp(IPAddress? remoteIp)
    {
        if (_options.AllowedIps.Length == 0)
        {
            return true;
        }

        if (remoteIp is null)
        {
            return false;
        }

        var normalizedRemoteIp = NormalizeIp(remoteIp);
        foreach (var allowedIp in _options.AllowedIps)
        {
            if (string.IsNullOrWhiteSpace(allowedIp))
            {
                continue;
            }

            if (!IPAddress.TryParse(allowedIp, out var parsedAllowedIp))
            {
                continue;
            }

            if (NormalizeIp(parsedAllowedIp).Equals(normalizedRemoteIp))
            {
                return true;
            }
        }

        return false;
    }

    private static IPAddress NormalizeIp(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}