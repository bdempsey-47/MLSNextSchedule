#r "nuget: Microsoft.Data.SqlClient, 6.0.1"
using Microsoft.Data.SqlClient;

var conn = new SqlConnection("Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
conn.AccessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
conn.Open();
Console.WriteLine("Connected to Azure SQL");

// First: understand the scope of the problem
Console.WriteLine("\n=== AD Group Play matches currently pointing to HG teams ===");
var diag = new SqlCommand(@"
    SELECT COUNT(*) as Cnt
    FROM Matches m
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    INNER JOIN Regions r ON m.RegionId = r.Id
    INNER JOIN Divisions d ON r.DivisionId = d.Id
    INNER JOIN Teams ht ON m.HomeTeamId = ht.Id
    WHERE c.Name = 'AD Group Play' AND ht.Program = 'HG'", conn);
Console.WriteLine($"  Home team is HG: {diag.ExecuteScalar()}");

var diag2 = new SqlCommand(@"
    SELECT COUNT(*) as Cnt
    FROM Matches m
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    INNER JOIN Teams at2 ON m.AwayTeamId = at2.Id
    WHERE c.Name = 'AD Group Play' AND at2.Program = 'HG'", conn);
Console.WriteLine($"  Away team is HG: {diag2.ExecuteScalar()}");

// Check how many HG teams only have AD Group Play matches (should become AG-only)
Console.WriteLine("\n=== HG teams that should be AG-only (all their non-T35 matches are AD-prefixed) ===");
var diag3 = new SqlCommand(@"
    SELECT t.Id, t.Name, t.EloRating
    FROM Teams t
    WHERE t.Program = 'HG'
      AND NOT EXISTS (
        -- No non-AD matches on tournament 12/75
        SELECT 1 FROM Matches m
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        INNER JOIN Regions r ON m.RegionId = r.Id
        INNER JOIN Divisions d ON r.DivisionId = d.Id
        WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
          AND d.TournamentId IN (12, 75)
          AND NOT c.Name LIKE 'AD%'
      )
      AND EXISTS (
        -- But has at least one AD-prefixed match
        SELECT 1 FROM Matches m
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
          AND c.Name LIKE 'AD%'
      )
    ORDER BY t.Name", conn);
var r3 = diag3.ExecuteReader();
int agOnlyCount = 0;
while(r3.Read())
{
    agOnlyCount++;
    if (agOnlyCount <= 10) Console.WriteLine($"  {r3["Id"]}: {r3["Name"]} (ELO={r3["EloRating"]})");
}
if (agOnlyCount > 10) Console.WriteLine($"  ... and {agOnlyCount - 10} more");
Console.WriteLine($"  Total: {agOnlyCount} HG teams that are actually AG-only");
r3.Close();

// Check how many HG teams have BOTH AD and non-AD matches (true dual-program, need AG row created)
Console.WriteLine("\n=== HG teams with AD Group Play matches AND real HG matches (need AG row if not exists) ===");
var diag4 = new SqlCommand(@"
    SELECT t.Id, t.Name
    FROM Teams t
    WHERE t.Program = 'HG'
      AND EXISTS (
        SELECT 1 FROM Matches m
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
          AND c.Name = 'AD Group Play'
      )
      AND EXISTS (
        SELECT 1 FROM Matches m
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        INNER JOIN Regions r ON m.RegionId = r.Id
        INNER JOIN Divisions d ON r.DivisionId = d.Id
        WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
          AND d.TournamentId IN (12, 75)
          AND NOT c.Name LIKE 'AD%'
      )
    ORDER BY t.Name", conn);
var r4 = diag4.ExecuteReader();
int dualCount = 0;
while(r4.Read())
{
    dualCount++;
    Console.WriteLine($"  {r4["Id"]}: {r4["Name"]}");
}
Console.WriteLine($"  Total: {dualCount} dual-program teams with AD Group Play");
r4.Close();

conn.Close();
