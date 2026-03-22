-- Data Migration: Program-Scoped Teams
-- Run AFTER the EF migration (AddProgramToTeam) has been applied.
-- This script splits dual-program teams into separate AG and HG rows
-- and re-points Academy match FKs to the new AG team rows.
--
-- Run inside a transaction on Azure SQL.

BEGIN TRANSACTION;

-- ============================================================
-- Phase 2a: Update single-program teams directly
-- Teams that ONLY appear in Academy matches → set to AG
-- ============================================================

-- Academy competitions define AG program
DECLARE @AcademyCompetitions TABLE (Name NVARCHAR(100));
INSERT INTO @AcademyCompetitions VALUES ('AD Showcase'), ('AD');

-- Teams that appear ONLY in Academy matches (tournament 35 or AD/AD Showcase competitions)
-- and never in Homegrown matches
UPDATE t
SET t.Program = 'AG'
FROM Teams t
WHERE t.Program = 'HG'  -- still at default
  AND EXISTS (
    -- Has at least one Academy match
    SELECT 1 FROM Matches m
    INNER JOIN Regions r ON m.RegionId = r.Id
    INNER JOIN Divisions d ON r.DivisionId = d.Id
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
      AND (d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD'))
  )
  AND NOT EXISTS (
    -- Has NO Homegrown matches
    SELECT 1 FROM Matches m
    INNER JOIN Regions r ON m.RegionId = r.Id
    INNER JOIN Divisions d ON r.DivisionId = d.Id
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
      AND d.TournamentId IN (12, 75)
      AND c.Name NOT IN ('AD Showcase', 'AD')
  );

PRINT 'Phase 2a complete: single-program AG teams updated';

-- ============================================================
-- Phase 2b: For dual-program teams, INSERT new AG rows
-- Existing row stays as HG; new row created for AG
-- ============================================================

INSERT INTO Teams (Name, Program, LogoUrl, EloRating)
SELECT DISTINCT t.Name, 'AG', t.LogoUrl, 1500
FROM Teams t
WHERE t.Program = 'HG'
  -- Has Academy matches
  AND EXISTS (
    SELECT 1 FROM Matches m
    INNER JOIN Regions r ON m.RegionId = r.Id
    INNER JOIN Divisions d ON r.DivisionId = d.Id
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
      AND (d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD'))
  )
  -- AND has Homegrown matches (truly dual-program)
  AND EXISTS (
    SELECT 1 FROM Matches m
    INNER JOIN Regions r ON m.RegionId = r.Id
    INNER JOIN Divisions d ON r.DivisionId = d.Id
    INNER JOIN Competitions c ON m.CompetitionId = c.Id
    WHERE (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id)
      AND d.TournamentId IN (12, 75)
      AND c.Name NOT IN ('AD Showcase', 'AD')
  );

PRINT 'Phase 2b complete: new AG rows inserted for dual-program teams';

-- ============================================================
-- Phase 2c: Re-point Academy match FKs to the new AG team rows
-- ============================================================

-- Update HomeTeamId for Academy matches
UPDATE m
SET m.HomeTeamId = ag.Id
FROM Matches m
INNER JOIN Teams hg ON m.HomeTeamId = hg.Id AND hg.Program = 'HG'
INNER JOIN Teams ag ON ag.Name = hg.Name AND ag.Program = 'AG'
INNER JOIN Regions r ON m.RegionId = r.Id
INNER JOIN Divisions d ON r.DivisionId = d.Id
INNER JOIN Competitions c ON m.CompetitionId = c.Id
WHERE d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD');

-- Update AwayTeamId for Academy matches
UPDATE m
SET m.AwayTeamId = ag.Id
FROM Matches m
INNER JOIN Teams hg ON m.AwayTeamId = hg.Id AND hg.Program = 'HG'
INNER JOIN Teams ag ON ag.Name = hg.Name AND ag.Program = 'AG'
INNER JOIN Regions r ON m.RegionId = r.Id
INNER JOIN Divisions d ON r.DivisionId = d.Id
INNER JOIN Competitions c ON m.CompetitionId = c.Id
WHERE d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD');

PRINT 'Phase 2c complete: Academy match FKs re-pointed to AG teams';

-- ============================================================
-- Phase 2d: Verification queries
-- ============================================================

-- Should return 0
SELECT COUNT(*) AS TeamsWithInvalidProgram
FROM Teams
WHERE Program NOT IN ('AG', 'HG');

-- Should return 0: no Academy match points to an HG team
SELECT COUNT(*) AS AcademyMatchesWithHGTeam
FROM Matches m
INNER JOIN Regions r ON m.RegionId = r.Id
INNER JOIN Divisions d ON r.DivisionId = d.Id
INNER JOIN Competitions c ON m.CompetitionId = c.Id
INNER JOIN Teams ht ON m.HomeTeamId = ht.Id
INNER JOIN Teams at2 ON m.AwayTeamId = at2.Id
WHERE (d.TournamentId = 35 OR c.Name IN ('AD Showcase', 'AD'))
  AND (ht.Program = 'HG' OR at2.Program = 'HG');

-- Dual-program clubs should have exactly 2 rows each
SELECT Name, COUNT(*) AS RowCount
FROM Teams
GROUP BY Name
HAVING COUNT(*) > 1
ORDER BY Name;

PRINT 'Phase 2d: Verification complete — review results above';

-- If everything looks good:
COMMIT TRANSACTION;
-- If something is wrong, uncomment:
-- ROLLBACK TRANSACTION;
