using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without starting the host. The
/// connection string is a placeholder: generating a migration connects to no database.
/// </summary>
public sealed class DesignTimeControlPlaneDbContextFactory : IDesignTimeDbContextFactory<ControlPlaneDbContext>
{
    public ControlPlaneDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ControlPlaneDbContext>();
        ControlPlaneDbContextOptions.Configure(builder, "Host=design-time");
        return new ControlPlaneDbContext(builder.Options);
    }
}
