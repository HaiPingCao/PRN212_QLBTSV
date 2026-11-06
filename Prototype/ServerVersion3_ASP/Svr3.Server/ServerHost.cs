using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Svr3.Server.Auth;
using Svr3.Server.Data;
using Svr3.Server.Endpoints;
using Svr3.Server.Files;
using Svr3.Shared;

namespace Svr3.Server;

/// <summary>
/// Builds the HTTP server. <see cref="ServerRunner"/> starts it with StartAsync (never Run, which would block the UI).
/// Address, port and storage path come from <see cref="ServerSettings"/>; the rest from appsettings.json next to the .exe.
/// </summary>
public static class ServerHost
{
    public static WebApplication Build(ServerSettings settings, ILoggerProvider uiLog)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });
        IConfiguration config = builder.Configuration;

        builder.Logging.AddProvider(uiLog);

        builder.Services.AddDbContext<Prn212PQlbtContext>(options => options.UseSqlServer(config.GetConnectionString("Default")));
        builder.Services.AddSingleton(new SessionStore(TimeSpan.FromMinutes(config.GetValue("Session:IdleMinutes", 120))));
        builder.Services.AddSingleton(new FileStorage(settings.StoragePath));
        // Stop button: give in-flight requests a few seconds, then abort them.
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));

        builder.Services
            .AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.Teacher, policy => policy.RequireRole(Policies.Teacher));
            options.AddPolicy(Policies.Student, policy => policy.RequireRole(Policies.Student));
        });

        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        long maxUploadBytes = config.GetValue<long>("Upload:MaxMegabytes", 500) * 1024 * 1024;
        IPAddress address = IPAddress.Parse(settings.BindAddress);
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = maxUploadBytes;
            kestrel.Listen(address, settings.Port);
        });

        WebApplication app = builder.Build();

        // Unhandled exceptions -> 500 with the usual ApiError body, never a stack trace to the client.
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            Exception? error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            app.Logger.LogError(error, "Unhandled error on {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ApiError { Status = ApiStatus.Error, Error = "Internal server error." }, ApiJson.Options);
        }));

        // One log line per request for the server window.
        app.Use(async (context, next) =>
        {
            await next(context);
            app.Logger.LogInformation("{Method} {Path} -> {Status} ({User})",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, context.User.Identity?.Name ?? "anonymous");
        });

        app.UseAuthentication();
        app.UseAuthorization();

        AuthEndpoints.Map(app);
        TeacherEndpoints.Map(app);
        StudentEndpoints.Map(app);
        SubmissionEndpoints.Map(app);

        app.Logger.LogInformation("Storage root: {Root}", app.Services.GetRequiredService<FileStorage>().Root);
        return app;
    }
}
