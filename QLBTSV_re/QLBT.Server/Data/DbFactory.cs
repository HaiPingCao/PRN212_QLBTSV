using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QLBT.Server.Asp;
using QLBT.Server.Files;
using QLBT.Server.Models;

namespace QLBT.Server.Data;

/// <summary>
/// DbContexts for code outside the HTTP pipeline (admin GUI). Works whether or not the server is running.
/// The HTTP endpoints keep using the DI context from ServerHost.
/// </summary>
public static class DbFactory
{
    private static readonly Lazy<DbContextOptions<Prn212PQlbtContext>> Options = new Lazy<DbContextOptions<Prn212PQlbtContext>>(BuildOptions);

    /// <summary>Short-lived: create one per operation and dispose it.</summary>
    public static Prn212PQlbtContext Create()
    {
        return new Prn212PQlbtContext(Options.Value);
    }

    /// <summary>Same storage root the HTTP server uses (from the saved server settings).</summary>
    public static FileStorage Storage()
    {
        return new FileStorage(ServerSettings.Load().StoragePath);
    }

    private static DbContextOptions<Prn212PQlbtContext> BuildOptions()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        string connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is missing in appsettings.json.");
        return new DbContextOptionsBuilder<Prn212PQlbtContext>()
            .UseSqlServer(connectionString)
            .Options;
    }
}
