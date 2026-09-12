using CreditCardApplication.Domain.Enums;

namespace CreditCardApplication.Domain.Entities;

public sealed class CardApplication : BaseEntity
{
    public string ApplicationNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int CardTypeId { get; set; }
    public CardType CardType { get; set; } = null!;
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public int? AssignedOfficerUserId { get; set; }
    public User? AssignedOfficerUser { get; set; }
    public int? AssignedByUserId { get; set; }
    public User? AssignedByUser { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public bool AutoAssignmentCompleted { get; set; }
    public string? LastAssignmentReason { get; set; }
    public int EscalationLevel { get; set; }
    public DateTime? LastEscalatedAtUtc { get; set; }
    public bool RequiresSecondApproval { get; set; }
    public int? FirstApprovedByUserId { get; set; }
    public User? FirstApprovedByUser { get; set; }
    public DateTime? FirstApprovedAtUtc { get; set; }
    public string? FirstApprovalNote { get; set; }
    public int? EvaluatedByUserId { get; set; }
    public User? EvaluatedByUser { get; set; }
    public decimal RequestedLimit { get; set; }
    public string DeliveryMethod { get; set; } = "RegisteredAddress";
    public string DeliveryAddress { get; set; } = string.Empty;
    public string? DeliveryCity { get; set; }
    public string? DeliveryDistrict { get; set; }
    public string? DeliveryNeighborhood { get; set; }
    public string? DeliveryRecipientName { get; set; }
    public string? DeliveryPhone { get; set; }
    public string? DeliveryBranch { get; set; }
    public string StatementPreference { get; set; } = "Email";
    public int StatementDay { get; set; } = 15;
    public bool ContactlessEnabled { get; set; } = true;
    public bool InternetShoppingEnabled { get; set; } = true;
    public bool AutomaticLimitIncreaseEnabled { get; set; }
    public DateTime? AutomaticLimitIncreaseConsentAtUtc { get; set; }
    public bool IdentityDocumentConfirmed { get; set; }
    public bool IncomeDocumentConfirmed { get; set; }
    public bool ResidenceDocumentConfirmed { get; set; }
    public bool DuplicateWarningAcknowledged { get; set; }
    public int PreAssessmentScore { get; set; }
    public string PreAssessmentRiskLevel { get; set; } = "MEDIUM";
    public string PreAssessmentRecommendation { get; set; } = "MANUAL_REVIEW";
    public string PreAssessmentPositiveFactorsJson { get; set; } = "[]";
    public string PreAssessmentRiskFactorsJson { get; set; } = "[]";
    public string? ApplicationNote { get; set; }
    public decimal? ApprovedLimit { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public string? EvaluationNote { get; set; }
    public DateTime? EvaluatedAtUtc { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public ICollection<ApplicationHistory> Histories { get; set; } = new List<ApplicationHistory>();
    public ICollection<ApplicationRevisionSnapshot> RevisionSnapshots { get; set; } = new List<ApplicationRevisionSnapshot>();
    public CreditCard? CreditCard { get; set; }
}
