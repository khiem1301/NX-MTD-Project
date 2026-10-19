using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NX_MTD.API.Common;
using NX_MTD.API.Filters;
using NX_MTD.API.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Extensions;

public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiCore(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        jwt.Key = config["Jwt:Key"] ?? throw new InvalidOperationException("Thiếu Jwt:Key. Chạy: dotnet user-secrets set \"Jwt:Key\" \"...\"");

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddControllers(o =>
            {
                o.Filters.Add<ValidationFilter>();
                o.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer()));
            })
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(o =>
                o.InvalidModelStateResponseFactory = ctx => new Microsoft.AspNetCore.Mvc.ObjectResult(
                    ApiResponses.Failure(ErrorCodes.CommonValidation, "Dữ liệu không hợp lệ.",
                        ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0).ToDictionary(
                            kv => char.ToLowerInvariant(kv.Key[0]) + kv.Key[1..],
                            kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray())))
                { StatusCode = StatusCodes.Status400BadRequest });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "username", RoleClaimType = "role",
            };
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    var p = ctx.Principal;
                    if (p is null || p.FindFirst("tokenType")?.Value != "access"
                        || !int.TryParse(p.FindFirst("sub")?.Value, out var accountId)
                        || !Guid.TryParse(p.FindFirst("stamp")?.Value, out var stamp))
                    {
                        ctx.Fail("Invalid token");
                        return;
                    }
                    var auth = ctx.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                    if (!await auth.IsSessionValidAsync(accountId, stamp, ctx.HttpContext.RequestAborted))
                        ctx.Fail("Session is no longer valid");
                },
                OnChallenge = async ctx =>
                {
                    ctx.HandleResponse();
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(ApiResponses.Failure(ErrorCodes.AuthUnauthorized, Messages.Unauthorized));
                },
                OnForbidden = async ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsJsonAsync(ApiResponses.Failure(ErrorCodes.AuthForbidden, Messages.Forbidden));
                },
            };
        });
        services.AddAuthorization();

        var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(o => o.AddPolicy("Web", p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "NX-MTD API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
                Description = "Dán access token (không cần chữ Bearer).",
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() },
            });
        });
        return services;
    }
}
