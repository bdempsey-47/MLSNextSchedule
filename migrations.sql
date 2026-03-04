IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE TABLE [AgeGroups] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_AgeGroups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE TABLE [Competitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Competitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE TABLE [Divisions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Divisions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE TABLE [RawIngestionLogs] (
        [Id] int NOT NULL IDENTITY,
        [FetchedAt] datetime2 NOT NULL,
        [PageNumber] int NOT NULL,
        [RawHtml] nvarchar(max) NOT NULL,
        [ParsedMatchCount] int NOT NULL,
        CONSTRAINT [PK_RawIngestionLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE TABLE [Teams] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE TABLE [Venues] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Venues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE TABLE [Matches] (
        [MatchId] nvarchar(50) NOT NULL,
        [HomeTeamId] int NOT NULL,
        [AwayTeamId] int NOT NULL,
        [VenueId] int NOT NULL,
        [DivisionId] int NOT NULL,
        [CompetitionId] int NOT NULL,
        [AgeGroupId] int NOT NULL,
        [MatchDateUtc] datetime2 NOT NULL,
        [Score] nvarchar(20) NULL,
        [Gender] nvarchar(20) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Matches] PRIMARY KEY ([MatchId]),
        CONSTRAINT [FK_Matches_AgeGroups_AgeGroupId] FOREIGN KEY ([AgeGroupId]) REFERENCES [AgeGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Competitions_CompetitionId] FOREIGN KEY ([CompetitionId]) REFERENCES [Competitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Divisions_DivisionId] FOREIGN KEY ([DivisionId]) REFERENCES [Divisions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Teams_AwayTeamId] FOREIGN KEY ([AwayTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Teams_HomeTeamId] FOREIGN KEY ([HomeTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Venues_VenueId] FOREIGN KEY ([VenueId]) REFERENCES [Venues] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AgeGroups_Name] ON [AgeGroups] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Competitions_Name] ON [Competitions] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Divisions_Name] ON [Divisions] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_AgeGroupId] ON [Matches] ([AgeGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_AwayTeamId] ON [Matches] ([AwayTeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_CompetitionId] ON [Matches] ([CompetitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_DivisionId] ON [Matches] ([DivisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_HomeTeamId] ON [Matches] ([HomeTeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Matches_MatchId] ON [Matches] ([MatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_VenueId] ON [Matches] ([VenueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Teams_Name] ON [Teams] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Venues_Name] ON [Venues] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226183429_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260226183429_InitialCreate', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    ALTER TABLE [Matches] DROP CONSTRAINT [FK_Matches_Divisions_DivisionId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    DROP INDEX [IX_Divisions_Name] ON [Divisions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    EXEC sp_rename N'[Matches].[DivisionId]', N'RegionId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    EXEC sp_rename N'[Matches].[IX_Matches_DivisionId]', N'IX_Matches_RegionId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    ALTER TABLE [Divisions] ADD [LeagueId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    ALTER TABLE [Divisions] ADD [TournamentId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    CREATE TABLE [Leagues] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Leagues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    CREATE TABLE [Regions] (
        [Id] int NOT NULL IDENTITY,
        [DivisionId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Regions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Regions_Divisions_DivisionId] FOREIGN KEY ([DivisionId]) REFERENCES [Divisions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Divisions_LeagueId_Name] ON [Divisions] ([LeagueId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Leagues_Name] ON [Leagues] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Regions_DivisionId_Name] ON [Regions] ([DivisionId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    ALTER TABLE [Divisions] ADD CONSTRAINT [FK_Divisions_Leagues_LeagueId] FOREIGN KEY ([LeagueId]) REFERENCES [Leagues] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    ALTER TABLE [Matches] ADD CONSTRAINT [FK_Matches_Regions_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [Regions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227190439_RefactorDivisionToRegionHierarchy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260227190439_RefactorDivisionToRegionHierarchy', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227213955_IncreaseScoreColumnSize'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Matches]') AND [c].[name] = N'Score');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Matches] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Matches] ALTER COLUMN [Score] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260227213955_IncreaseScoreColumnSize'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260227213955_IncreaseScoreColumnSize', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228234737_AddTeamLogoUrl'
)
BEGIN
    ALTER TABLE [Teams] ADD [LogoUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228234737_AddTeamLogoUrl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228234737_AddTeamLogoUrl', N'10.0.3');
END;

COMMIT;
GO

