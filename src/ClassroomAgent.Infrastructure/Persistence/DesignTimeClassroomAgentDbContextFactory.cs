using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without starting the installation host, which refuses
/// to start without its settings (US-005 db-design §6.2). The connection string is a placeholder: generating
/// a migration connects to no database. Never used at runtime.
/// </summary>
public sealed class DesignTimeClassroomAgentDbContextFactory : IDesignTimeDbContextFactory<ClassroomAgentDbContext>
{
    public ClassroomAgentDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ClassroomAgentDbContext>();
        ClassroomAgentDbContextOptions.Configure(builder, "Host=design-time");
        return new ClassroomAgentDbContext(builder.Options);
    }
}
