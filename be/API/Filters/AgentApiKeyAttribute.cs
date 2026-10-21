using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NX_MTD.API.Common;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Filters;

/// <summary>
/// Agent chấm công là console chạy trên máy cài Ronald Jack Pro, không đăng nhập bằng tài khoản
/// nhân viên nên xác thực bằng khóa tĩnh trong cấu hình (Attendance:AgentApiKey, đặt qua user-secrets).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AgentApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Agent-Key";
    public const string ConfigKey = "Attendance:AgentApiKey";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configured = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()[ConfigKey];

        if (string.IsNullOrWhiteSpace(configured))
        {
            context.Result = Unauthorized("Agent chấm công chưa được cấu hình khóa truy cập.");
            return Task.CompletedTask;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!IsMatch(provided, configured))
            context.Result = Unauthorized("Khóa truy cập của agent không hợp lệ.");

        return Task.CompletedTask;
    }

    /// <summary>So sánh không phụ thuộc thời gian để không lộ độ dài khóa đúng.</summary>
    private static bool IsMatch(string provided, string expected)
    {
        if (string.IsNullOrEmpty(provided)) return false;

        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(a), SHA256.HashData(b));
    }

    private static ObjectResult Unauthorized(string message) =>
        new(ApiResponses.Failure(ErrorCodes.AuthUnauthorized, message))
        {
            StatusCode = StatusCodes.Status401Unauthorized,
        };
}
