using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YSS.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Check for access token in environment variable (set by ingestion script)
        var accessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");

        if (!string.IsNullOrEmpty(accessToken))
        {
            // Token-based authentication for Azure SQL
            var connection = new SqlConnection(
                "Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
            connection.AccessToken = accessToken;
            optionsBuilder.UseSqlServer(connection);
        }
        else if (args.Length > 0)
        {
            // Connection string provided as argument
            optionsBuilder.UseSqlServer(args[0]);
        }
        else
        {
            // Default to local development
            optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=YSS;Trusted_Connection=true;Encrypt=false;");
        }

        return new AppDbContext(optionsBuilder.Options);
    }
}
