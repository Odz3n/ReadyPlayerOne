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
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<SessionParticipant> SessionParticipants => Set<SessionParticipant>();
    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            e.HasIndex(x => x.TokenHash)
                .IsUnique();

            e.HasIndex(x => x.FamilyId);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GameSession>(e =>
        {
            e.HasOne(s => s.Game)
                .WithMany(g => g.Sessions)
                .HasForeignKey(s => s.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Host)
                .WithMany(u => u.HostedSessions)
                .HasForeignKey(s => s.HostId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SessionParticipant>(e =>
        {
            e.HasIndex(p => new
            {
                p.GameSessionId,
                p.UserId
            }).IsUnique();

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
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}