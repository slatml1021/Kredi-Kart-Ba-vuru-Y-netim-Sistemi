using CreditCardApplication.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CardType> CardTypes => Set<CardType>();
    public DbSet<CardApplication> CardApplications => Set<CardApplication>();
    public DbSet<ApplicationHistory> ApplicationHistories => Set<ApplicationHistory>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureCustomers(modelBuilder);
        ConfigureCardsAndApplications(modelBuilder);
        ConfigureHistoryAndAudit(modelBuilder);
        SeedReferenceData(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RegistrationNumber).IsUnique();
            entity.Property(x => x.RegistrationNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(30).IsRequired();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });
    }

    private static void ConfigureCustomers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CustomerNumber).IsUnique();
            entity.HasIndex(x => x.NationalIdentityNumber).IsUnique();
            entity.Property(x => x.CustomerNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.NationalIdentityNumber).HasMaxLength(11).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(11).IsRequired();
            entity.Property(x => x.EmailAddress).HasMaxLength(150).IsRequired();
            entity.Property(x => x.MonthlyNetIncome).HasPrecision(18, 2);
            entity.Property(x => x.OtherBankTotalCardLimit).HasPrecision(18, 2);
        });
    }

    private static void ConfigureCardsAndApplications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CardType>(entity =>
        {
            entity.ToTable("CardTypes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.Bin).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Bin).HasMaxLength(8).IsRequired();
            entity.Property(x => x.MinimumLimit).HasPrecision(18, 2);
            entity.Property(x => x.MaximumLimit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CardApplication>(entity =>
        {
            entity.ToTable("CardApplications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ApplicationNumber).IsUnique();
            entity.Property(x => x.ApplicationNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.RequestedLimit).HasPrecision(18, 2);
            entity.Property(x => x.ApprovedLimit).HasPrecision(18, 2);
            entity.Property(x => x.EvaluationNote).HasMaxLength(500);
            entity.HasOne(x => x.Customer).WithMany(x => x.Applications).HasForeignKey(x => x.CustomerId);
            entity.HasOne(x => x.CardType).WithMany(x => x.Applications).HasForeignKey(x => x.CardTypeId);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EvaluatedByUser).WithMany().HasForeignKey(x => x.EvaluatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditCard>(entity =>
        {
            entity.ToTable("CreditCards");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CardApplicationId).IsUnique();
            entity.Property(x => x.MaskedCardNumber).HasMaxLength(24).IsRequired();
            entity.Property(x => x.CardLimit).HasPrecision(18, 2);
            entity.HasOne(x => x.CardApplication).WithOne(x => x.CreditCard)
                .HasForeignKey<CreditCard>(x => x.CardApplicationId);
        });
    }

    private static void ConfigureHistoryAndAudit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationHistory>(entity =>
        {
            entity.ToTable("ApplicationHistories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasOne(x => x.CardApplication).WithMany(x => x.Histories).HasForeignKey(x => x.CardApplicationId);
            entity.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoginHistory>(entity =>
        {
            entity.ToTable("LoginHistories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RegistrationNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.FailureReason).HasMaxLength(250);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.Detail).HasMaxLength(1000);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void SeedReferenceData(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Officer", CreatedAtUtc = seedDate },
            new Role { Id = 2, Name = "Manager", CreatedAtUtc = seedDate });

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, RegistrationNumber = "BSP000001", FullName = "Sıla Temel", PasswordHash = "DEMO_ONLY", CreatedAtUtc = seedDate },
            new User { Id = 2, RegistrationNumber = "BSP000002", FullName = "Ayşe Yılmaz", PasswordHash = "DEMO_ONLY", CreatedAtUtc = seedDate });

        modelBuilder.Entity<UserRole>().HasData(
            new UserRole { UserId = 1, RoleId = 1 },
            new UserRole { UserId = 2, RoleId = 2 });

        modelBuilder.Entity<CardType>().HasData(
            new CardType { Id = 1, Name = "Classic", Bin = "45000101", MinimumLimit = 5_000, MaximumLimit = 50_000, CreatedAtUtc = seedDate },
            new CardType { Id = 2, Name = "Gold", Bin = "45000102", MinimumLimit = 15_000, MaximumLimit = 150_000, CreatedAtUtc = seedDate },
            new CardType { Id = 3, Name = "Platinum", Bin = "45000103", MinimumLimit = 30_000, MaximumLimit = 300_000, CreatedAtUtc = seedDate },
            new CardType { Id = 4, Name = "Premium", Bin = "45000104", MinimumLimit = 50_000, MaximumLimit = 500_000, CreatedAtUtc = seedDate });
    }
}
