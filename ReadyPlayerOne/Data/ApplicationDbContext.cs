using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<SessionParticipant> SessionParticipants => Set<SessionParticipant>();
    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Refresh Tokens
        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.FamilyId);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Genres
        builder.Entity<Genre>(e =>
        {
            e.HasKey(g => g.Id);

            e.Property(g => g.Name)
                .HasMaxLength(100)
                .IsRequired()
                .UseCollation("Latin1_General_100_CI_AS");

            e.HasIndex(g => g.Name).IsUnique();
        });

        // Games
        builder.Entity<Game>(e =>
        {
            e.HasKey(g => g.Id);

            e.Property(g => g.Name).HasMaxLength(100).IsRequired();
            e.Property(g => g.Slug).HasMaxLength(100).IsRequired();
            e.Property(g => g.Description).HasMaxLength(1000);
            e.Property(g => g.CoverImageUrl).HasMaxLength(2048);

            e.HasIndex(g => g.Slug).IsUnique();

            e.HasOne(g => g.Genre)
                .WithMany(g => g.Games)
                .HasForeignKey(g => g.GenreId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Game Sessions
        builder.Entity<GameSession>(e =>
        {
            e.HasKey(s => s.Id);

            e.Property(s => s.Name).HasMaxLength(100).IsRequired();

            e.Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            e.Property(s => s.Mode)
                .HasConversion<string>()
                .HasMaxLength(20);

            e.Property(s => s.CreatedAt).IsRequired();

            e.HasIndex(s => s.GameId);
            e.HasIndex(s => s.HostId);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.CreatedAt);

            e.HasOne(s => s.Game)
                .WithMany(g => g.Sessions)
                .HasForeignKey(s => s.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Host)
                .WithMany(u => u.HostedSessions)
                .HasForeignKey(s => s.HostId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Teams
        builder.Entity<Team>(e =>
        {
            e.HasKey(t => t.Id);

            e.Property(t => t.Name)
                .HasMaxLength(50)
                .IsRequired();

            e.HasIndex(t => new { t.GameSessionId, t.Name })
                .IsUnique();

            e.HasAlternateKey(t => new { t.GameSessionId, t.Id });

            e.HasOne(t => t.GameSession)
                .WithMany(s => s.Teams)
                .HasForeignKey(t => t.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Session Participants
        builder.Entity<SessionParticipant>(e =>
        {
            e.HasKey(p => p.Id);

            e.HasIndex(p => new { p.GameSessionId, p.UserId })
                .IsUnique();

            e.HasIndex(p => new { p.GameSessionId, p.TeamId });

            e.Property(p => p.JoinedAt).IsRequired();
            e.Property(p => p.IsReady).HasDefaultValue(false);

            e.HasOne(p => p.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.GameSession)
                .WithMany(s => s.Participants)
                .HasForeignKey(p => p.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Team)
                .WithMany(t => t.Members)
                .HasForeignKey(p => new { p.GameSessionId, p.TeamId })
                .HasPrincipalKey(t => new { t.GameSessionId, t.Id })
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.AvatarUrl).HasMaxLength(2048);
        });
    }
}