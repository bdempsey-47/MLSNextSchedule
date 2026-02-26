using Microsoft.EntityFrameworkCore;
using MLSNext.Data.Entities;

namespace MLSNext.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Match> Matches { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<Venue> Venues { get; set; }
    public DbSet<Division> Divisions { get; set; }
    public DbSet<Competition> Competitions { get; set; }
    public DbSet<AgeGroup> AgeGroups { get; set; }
    public DbSet<RawIngestionLog> RawIngestionLogs { get; set; }

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

            entity.HasOne(m => m.Division)
                .WithMany(d => d.Matches)
                .HasForeignKey(m => m.DivisionId)
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
            entity.HasIndex(t => t.Name).IsUnique();
        });

        // Venue entity configuration
        modelBuilder.Entity<Venue>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(v => v.Name).IsUnique();
        });

        // Division entity configuration
        modelBuilder.Entity<Division>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(d => d.Name).IsUnique();
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

        // RawIngestionLog entity configuration
        modelBuilder.Entity<RawIngestionLog>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.RawHtml).IsRequired();
        });
    }
}
