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
    public DbSet<OtherBankCard> OtherBankCards => Set<OtherBankCard>();
    public DbSet<CardType> CardTypes => Set<CardType>();
    public DbSet<CardApplication> CardApplications => Set<CardApplication>();
    public DbSet<ApplicationHistory> ApplicationHistories => Set<ApplicationHistory>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<CustomerConsent> CustomerConsents => Set<CustomerConsent>();
    public DbSet<ApplicationRevisionSnapshot> ApplicationRevisionSnapshots => Set<ApplicationRevisionSnapshot>();
    public DbSet<CardFulfillment> CardFulfillments => Set<CardFulfillment>();
    public DbSet<SupplementaryCardApplication> SupplementaryCardApplications => Set<SupplementaryCardApplication>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();
    public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureCustomers(modelBuilder);
        ConfigureCardsAndApplications(modelBuilder);
        ConfigureHistoryAndAudit(modelBuilder);
        ConfigurePlatformV2(modelBuilder);
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
            entity.Property(x => x.CorporateEmail).HasMaxLength(150);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20);
            entity.Property(x => x.Title).HasMaxLength(100);
            entity.Property(x => x.Department).HasMaxLength(100);
            entity.Property(x => x.Branch).HasMaxLength(120);
            entity.Property(x => x.ProfilePhotoUrl).HasMaxLength(500);
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
            entity.HasIndex(x => x.EmailAddress).IsUnique();
            entity.HasIndex(x => new { x.PhoneCountryCode, x.PhoneNumber }).IsUnique();
            entity.Property(x => x.CustomerNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.NationalIdentityNumber).HasMaxLength(11).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.PhoneCountryCode).HasMaxLength(4).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(11).IsRequired();
            entity.Property(x => x.EmailAddress).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Gender).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EducationLevel).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Occupation).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EmploymentStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.City).HasMaxLength(80).IsRequired();
            entity.Property(x => x.District).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Neighborhood).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(500).IsRequired();
            entity.Property(x => x.MonthlyNetIncome).HasPrecision(18, 2);
            entity.Property(x => x.OtherBankTotalCardLimit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OtherBankCard>(entity =>
        {
            entity.ToTable("OtherBankCards");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CustomerId);
            entity.Property(x => x.BankName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MaskedCardNumber).HasMaxLength(12);
            entity.Property(x => x.CardLimit).HasPrecision(18, 2);
            entity.HasOne(x => x.Customer).WithMany(x => x.OtherBankCards)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.ToTable("CustomerAddresses");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CustomerId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(40).IsRequired();
            entity.Property(x => x.City).HasMaxLength(80).IsRequired();
            entity.Property(x => x.District).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Neighborhood).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Street).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Avenue).HasMaxLength(160);
            entity.Property(x => x.BuildingNo).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ApartmentNo).HasMaxLength(30);
            entity.Property(x => x.Floor).HasMaxLength(30);
            entity.Property(x => x.PostalCode).HasMaxLength(5).IsRequired();
            entity.Property(x => x.FullAddress).HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.Customer).WithMany(x => x.SavedAddresses)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(x => x.DeliveryMethod).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DeliveryAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DeliveryCity).HasMaxLength(80);
            entity.Property(x => x.DeliveryDistrict).HasMaxLength(80);
            entity.Property(x => x.DeliveryNeighborhood).HasMaxLength(120);
            entity.Property(x => x.DeliveryRecipientName).HasMaxLength(120);
            entity.Property(x => x.DeliveryPhone).HasMaxLength(16);
            entity.Property(x => x.DeliveryBranch).HasMaxLength(160);
            entity.Property(x => x.StatementPreference).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PreAssessmentRiskLevel).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PreAssessmentRecommendation).HasMaxLength(40).IsRequired();
            entity.Property(x => x.PreAssessmentPositiveFactorsJson).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.PreAssessmentRiskFactorsJson).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ApplicationNote).HasMaxLength(500);
            entity.Property(x => x.EvaluationNote).HasMaxLength(500);
            entity.Property(x => x.FirstApprovalNote).HasMaxLength(500);
            entity.Property(x => x.LastAssignmentReason).HasMaxLength(500);
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.HasOne(x => x.Customer).WithMany(x => x.Applications).HasForeignKey(x => x.CustomerId);
            entity.HasOne(x => x.CardType).WithMany(x => x.Applications).HasForeignKey(x => x.CardTypeId);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EvaluatedByUser).WithMany().HasForeignKey(x => x.EvaluatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssignedOfficerUser).WithMany().HasForeignKey(x => x.AssignedOfficerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssignedByUser).WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FirstApprovedByUser).WithMany().HasForeignKey(x => x.FirstApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CancelledByUser).WithMany().HasForeignKey(x => x.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditCard>(entity =>
        {
            entity.ToTable("CreditCards");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CardApplicationId).IsUnique();
            entity.Property(x => x.MaskedCardNumber).HasMaxLength(24).IsRequired();
            entity.Property(x => x.CardLimit).HasPrecision(18, 2);
            entity.Property(x => x.RequestedNewLimit).HasPrecision(18, 2);
            entity.Property(x => x.LimitIncreaseStatus).HasMaxLength(30);
            entity.Property(x => x.LimitChangeType).HasMaxLength(20);
            entity.Property(x => x.LimitIncreaseEvaluationNote).HasMaxLength(500);
            entity.HasOne(x => x.CardApplication).WithOne(x => x.CreditCard)
                .HasForeignKey<CreditCard>(x => x.CardApplicationId);
        });
    }

    private static void ConfigurePlatformV2(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.IsRead });
            entity.Property(x => x.Type).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(600).IsRequired();
            entity.Property(x => x.Link).HasMaxLength(300);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SenderUserId, x.RecipientUserId, x.CreatedAtUtc });
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.HasOne(x => x.SenderUser).WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RecipientUser).WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CardApplication).WithMany().HasForeignKey(x => x.CardApplicationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CustomerConsent>(entity =>
        {
            entity.ToTable("CustomerConsents");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CustomerId, x.ConsentType, x.CreatedAtUtc });
            entity.Property(x => x.ConsentType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.TextVersion).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(30).IsRequired();
            entity.HasOne(x => x.Customer).WithMany(x => x.Consents).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CapturedByUser).WithMany().HasForeignKey(x => x.CapturedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationRevisionSnapshot>(entity =>
        {
            entity.ToTable("ApplicationRevisionSnapshots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CardApplicationId, x.RevisionNumber, x.Stage });
            entity.Property(x => x.Stage).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DataJson).HasMaxLength(8000).IsRequired();
            entity.HasOne(x => x.CardApplication).WithMany(x => x.RevisionSnapshots).HasForeignKey(x => x.CardApplicationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CapturedByUser).WithMany().HasForeignKey(x => x.CapturedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CardFulfillment>(entity =>
        {
            entity.ToTable("CardFulfillments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreditCardId).IsUnique();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.TrackingNumber).HasMaxLength(60);
            entity.HasOne(x => x.CreditCard).WithOne(x => x.Fulfillment)
                .HasForeignKey<CardFulfillment>(x => x.CreditCardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplementaryCardApplication>(entity =>
        {
            entity.ToTable("SupplementaryCardApplications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ApplicationNumber).IsUnique();
            entity.Property(x => x.ApplicationNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Relationship).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RequestedLimit).HasPrecision(18, 2);
            entity.Property(x => x.DeliveryMethod).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DeliveryAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EvaluationNote).HasMaxLength(500);
            entity.Property(x => x.MaskedCardNumber).HasMaxLength(24);
            entity.Property(x => x.CardStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FulfillmentStatus).HasMaxLength(30);
            entity.Property(x => x.TrackingNumber).HasMaxLength(60);
            entity.HasOne(x => x.PrimaryCustomer).WithMany().HasForeignKey(x => x.PrimaryCustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PrimaryCreditCard).WithMany().HasForeignKey(x => x.PrimaryCreditCardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SupplementaryHolderCustomer).WithMany().HasForeignKey(x => x.SupplementaryHolderCustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EvaluatedByUser).WithMany().HasForeignKey(x => x.EvaluatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GeneratedDocument>(entity =>
        {
            entity.ToTable("GeneratedDocuments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.DocumentType });
            entity.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EmailStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EmailFailureReason).HasMaxLength(500);
            entity.Property(x => x.VerificationStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.VerificationNote).HasMaxLength(500);
            entity.HasOne(x => x.VerifiedByUser).WithMany().HasForeignKey(x => x.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerAccount>(entity =>
        {
            entity.ToTable("CustomerAccounts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CustomerId).IsUnique();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<CardType>().HasData(
            new CardType { Id = 1, Name = "Classic Visa", Bin = "45000101", MinimumLimit = 5_000, MaximumLimit = 50_000, CreatedAtUtc = seedDate },
            new CardType { Id = 2, Name = "Gold World", Bin = "45000102", MinimumLimit = 15_000, MaximumLimit = 150_000, CreatedAtUtc = seedDate },
            new CardType { Id = 3, Name = "Platinum Visa", Bin = "45000103", MinimumLimit = 30_000, MaximumLimit = 300_000, CreatedAtUtc = seedDate },
            new CardType { Id = 4, Name = "Platinum Plus Visa", Bin = "45000104", MinimumLimit = 50_000, MaximumLimit = 500_000, CreatedAtUtc = seedDate },
            new CardType { Id = 5, Name = "Classic Troy", Bin = "97920101", MinimumLimit = 5_000, MaximumLimit = 50_000, CreatedAtUtc = seedDate },
            new CardType { Id = 6, Name = "Gold Troy", Bin = "97920102", MinimumLimit = 15_000, MaximumLimit = 150_000, CreatedAtUtc = seedDate });
    }
}
