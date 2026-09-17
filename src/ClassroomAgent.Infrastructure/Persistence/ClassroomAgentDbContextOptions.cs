using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// Provider and naming options shared by the host and the design-time factory, so the migrations are
/// generated against the model the installation runs (PC-2, PC-5).
/// </summary>
public static class ClassroomAgentDbContextOptions
{
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
