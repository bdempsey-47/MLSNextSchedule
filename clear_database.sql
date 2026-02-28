-- Clear all data and reset sequences
-- Run this in SQL Server Management Studio or through dotnet ef

PRINT 'Clearing MLSNext database...'

-- Disable foreign key constraints temporarily
EXEC sp_MSForEachTable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'

-- Delete all data
DELETE FROM [Matches]
DELETE FROM [Venues]
DELETE FROM [Teams]
DELETE FROM [AgeGroups]
DELETE FROM [Competitions]
DELETE FROM [Regions]
DELETE FROM [Divisions]
DELETE FROM [Leagues]

-- Re-enable foreign key constraints
EXEC sp_MSForEachTable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'

PRINT 'Database cleared successfully'
PRINT 'Ready to repopulate with fresh seasonal data'
