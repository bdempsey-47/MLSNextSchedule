#r "nuget: Microsoft.Data.SqlClient, 6.0.1"
using Microsoft.Data.SqlClient;

var conn = new SqlConnection("Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
conn.AccessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
conn.Open();

Console.WriteLine("=== Long Island Slammers per-age-group ELO ===");
var cmd = new SqlCommand(@"
    SELECT t.Name, t.Program, ag.Name as AgeGroup, e.EloRating
    FROM TeamAgeGroupElos e
    INNER JOIN Teams t ON e.TeamId = t.Id
    INNER JOIN AgeGroups ag ON e.AgeGroupId = ag.Id
    WHERE t.Name = 'Long Island Slammers'
    ORDER BY ag.Name", conn);
var r = cmd.ExecuteReader();
while(r.Read())
    Console.WriteLine($"  {r["Name"]} ({r["Program"]}) {r["AgeGroup"]}: ELO={r["EloRating"]}");
r.Close();

Console.WriteLine("\n=== Total rows in TeamAgeGroupElos ===");
var cmd2 = new SqlCommand("SELECT COUNT(*) FROM TeamAgeGroupElos", conn);
Console.WriteLine($"  {cmd2.ExecuteScalar()} rows");

Console.WriteLine("\n=== Top 5 U17 Academy by per-age-group ELO ===");
var cmd3 = new SqlCommand(@"
    SELECT TOP 5 t.Name, e.EloRating
    FROM TeamAgeGroupElos e
    INNER JOIN Teams t ON e.TeamId = t.Id
    INNER JOIN AgeGroups ag ON e.AgeGroupId = ag.Id
    WHERE ag.Name = 'U17' AND t.Program = 'AG'
    ORDER BY e.EloRating DESC", conn);
var r3 = cmd3.ExecuteReader();
while(r3.Read())
    Console.WriteLine($"  {r3["Name"]}: {r3["EloRating"]}");
r3.Close();

conn.Close();
