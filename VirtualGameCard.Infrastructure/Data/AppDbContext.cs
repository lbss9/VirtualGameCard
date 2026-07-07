using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<GiftCardPurchase> GiftCardPurchases => Set<GiftCardPurchase>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).IsRequired().HasMaxLength(256).HasColumnType("citext");
            e.Property(u => u.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.TokenHash);
            e.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
            e.Ignore(t => t.IsActive);
            e.HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GiftCardPurchase>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.AmountInCents).IsRequired();
            e.Property(p => p.Code).HasMaxLength(100);
            e.Property(p => p.IdempotencyKey).IsRequired().HasMaxLength(100);
            e.Property(p => p.PaymentReference).IsRequired().HasMaxLength(100);
            e.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(30);
            e.Property(p => p.Platform).HasConversion<string>().HasMaxLength(30);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(p => p.CreatedAt).IsRequired();

            e.HasIndex(p => p.Code).IsUnique();
            e.HasIndex(p => new { p.UserId, p.IdempotencyKey }).IsUnique();
            e.HasIndex(p => p.PaymentReference).IsUnique();
            e.HasIndex(p => new { p.UserId, p.CreatedAt });

            e.HasOne(p => p.User)
                .WithMany(u => u.Purchases)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_GiftCardPurchase_AmountInCents",
                    "\"AmountInCents\" BETWEEN 500 AND 25000 AND \"AmountInCents\" % 500 = 0"
                );
            });
        });

        modelBuilder.Entity<EmailVerificationToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.User)
                .WithMany(x => x.EmailVerificationTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SupportTicket>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Subject).HasMaxLength(120).IsRequired();
            e.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.User)
                .WithMany(x => x.SupportTickets)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Message).HasMaxLength(500).IsRequired();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => new { x.UserId, x.Read });
            e.HasOne(x => x.User)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RefreshSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ExpiresAt });
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.Ignore(x => x.IsActive);
            e.HasOne(x => x.User)
                .WithMany(x => x.RefreshSessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PaymentWebhookEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProviderEventId).IsUnique();
            e.Property(x => x.ProviderEventId).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.PurchaseId);
        });
    }
}
