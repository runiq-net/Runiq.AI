using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Runiq.Core.Agents;
using Runiq.Core.Dashboard;
using Runiq.Core.Metadata;

namespace Runiq.Core;

/// <summary>
/// Runiq Dashboard'u host uygulama içinde yayınlayan extension metodlarını içerir.
/// </summary>
public static class RuniqDashboardApplicationBuilderExtensions
{
    /// <summary>
    /// Runiq Dashboard'u varsayılan ayarlarla yayınlar.
    /// </summary>
    public static IApplicationBuilder UseRuniqDashboard(this IApplicationBuilder app)
    {
        return app.UseRuniqDashboard(_ => { });
    }

    /// <summary>
    /// Runiq Dashboard'u verilen ayarlarla yayınlar.
    /// </summary>
    public static IApplicationBuilder UseRuniqDashboard(
        this IApplicationBuilder app,
        Action<RuniqDashboardOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RuniqDashboardOptions();
        configure(options);

        var basePath = NormalizePath(options.Path);

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRuniqAgentApi($"{basePath}/api");
        });

        var dashboardRoot = Path.Combine(
            AppContext.BaseDirectory,
            "Studio",
            "wwwroot");

        if (!Directory.Exists(dashboardRoot))
        {
            throw new DirectoryNotFoundException(
                $"Runiq Dashboard assets could not be found. Expected path: {dashboardRoot}");
        }

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals(basePath, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect(basePath + "/");
                return;
            }

            await next();
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = basePath,
            FileProvider = new PhysicalFileProvider(dashboardRoot)
        });

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(basePath, out var remainingPath))
            {
                await next();
                return;
            }

            var relativePath = remainingPath.Value ?? string.Empty;

            if (relativePath.Equals("/metadata/agents", StringComparison.OrdinalIgnoreCase))
            {
                var metadataService =
                    context.RequestServices.GetRequiredService<IRuntimeMetadataService>();

                var agents = metadataService.GetAgents();

                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(agents, context.RequestAborted);
                return;
            }

            if (relativePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(
                    "Runiq API endpoint was not found.",
                    context.RequestAborted);

                return;
            }

            var requestPath = context.Request.Path.Value ?? string.Empty;

            var isStaticAsset =
                Path.HasExtension(requestPath) ||
                requestPath.Contains("/assets/", StringComparison.OrdinalIgnoreCase);

            if (isStaticAsset)
            {
                await next();
                return;
            }

            var indexPath = Path.Combine(dashboardRoot, "index.html");

            if (!File.Exists(indexPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(
                    "Runiq Dashboard index.html not found.",
                    context.RequestAborted);

                return;
            }

            var html = await File.ReadAllTextAsync(
                indexPath,
                context.RequestAborted);

            html = html
                .Replace("__RUNIQ_BASE_PATH__", basePath, StringComparison.Ordinal)
                .Replace("__RUNIQ_TITLE__", options.Title, StringComparison.Ordinal);

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html, context.RequestAborted);
        });

        return app;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Dashboard path cannot be empty.",
                nameof(path));
        }

        var normalized = path.Trim();

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');

        if (normalized == "/")
        {
            throw new ArgumentException(
                "Dashboard root path '/' is not supported.",
                nameof(path));
        }

        return normalized;
    }
}