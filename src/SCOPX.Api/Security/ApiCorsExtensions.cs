using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SCOPX.Api.Security;

public static class ApiCorsExtensions
{
    public static IServiceCollection AddScopxCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        foreach (var origin in origins)
        {
            if (string.IsNullOrWhiteSpace(origin)
                || origin.Contains('*')
                || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("https" or "http")
                || (uri.Scheme == "http" && !uri.IsLoopback)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
                || !string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cors:AllowedOrigins must contain exact HTTPS origins (HTTP is allowed only for loopback development). Wildcards, paths, credentials, queries, and fragments are prohibited.");
            }
        }

        if (origins.Distinct(StringComparer.OrdinalIgnoreCase).Count() != origins.Length)
            throw new InvalidOperationException("Cors:AllowedOrigins cannot contain duplicate origins.");

        services.AddCors(options =>
        {
            options.AddPolicy("scopx", policy =>
            {
                if (origins.Length > 0)
                    policy.WithOrigins(origins)
                        .WithMethods("GET", "POST", "OPTIONS")
                        .WithHeaders("Authorization", "Content-Type", "X-Hospital-ID", "X-Correlation-ID")
                        .WithExposedHeaders("X-Correlation-ID");
            });
        });
        return services;
    }
}
