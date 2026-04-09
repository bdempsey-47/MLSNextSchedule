using Microsoft.EntityFrameworkCore;
using YSS.Data.Entities;

namespace YSS.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Match> Matches { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<Venue> Venues { get; set; }
    public DbSet<League> Leagues { get; set; }
    public DbSet<Division> Divisions { get; set; }
    public DbSet<Region> Regions { get; set; }
    public DbSet<Competition> Competitions { get; set; }
    public DbSet<AgeGroup> AgeGroups { get; set; }
    public DbSet<TeamAgeGroupElo> TeamAgeGroupElos { get; set; }
    public DbSet<RawIngestionLog> RawIngestionLogs { get; set; }
    public DbSet<HomepageSnapshot> HomepageSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Match entity configuration
        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(m => m.MatchId);
            entity.Property(m => m.MatchId).HasMaxLength(50).IsRequired();

            // UNIQUE constraint on MatchId (natural key)
            entity.HasIndex(m => m.MatchId).IsUnique();

            // Foreign keys
            entity.HasOne(m => m.HomeTeam)
                .WithMany(t => t.HomeMatches)
                .HasForeignKey(m => m.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.AwayTeam)
                .WithMany(t => t.AwayMatches)
                .HasForeignKey(m => m.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Venue)
                .WithMany(v => v.Matches)
                .HasForeignKey(m => m.VenueId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Region)
                .WithMany(r => r.Matches)
                .HasForeignKey(m => m.RegionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Competition)
                .WithMany(c => c.Matches)
                .HasForeignKey(m => m.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.AgeGroup)
                .WithMany(a => a.Matches)
                .HasForeignKey(m => m.AgeGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(m => m.Score).HasMaxLength(20);
            entity.Property(m => m.Gender).HasMaxLength(20);
        });

        // Team entity configuration
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Program).HasMaxLength(2).IsRequired();
            entity.Property(t => t.LogoUrl).HasMaxLength(500);
            entity.HasIndex(t => new { t.Name, t.Program }).IsUnique();
        });

        // Venue entity configuration
        modelBuilder.Entity<Venue>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(v => v.Name).IsUnique();
        });

        // Division entity configuration (Homegrown/Academy)
        modelBuilder.Entity<Division>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
            entity.Property(d => d.TournamentId).IsRequired();
            entity.HasIndex(d => new { d.LeagueId, d.Name }).IsUnique();

            entity.HasOne(d => d.League)
                .WithMany(l => l.Divisions)
                .HasForeignKey(d => d.LeagueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // League entity configuration
        modelBuilder.Entity<League>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(l => l.Name).IsUnique();
        });

        // Region entity configuration (geographic regions within a division)
        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(r => new { r.DivisionId, r.Name }).IsUnique();

            entity.HasOne(r => r.Division)
                .WithMany(d => d.Regions)
                .HasForeignKey(r => r.DivisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Competition entity configuration
        modelBuilder.Entity<Competition>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(c => c.Name).IsUnique();
        });

        // AgeGroup entity configuration
        modelBuilder.Entity<AgeGroup>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(a => a.Name).IsUnique();
        });

        // TeamAgeGroupElo entity configuration
        modelBuilder.Entity<TeamAgeGroupElo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TeamId, e.AgeGroupId }).IsUnique();

            entity.HasOne(e => e.Team)
                .WithMany()
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AgeGroup)
                .WithMany()
                .HasForeignKey(e => e.AgeGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RawIngestionLog entity configuration
        modelBuilder.Entity<RawIngestionLog>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.RawHtml).IsRequired();
        });
    }
}
