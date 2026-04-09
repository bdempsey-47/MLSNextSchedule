#r "nuget: Microsoft.Data.SqlClient, 6.0.1"
using Microsoft.Data.SqlClient;

var conn = new SqlConnection("Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
conn.AccessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
conn.Open();

Console.WriteLine("=== Regions matching NJ/Cup ===");
var cmd = new SqlCommand("SELECT Name FROM Regions WHERE Name LIKE '%NJ%' OR Name LIKE '%Cup%' OR Name LIKE '%Qualifier%'", conn);
var r = cmd.ExecuteReader();
while(r.Read()) Console.WriteLine("  " + r["Name"]);
r.Close();

Console.WriteLine("\n=== All divisions ===");
var cmd2 = new SqlCommand("SELECT Name, TournamentId FROM Divisions ORDER BY TournamentId", conn);
var r2 = cmd2.ExecuteReader();
while(r2.Read()) Console.WriteLine($"  T{r2["TournamentId"]}: {r2["Name"]}");
r2.Close();

Console.WriteLine("\n=== Most recent 10 matches by CreatedAt ===");
var cmd3 = new SqlCommand(@"
    SELECT TOP 10 m.MatchId, m.MatchDateUtc, r.Name AS Region, c.Name AS Competition, m.CreatedAt
    FROM Matches m
    JOIN Regions r ON m.RegionId = r.Id
    JOIN Competitions c ON m.CompetitionId = c.Id
    ORDER BY m.CreatedAt DESC", conn);
var r3 = cmd3.ExecuteReader();
while(r3.Read()) Console.WriteLine($"  {r3["MatchId"]} | {r3["MatchDateUtc"]:MM/dd/yy} | {r3["Region"],-25} | {r3["Competition"],-15} | created {r3["CreatedAt"]:HH:mm:ss}");
r3.Close();

conn.Close();
