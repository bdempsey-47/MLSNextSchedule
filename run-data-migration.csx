// Quick script to run data migration SQL against Azure SQL using token auth
// Usage: set AZURE_SQL_ACCESS_TOKEN env var, then dotnet-script run-data-migration.csx

#r "nuget: Microsoft.Data.SqlClient, 6.0.1"
using Microsoft.Data.SqlClient;

var token = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
if (string.IsNullOrEmpty(token)) { Console.WriteLine("ERROR: AZURE_SQL_ACCESS_TOKEN not set"); return; }

var connStr = "Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
var conn = new SqlConnection(connStr);
conn.AccessToken = token;
await conn.OpenAsync();
Console.WriteLine("Connected to Azure SQL");

// Run SQL commands one at a time (sqlcmd GO separator not supported)
var commands = new[]
{
    // Phase 2a: Update single-program AG teams
    @"UPDATE t SET t.Program = 'AG'
      FROM Teams t
      WHERE t.Program = 'HG'
        AND EXISTS (
          SELECT 1 FROM Matches m
          INNER JOIN Regions r ON m.RegionId = r.Id
          INNER JOIN Divisions d ON r.DivisionId = d.Id
          INNER JOIN Competitions c ON m.CompetitionId = c.Id
          WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
            AND (d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD'))
        )
        AND NOT EXISTS (
          SELECT 1 FROM Matches m
          INNER JOIN Regions r ON m.RegionId = r.Id
          INNER JOIN Divisions d ON r.DivisionId = d.Id
          INNER JOIN Competitions c ON m.CompetitionId = c.Id
          WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
            AND d.TournamentId IN (12, 75)
            AND c.Name NOT IN ('AD Showcase', 'AD')
        )",

    // Phase 2b: Insert new AG rows for dual-program teams
    @"INSERT INTO Teams (Name, Program, LogoUrl, EloRating)
      SELECT DISTINCT t.Name, 'AG', t.LogoUrl, 1500
      FROM Teams t
      WHERE t.Program = 'HG'
        AND EXISTS (
          SELECT 1 FROM Matches m
          INNER JOIN Regions r ON m.RegionId = r.Id
          INNER JOIN Divisions d ON r.DivisionId = d.Id
          INNER JOIN Competitions c ON m.CompetitionId = c.Id
          WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
            AND (d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD'))
        )
        AND EXISTS (
          SELECT 1 FROM Matches m
          INNER JOIN Regions r ON m.RegionId = r.Id
          INNER JOIN Divisions d ON r.DivisionId = d.Id
          INNER JOIN Competitions c ON m.CompetitionId = c.Id
          WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
            AND d.TournamentId IN (12, 75)
            AND c.Name NOT IN ('AD Showcase', 'AD')
        )",

    // Phase 2c: Re-point HomeTeamId for Academy matches
    @"UPDATE m SET m.HomeTeamId = ag.Id
      FROM Matches m
      INNER JOIN Teams hg ON m.HomeTeamId = hg.Id AND hg.Program = 'HG'
      INNER JOIN Teams ag ON ag.Name = hg.Name AND ag.Program = 'AG'
      INNER JOIN Regions r ON m.RegionId = r.Id
      INNER JOIN Divisions d ON r.DivisionId = d.Id
      INNER JOIN Competitions c ON m.CompetitionId = c.Id
      WHERE d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD')",

    // Phase 2c: Re-point AwayTeamId for Academy matches
    @"UPDATE m SET m.AwayTeamId = ag.Id
      FROM Matches m
      INNER JOIN Teams hg ON m.AwayTeamId = hg.Id AND hg.Program = 'HG'
      INNER JOIN Teams ag ON ag.Name = hg.Name AND ag.Program = 'AG'
      INNER JOIN Regions r ON m.RegionId = r.Id
      INNER JOIN Divisions d ON r.DivisionId = d.Id
      INNER JOIN Competitions c ON m.CompetitionId = c.Id
      WHERE d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD')",
};

var labels = new[] { "Phase 2a: single-program AG teams", "Phase 2b: new AG rows for dual-program", "Phase 2c: re-point HomeTeamId", "Phase 2c: re-point AwayTeamId" };

var txn = conn.BeginTransaction();
try
{
    for (int i = 0; i < commands.Length; i++)
    {
        var cmd = new SqlCommand(commands[i], conn, txn);
        cmd.CommandTimeout = 60;
        var rows = await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"{labels[i]}: {rows} rows affected");
    }

    // Verification
    var v1 = new SqlCommand("SELECT COUNT(*) FROM Teams WHERE Program NOT IN ('AG','HG')", conn, txn);
    Console.WriteLine($"Teams with invalid program: {await v1.ExecuteScalarAsync()}");

    var v2 = new SqlCommand(@"SELECT COUNT(*) FROM Matches m
      INNER JOIN Regions r ON m.RegionId = r.Id
      INNER JOIN Divisions d ON r.DivisionId = d.Id
      INNER JOIN Competitions c ON m.CompetitionId = c.Id
      INNER JOIN Teams ht ON m.HomeTeamId = ht.Id
      INNER JOIN Teams at2 ON m.AwayTeamId = at2.Id
      WHERE (d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD'))
        AND (ht.Program = 'HG' OR at2.Program = 'HG')", conn, txn);
    Console.WriteLine($"Academy matches still pointing to HG teams: {await v2.ExecuteScalarAsync()}");

    var v3 = new SqlCommand("SELECT Program, COUNT(*) as Cnt FROM Teams GROUP BY Program", conn, txn);
    var reader = await v3.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        Console.WriteLine($"  Program={reader["Program"]}, Count={reader["Cnt"]}");
    reader.Close();

    txn.Commit();
    Console.WriteLine("Transaction committed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    txn.Rollback();
    Console.WriteLine("Transaction rolled back.");
}
finally
{
    conn.Close();
}
