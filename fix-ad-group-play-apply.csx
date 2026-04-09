#r "nuget: Microsoft.Data.SqlClient, 6.0.1"
using Microsoft.Data.SqlClient;

var conn = new SqlConnection("Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
conn.AccessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
conn.Open();
Console.WriteLine("Connected to Azure SQL");

var txn = conn.BeginTransaction();
try
{
    // Phase A: AG-only teams that were incorrectly left as HG
    // These have NO real HG matches (all their T12 matches are AD-prefixed)
    // Two sub-cases:
    //   A1: Team already has an AG row (from initial migration) → re-point matches to AG row, delete HG row
    //   A2: Team has no AG row → just flip Program from HG to AG

    // A2 first: flip HG→AG for teams that don't already have an AG counterpart
    var a2 = new SqlCommand(@"
        UPDATE t SET t.Program = 'AG'
        FROM Teams t
        WHERE t.Program = 'HG'
          AND NOT EXISTS (SELECT 1 FROM Teams ag WHERE ag.Name = t.Name AND ag.Program = 'AG')
          AND NOT EXISTS (
            SELECT 1 FROM Matches m
            INNER JOIN Competitions c ON m.CompetitionId = c.Id
            INNER JOIN Regions r ON m.RegionId = r.Id
            INNER JOIN Divisions d ON r.DivisionId = d.Id
            WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
              AND d.TournamentId IN (12, 75)
              AND NOT c.Name LIKE 'AD%'
          )
          AND EXISTS (
            SELECT 1 FROM Matches m
            INNER JOIN Competitions c ON m.CompetitionId = c.Id
            WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
              AND c.Name LIKE 'AD%'
          )", conn, txn);
    a2.CommandTimeout = 60;
    Console.WriteLine($"Phase A2 (flip HG→AG, no AG row exists): {a2.ExecuteNonQuery()} teams updated");

    // A1: AG-only teams that DO have an AG counterpart already
    // Re-point their matches to the AG row, then delete the HG row
    // First re-point HomeTeamId
    var a1h = new SqlCommand(@"
        UPDATE m SET m.HomeTeamId = ag.Id
        FROM Matches m
        INNER JOIN Teams hg ON m.HomeTeamId = hg.Id AND hg.Program = 'HG'
        INNER JOIN Teams ag ON ag.Name = hg.Name AND ag.Program = 'AG'
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        WHERE c.Name LIKE 'AD%'
          AND NOT EXISTS (
            SELECT 1 FROM Matches m2
            INNER JOIN Competitions c2 ON m2.CompetitionId = c2.Id
            INNER JOIN Regions r2 ON m2.RegionId = r2.Id
            INNER JOIN Divisions d2 ON r2.DivisionId = d2.Id
            WHERE (m2.HomeTeamId = hg.Id OR m2.AwayTeamId = hg.Id)
              AND d2.TournamentId IN (12, 75)
              AND NOT c2.Name LIKE 'AD%'
          )", conn, txn);
    a1h.CommandTimeout = 60;
    Console.WriteLine($"Phase A1 home (re-point AG-only HG→AG): {a1h.ExecuteNonQuery()} matches");

    // Re-point AwayTeamId
    var a1a = new SqlCommand(@"
        UPDATE m SET m.AwayTeamId = ag.Id
        FROM Matches m
        INNER JOIN Teams hg ON m.AwayTeamId = hg.Id AND hg.Program = 'HG'
        INNER JOIN Teams ag ON ag.Name = hg.Name AND ag.Program = 'AG'
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        WHERE c.Name LIKE 'AD%'
          AND NOT EXISTS (
            SELECT 1 FROM Matches m2
            INNER JOIN Competitions c2 ON m2.CompetitionId = c2.Id
            INNER JOIN Regions r2 ON m2.RegionId = r2.Id
            INNER JOIN Divisions d2 ON r2.DivisionId = d2.Id
            WHERE (m2.HomeTeamId = hg.Id OR m2.AwayTeamId = hg.Id)
              AND d2.TournamentId IN (12, 75)
              AND NOT c2.Name LIKE 'AD%'
          )", conn, txn);
    a1a.CommandTimeout = 60;
    Console.WriteLine($"Phase A1 away (re-point AG-only HG→AG): {a1a.ExecuteNonQuery()} matches");

    // Delete orphaned HG rows (AG-only teams that had an AG counterpart)
    var a1del = new SqlCommand(@"
        DELETE t FROM Teams t
        WHERE t.Program = 'HG'
          AND EXISTS (SELECT 1 FROM Teams ag WHERE ag.Name = t.Name AND ag.Program = 'AG')
          AND NOT EXISTS (
            SELECT 1 FROM Matches m WHERE m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id
          )", conn, txn);
    a1del.CommandTimeout = 60;
    Console.WriteLine($"Phase A1 delete (orphaned HG rows): {a1del.ExecuteNonQuery()} teams deleted");

    // Phase B: Dual-program teams — re-point AD Group Play matches from HG to AG row
    // These teams legitimately have both HG and AG matches
    // Create AG rows for any that don't have one yet
    var b1 = new SqlCommand(@"
        INSERT INTO Teams (Name, Program, LogoUrl, EloRating)
        SELECT DISTINCT t.Name, 'AG', t.LogoUrl, 1500
        FROM Teams t
        WHERE t.Program = 'HG'
          AND NOT EXISTS (SELECT 1 FROM Teams ag WHERE ag.Name = t.Name AND ag.Program = 'AG')
          AND EXISTS (
            SELECT 1 FROM Matches m
            INNER JOIN Competitions c ON m.CompetitionId = c.Id
            WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
              AND c.Name LIKE 'AD%'
          )
          AND EXISTS (
            SELECT 1 FROM Matches m
            INNER JOIN Competitions c ON m.CompetitionId = c.Id
            INNER JOIN Regions r ON m.RegionId = r.Id
            INNER JOIN Divisions d ON r.DivisionId = d.Id
            WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
              AND d.TournamentId IN (12, 75)
              AND NOT c.Name LIKE 'AD%'
          )", conn, txn);
    b1.CommandTimeout = 60;
    Console.WriteLine($"Phase B1 (create missing AG rows for dual teams): {b1.ExecuteNonQuery()} teams created");

    // Re-point AD Group Play HomeTeamId from HG to AG
    var b2h = new SqlCommand(@"
        UPDATE m SET m.HomeTeamId = ag.Id
        FROM Matches m
        INNER JOIN Teams hg ON m.HomeTeamId = hg.Id AND hg.Program = 'HG'
        INNER JOIN Teams ag ON ag.Name = hg.Name AND ag.Program = 'AG'
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        WHERE c.Name LIKE 'AD%'", conn, txn);
    b2h.CommandTimeout = 60;
    Console.WriteLine($"Phase B2 home (re-point AD matches HG→AG): {b2h.ExecuteNonQuery()} matches");

    // Re-point AD Group Play AwayTeamId from HG to AG
    var b2a = new SqlCommand(@"
        UPDATE m SET m.AwayTeamId = ag.Id
        FROM Matches m
        INNER JOIN Teams hg ON m.AwayTeamId = hg.Id AND hg.Program = 'HG'
        INNER JOIN Teams ag ON ag.Name = hg.Name AND ag.Program = 'AG'
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        WHERE c.Name LIKE 'AD%'", conn, txn);
    b2a.CommandTimeout = 60;
    Console.WriteLine($"Phase B2 away (re-point AD matches HG→AG): {b2a.ExecuteNonQuery()} matches");

    // Verification
    Console.WriteLine("\n=== Verification ===");

    var v1 = new SqlCommand("SELECT COUNT(*) FROM Teams WHERE Program NOT IN ('AG','HG')", conn, txn);
    Console.WriteLine($"Teams with invalid program: {v1.ExecuteScalar()}");

    var v2 = new SqlCommand(@"
        SELECT COUNT(*) FROM Matches m
        INNER JOIN Competitions c ON m.CompetitionId = c.Id
        INNER JOIN Teams ht ON m.HomeTeamId = ht.Id
        INNER JOIN Teams at2 ON m.AwayTeamId = at2.Id
        WHERE c.Name LIKE 'AD%'
          AND (ht.Program = 'HG' OR at2.Program = 'HG')", conn, txn);
    Console.WriteLine($"AD matches still pointing to HG teams: {v2.ExecuteScalar()}");

    var v3 = new SqlCommand("SELECT Program, COUNT(*) as Cnt FROM Teams GROUP BY Program", conn, txn);
    var r3 = v3.ExecuteReader();
    while(r3.Read()) Console.WriteLine($"  Program={r3["Program"]}, Count={r3["Cnt"]}");
    r3.Close();

    // Check Long Island Slammers specifically
    var v4 = new SqlCommand("SELECT Id, Name, Program, EloRating FROM Teams WHERE Name = 'Long Island Slammers'", conn, txn);
    var r4 = v4.ExecuteReader();
    Console.WriteLine("\nLong Island Slammers:");
    while(r4.Read()) Console.WriteLine($"  Id={r4["Id"]}, Program={r4["Program"]}, EloRating={r4["EloRating"]}");
    r4.Close();

    txn.Commit();
    Console.WriteLine("\nTransaction committed!");
}
catch (Exception ex)
{
    Console.WriteLine($"\nERROR: {ex.Message}");
    txn.Rollback();
    Console.WriteLine("Transaction rolled back.");
}
finally
{
    conn.Close();
}
