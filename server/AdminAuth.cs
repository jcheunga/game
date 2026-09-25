using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace CrownroadServer;

/// <summary>
/// Small, deployment-owned guard for operational endpoints. It deliberately
/// does not reuse player sessions: an operator credential must never be a
/// player credential. Configure it with ADMIN_API_KEY_FILE (preferred) or
/// ADMIN_API_KEY. When neither is configured, the admin surface is hidden.
/// </summary>
public static class AdminAuth
{
    public static IResult? Require(HttpRequest request)
    {
        var expected = ReadConfiguredKey();
        if (string.IsNullOrWhiteSpace(expected))
        {
            // Do not advertise an unconfigured administration surface.
            return Results.NotFound();
        }

        var provided = ReadProvidedKey(request);
        if (!SecureEquals(expected, provided))
        {
            request.HttpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Crownroad Admin\"";
            return Results.Json(new { error = "admin_auth_required" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        return null;
    }

    private static string ReadConfiguredKey()
    {
        var secretPath = Environment.GetEnvironmentVariable("ADMIN_API_KEY_FILE");
        if (!string.IsNullOrWhiteSpace(secretPath))
        {
            try
            {
                return File.ReadAllText(secretPath).Trim();
            }
            catch (IOException)
            {
                return "";
            }
            catch (UnauthorizedAccessException)
            {
                return "";
            }
        }

        return Environment.GetEnvironmentVariable("ADMIN_API_KEY")?.Trim() ?? "";
    }

    private static string ReadProvidedKey(HttpRequest request)
    {
        var headerKey = request.Headers["X-Admin-Api-Key"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerKey)) return headerKey.Trim();

        var authorization = request.Headers.Authorization.FirstOrDefault() ?? "";
        const string basicPrefix = "Basic ";
        if (!authorization.StartsWith(basicPrefix, StringComparison.OrdinalIgnoreCase)) return "";

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[basicPrefix.Length..].Trim()));
            var separator = decoded.IndexOf(':');
            return separator >= 0 ? decoded[(separator + 1)..] : "";
        }
        catch (FormatException)
        {
            return "";
        }
    }

    private static bool SecureEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
