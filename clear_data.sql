-- Clear all test data from MLSNext database
DELETE FROM Matches;
DELETE FROM RawIngestionLogs;
DELETE FROM Regions;
DELETE FROM Divisions;
DELETE FROM Leagues;
DELETE FROM Teams;
DELETE FROM Venues;
DELETE FROM Competitions;
DELETE FROM AgeGroups;

SELECT 'All data cleared successfully' AS Status;
SELECT COUNT(*) AS MatchCount FROM Matches;
