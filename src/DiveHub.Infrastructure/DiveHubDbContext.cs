using DiveHub.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Infrastructure;

public sealed class DiveHubDbContext(
    DbContextOptions<DiveHubDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<DiveSite> DiveSites => Set<DiveSite>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<DiveEvent> DiveEvents => Set<DiveEvent>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<CustomerNotification> CustomerNotifications =>
        Set<CustomerNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SiteSettings>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Address).HasMaxLength(500);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.WhatsApp).HasMaxLength(50);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.Website).HasMaxLength(300);
            b.Property(x => x.Latitude).HasPrecision(9, 6);
            b.Property(x => x.Longitude).HasPrecision(9, 6);

            b.HasData(new SiteSettings
            {
                Id = 1,
                Name = "Dive Hub"
            });
        });

        modelBuilder.Entity<DiveSite>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Latitude).HasPrecision(9, 6);
            b.Property(x => x.Longitude).HasPrecision(9, 6);
        });

        modelBuilder.Entity<Activity>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Price).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.RequiresBoat).HasDefaultValue(false);
        });

        modelBuilder.Entity<Boat>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<StaffMember>(b =>
        {
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Biography).HasMaxLength(4000);
            b.Property(x => x.CertificationAgency).HasMaxLength(100);
            b.Property(x => x.CertificationNumber).HasMaxLength(100);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL");
        });

        modelBuilder.Entity<Reservation>(b =>
        {
            b.Property(x => x.ActivityDate).HasColumnType("date");

            b.Property(x => x.Status).IsConcurrencyToken();

            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.TotalPrice).HasPrecision(18, 2);
            b.Property(x => x.CustomerNotes).HasMaxLength(2000);

            b.Property(x => x.CustomerFullName).HasMaxLength(160);
            b.Property(x => x.ContactEmail).HasMaxLength(254);
            b.Property(x => x.ContactPhone).HasMaxLength(50);
            b.Property(x => x.ContactWhatsApp).HasMaxLength(50);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Activity>()
                .WithMany()
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Boat>()
                .WithMany()
                .HasForeignKey(x => x.BoatId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<StaffMember>()
                .WithMany()
                .HasForeignKey(x => x.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.BoatId, x.ActivityDate })
                .IsUnique()
                .HasFilter(
                    "[BoatId] IS NOT NULL AND [Status] IN (1, 2)");

            b.HasIndex(x => new { x.InstructorId, x.ActivityDate })
                .IsUnique()
                .HasFilter(
                    "[InstructorId] IS NOT NULL AND [Status] IN (1, 2)");
        });

        modelBuilder.Entity<CustomerNotification>(b =>
        {
            b.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.Message)
                .HasMaxLength(2000)
                .IsRequired();

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Reservation>()
                .WithMany()
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new
            {
                x.CustomerUserId,
                x.ReadAtUtc,
                x.CreatedAtUtc
            });

            b.HasIndex(x => new
            {
                x.ReservationId,
                x.Kind
            })
            .IsUnique()
            .HasFilter("[ReservationId] IS NOT NULL");
        });

        modelBuilder.Entity<DiveEvent>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(4000);
        });

        modelBuilder.Entity<MediaAsset>(b =>
        {
            b.Property(x => x.FileName).HasMaxLength(255);
            b.Property(x => x.RelativePath).HasMaxLength(500);
            b.Property(x => x.AltText).HasMaxLength(300);
            b.Property(x => x.ContentType).HasMaxLength(100);

            b.HasOne<DiveSite>()
                .WithMany()
                .HasForeignKey(x => x.DiveSiteId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Activity>()
                .WithMany()
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<StaffMember>()
                .WithMany()
                .HasForeignKey(x => x.StaffMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => x.StaffMemberId)
                .IsUnique()
                .HasFilter("[StaffMemberId] IS NOT NULL");
        });
    }
}