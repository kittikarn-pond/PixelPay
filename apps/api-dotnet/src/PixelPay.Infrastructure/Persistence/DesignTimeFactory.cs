using Microsoft.EntityFrameworkCore.Design;

namespace PixelPay.Infrastructure.Persistence;

/// <summary>For <c>dotnet ef migrations</c>: the server version is fixed, so no database connection is needed.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<PixelPayDbContext>
{
    public PixelPayDbContext CreateDbContext(string[] args) =>
        PixelPayDbContext.Create(Environment.GetEnvironmentVariable("EF_CONNECTION")
                                 ?? "Server=localhost;Database=pixelpay;User=pixelpay;Password=pixelpay");
}
