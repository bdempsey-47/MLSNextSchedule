#r "nuget: Microsoft.Data.SqlClient, 6.0.1"
using Microsoft.Data.SqlClient;

var conn = new SqlConnection("Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
conn.AccessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
conn.Open();

Console.WriteLine("=== All competitions ===");
var cmd = new SqlCommand("SELECT Id, Name FROM Competitions ORDER BY Name", conn);
var r = cmd.ExecuteReader();
while(r.Read())
    Console.WriteLine($"  {r["Id"]}: {r["Name"]}");
r.Close();

Console.WriteLine("\n=== Matches by competition + tournament (count) ===");
var cmd2 = new SqlCommand(@"
    SELECT c.Name as Competition, d.TournamentId, COUNT(*) as Cnt
    FROM Matches m
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    INNER JOIN Regions r ON m.RegionId = r.Id
    INNER JOIN Divisions d ON r.DivisionId = d.Id
    GROUP BY c.Name, d.TournamentId
    ORDER BY d.TournamentId, c.Name", conn);
var r2 = cmd2.ExecuteReader();
while(r2.Read())
    Console.WriteLine($"  T{r2["TournamentId"]} | {r2["Competition"],-30} | {r2["Cnt"]} matches");
r2.Close();

conn.Close();
