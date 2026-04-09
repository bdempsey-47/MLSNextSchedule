#r "nuget: Microsoft.Data.SqlClient, 6.0.1"
using Microsoft.Data.SqlClient;

var conn = new SqlConnection("Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
conn.AccessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
conn.Open();

// First get the team IDs
Console.WriteLine("=== Long Island Slammers teams ===");
var cmd1 = new SqlCommand("SELECT Id, Name, Program, EloRating FROM Teams WHERE Name = 'Long Island Slammers'", conn);
var r1 = cmd1.ExecuteReader();
while(r1.Read())
    Console.WriteLine($"Id={r1["Id"]}, Program={r1["Program"]}, EloRating={r1["EloRating"]}");
r1.Close();

// Get all matches for Long Island Slammers (AG, Id=632)
Console.WriteLine("\n=== Matches for Long Island Slammers AG (Id=632) ===");
var cmd2 = new SqlCommand(@"
    SELECT m.MatchId, m.MatchDateUtc, m.Score,
           ht.Name as HomeTeam, ht.Id as HomeTeamId, ht.Program as HomeProgram,
           at2.Name as AwayTeam, at2.Id as AwayTeamId, at2.Program as AwayProgram,
           c.Name as Competition, d.TournamentId, r.Name as Region
    FROM Matches m
    INNER JOIN Teams ht ON m.HomeTeamId = ht.Id
    INNER JOIN Teams at2 ON m.AwayTeamId = at2.Id
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    INNER JOIN Regions r ON m.RegionId = r.Id
    INNER JOIN Divisions d ON r.DivisionId = d.Id
    WHERE m.HomeTeamId = 632 OR m.AwayTeamId = 632
    ORDER BY m.MatchDateUtc", conn);
var r2 = cmd2.ExecuteReader();
int count = 0;
while(r2.Read())
{
    count++;
    Console.WriteLine($"{r2["MatchDateUtc"]:yyyy-MM-dd} | {r2["HomeTeam"]} ({r2["HomeTeamId"]},{r2["HomeProgram"]}) vs {r2["AwayTeam"]} ({r2["AwayTeamId"]},{r2["AwayProgram"]}) | Score: {r2["Score"]} | {r2["Competition"]} | T{r2["TournamentId"]} | {r2["Region"]}");
}
r2.Close();
Console.WriteLine($"\nTotal matches: {count}");

// Also check matches for the HG version (Id=391)
Console.WriteLine("\n=== Matches for Long Island Slammers HG (Id=391) ===");
var cmd3 = new SqlCommand(@"
    SELECT m.MatchId, m.MatchDateUtc, m.Score,
           ht.Name as HomeTeam, ht.Id as HomeTeamId, ht.Program as HomeProgram,
           at2.Name as AwayTeam, at2.Id as AwayTeamId, at2.Program as AwayProgram,
           c.Name as Competition, d.TournamentId, r.Name as Region
    FROM Matches m
    INNER JOIN Teams ht ON m.HomeTeamId = ht.Id
    INNER JOIN Teams at2 ON m.AwayTeamId = at2.Id
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    INNER JOIN Regions r ON m.RegionId = r.Id
    INNER JOIN Divisions d ON r.DivisionId = d.Id
    WHERE m.HomeTeamId = 391 OR m.AwayTeamId = 391
    ORDER BY m.MatchDateUtc", conn);
var r3 = cmd3.ExecuteReader();
count = 0;
while(r3.Read())
{
    count++;
    Console.WriteLine($"{r3["MatchDateUtc"]:yyyy-MM-dd} | {r3["HomeTeam"]} ({r3["HomeTeamId"]},{r3["HomeProgram"]}) vs {r3["AwayTeam"]} ({r3["AwayTeamId"]},{r3["AwayProgram"]}) | Score: {r3["Score"]} | {r3["Competition"]} | T{r3["TournamentId"]} | {r3["Region"]}");
}
r3.Close();
Console.WriteLine($"\nTotal matches: {count}");

conn.Close();
