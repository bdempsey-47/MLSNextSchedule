-- Insert test data into MLSNext database
INSERT INTO Leagues (Name) VALUES ('MLS Next');
INSERT INTO Divisions (LeagueId, Name, TournamentId) VALUES (1, 'Academy', 35);
INSERT INTO Regions (DivisionId, Name) VALUES (1, 'Pioneer');
INSERT INTO Teams (Name) VALUES ('Test Team 1');
INSERT INTO Teams (Name) VALUES ('Test Team 2');
INSERT INTO Venues (Name) VALUES ('Test Venue');
INSERT INTO Competitions (Name) VALUES ('AD');
INSERT INTO AgeGroups (Name) VALUES ('U16');
INSERT INTO Matches (MatchId, HomeTeamId, AwayTeamId, VenueId, RegionId, CompetitionId, AgeGroupId, MatchDateUtc, Score, Gender, CreatedAt, UpdatedAt) 
  VALUES ('TEST001', 1, 2, 1, 1, 1, 1, GETUTCDATE(), '1-0', 'Male', GETUTCDATE(), GETUTCDATE());
SELECT 'Test data inserted successfully' AS Status;
SELECT COUNT(*) AS MatchCount FROM Matches;
