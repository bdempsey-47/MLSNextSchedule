using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YSS.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Use connection string from args if provided (for CI/CD), otherwise use local default
        var connectionString = args.Length > 0
            ? args[0]
            : "Server=(localdb)\\MSSQLLocalDB;Database=YSS;Trusted_Connection=true;Encrypt=false;";

        optionsBuilder.UseSqlServer(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
