namespace CreditCardApplication.Application.Platform;

public sealed record KkbRiskAnalysisResponse(
    int CustomerId, int Score, string Category, string RiskLevel, string DataSource,
    string Explanation, IReadOnlyList<string> Factors, DateTime CalculatedAtUtc);

public sealed record PreAssessmentResponse(
    int Score, string RiskLevel, string Recommendation,
    IReadOnlyList<string> PositiveFactors, IReadOnlyList<string> RiskFactors,
    string Disclaimer);

public sealed record DuplicateApplicationResponse(
    bool HasDuplicate, string Message, int? ApplicationId, string? ApplicationNumber,
    string? Status, DateTime? ApplicationDate);

public sealed record CreateSupplementaryApplicationRequest(
    int PrimaryCreditCardId, int SupplementaryHolderCustomerId, string Relationship, decimal RequestedLimit,
    string DeliveryMethod = "RegisteredAddress", string? DeliveryAddress = null);
public sealed record EvaluateSupplementaryApplicationRequest(string Decision, string? Note);
public sealed record SupplementaryApplicationResponse(
    int Id, string ApplicationNumber, int PrimaryCreditCardId, int PrimaryCustomerId, string PrimaryCustomer,
    int HolderCustomerId, string HolderCustomer, string Relationship, decimal RequestedLimit,
    string DeliveryMethod, string DeliveryAddress, string Status, DateTime CreatedAtUtc,
    string? EvaluationNote, DateTime? EvaluatedAtUtc, string? MaskedCardNumber, DateTime? IssuedAtUtc,
    string CardStatus, string? FulfillmentStatus, DateTime? EstimatedPrintAtUtc,
    DateTime? EstimatedDeliveryAtUtc, DateTime? DeliveredAtUtc, string? TrackingNumber);
public sealed record SupplementaryCardContextResponse(
    int PrimaryCreditCardId, string MaskedCardNumber, string PrimaryCardStatus, decimal PrimaryCardLimit,
    decimal UsedSupplementaryLimit, decimal ReservedSupplementaryLimit, decimal AvailableSupplementaryLimit,
    int PrimaryCustomerId, string PrimaryCustomerNumber, string PrimaryCustomerFullName,
    string PrimaryCustomerNationalIdentityNumber, bool PrimaryCustomerIsActive,
    DateOnly? PrimaryCustomerBirthDate, string RegisteredDeliveryAddress);

public sealed record FulfillmentStepResponse(string Key, string Label, string Status, DateTime? TimestampUtc);
public sealed record FulfillmentResponse(
    int CreditCardId, string Status, DateTime EstimatedPrintAtUtc, DateTime EstimatedDeliveryAtUtc,
    string? TrackingNumber, IReadOnlyList<FulfillmentStepResponse> Steps);

public sealed record SlaItemResponse(
    int ApplicationId, string ApplicationNumber, string Customer, string Status,
    double WaitingHours, string SlaStatus, DateTime CreatedAtUtc);
public sealed record SlaDashboardResponse(
    double AverageEvaluationMinutes, int OverdueToday, double LongestWaitingHours,
    decimal RevisionReturnRate, IReadOnlyList<SlaItemResponse> LongestWaiting);

public sealed record NotificationResponse(
    int Id, string Type, string Title, string Message, string? Link, bool IsRead, DateTime CreatedAtUtc);

public sealed record ChatUserResponse(int Id, string FullName, string Role);
public sealed record SendChatMessageRequest(int RecipientUserId, string Message, int? ApplicationId);
public sealed record SendBulkChatMessageRequest(
    IReadOnlyList<int> RecipientUserIds, IReadOnlyList<int> CcRecipientUserIds,
    string Message, int? ApplicationId);
public sealed record ReportChatMessageRequest(string Reason, string? Note);
public sealed record ChatMessageResponse(
    int Id, int SenderUserId, string SenderName, int RecipientUserId, string RecipientName,
    string Message, int? ApplicationId, bool IsRead, DateTime CreatedAtUtc);

public sealed record ConsentRequest(string ConsentType, string TextVersion, bool IsGranted, string Channel);
public sealed record ConsentResponse(
    int Id, string ConsentType, string TextVersion, bool IsGranted, string Channel,
    string CapturedBy, DateTime CapturedAtUtc, DateTime? WithdrawnAtUtc);

public sealed record RevisionFieldDifference(string Field, string PreviousValue, string NewValue, bool Changed);
public sealed record RevisionComparisonResponse(int RevisionNumber, IReadOnlyList<RevisionFieldDifference> Fields);

public sealed record SimulationRequest(
    int CustomerId, int CardTypeId, decimal RequestedLimit, string DeliveryMethod);
public sealed record SimulationResponse(
    bool CanCreateApplication, IReadOnlyList<string> MissingFields,
    string RecommendedCard, decimal SuggestedMinimumLimit, decimal SuggestedMaximumLimit,
    PreAssessmentResponse PreAssessment, IReadOnlyList<string> RiskReasons,
    IReadOnlyList<string> SystemRecommendations, string Disclaimer);

public sealed record LoginHistoryItem(DateTime DateUtc, bool IsSuccessful, string Device, string? IpAddress);
public sealed record ProfileActivityItem(DateTime DateUtc, string Description);
public sealed record UserProfileResponse(
    int Id, string FullName, string RegistrationNumber, string Role, string CorporateEmail,
    string PhoneNumber, string Title, string Department, string Branch, bool IsActive,
    DateTime RegisteredAtUtc, DateTime? LastSuccessfulLoginUtc, DateTime? LastFailedLoginUtc,
    DateTime? PasswordChangedAtUtc, bool TwoFactorEnabled, bool IsLocked,
    IReadOnlyList<LoginHistoryItem> LoginHistory, IReadOnlyList<ProfileActivityItem> RecentActivities,
    IReadOnlyList<string> GrantedPermissions, IReadOnlyList<string> DeniedPermissions,
    int? ActiveOfficerCount, int? ApplicationsToday, int? RevisionWaiting, int? OverdueApplications,
    bool NotifyApplicationEvents, bool NotifySlaWarnings, bool NotifySecurityEvents);
public sealed record UpdateProfilePreferencesRequest(
    string PhoneNumber, string? ProfilePhotoUrl, bool NotifyApplicationEvents,
    bool NotifySlaWarnings, bool NotifySecurityEvents);

public sealed record GeneratedPdfResponse(byte[] Content, string FileName);
public sealed record ApplicationDocumentResponse(
    int Id, int ApplicationId, string DocumentType, string FileName, string Sha256, DateTime UploadedAtUtc,
    string VerificationStatus, string? VerifiedBy, DateTime? VerifiedAtUtc,
    string? VerificationNote, DateTime? ExpiresAtUtc);
public sealed record StoredDocumentResponse(byte[] Content, string FileName, string ContentType);

public interface IPlatformV2Service
{
    Task<KkbRiskAnalysisResponse> GetKkbAnalysisAsync(int customerId, CancellationToken cancellationToken);
    Task<PreAssessmentResponse> GetPreAssessmentAsync(int applicationId, CancellationToken cancellationToken);
    Task<DuplicateApplicationResponse> CheckDuplicateAsync(int customerId, int cardTypeId, CancellationToken cancellationToken);
    Task<SupplementaryApplicationResponse> CreateSupplementaryAsync(CreateSupplementaryApplicationRequest request, int userId, CancellationToken cancellationToken);
    Task<SupplementaryCardContextResponse> GetSupplementaryCardContextAsync(int primaryCardId, int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupplementaryApplicationResponse>> GetSupplementaryAsync(bool all, int userId, CancellationToken cancellationToken);
    Task<SupplementaryApplicationResponse> GetSupplementaryByIdAsync(int id, bool isManager, int userId, CancellationToken cancellationToken);
    Task<SupplementaryApplicationResponse> EvaluateSupplementaryAsync(int id, EvaluateSupplementaryApplicationRequest request, int userId, CancellationToken cancellationToken);
    Task<FulfillmentResponse?> GetFulfillmentAsync(int creditCardId, CancellationToken cancellationToken);
    Task<SlaDashboardResponse> GetSlaAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(int userId, CancellationToken cancellationToken);
    Task MarkNotificationReadAsync(int notificationId, int userId, CancellationToken cancellationToken);
    Task MarkAllNotificationsReadAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatUserResponse>> GetChatUsersAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatMessageResponse>> GetConversationAsync(int userId, int otherUserId, CancellationToken cancellationToken);
    Task<ChatMessageResponse> SendMessageAsync(int userId, SendChatMessageRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatMessageResponse>> SendBulkMessageAsync(int userId, SendBulkChatMessageRequest request, CancellationToken cancellationToken);
    Task ReportChatMessageAsync(int userId, int messageId, ReportChatMessageRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConsentResponse>> GetConsentsAsync(int customerId, CancellationToken cancellationToken);
    Task<ConsentResponse> SetConsentAsync(int customerId, ConsentRequest request, int userId, CancellationToken cancellationToken);
    Task<RevisionComparisonResponse?> GetRevisionComparisonAsync(int applicationId, CancellationToken cancellationToken);
    Task<SimulationResponse> SimulateAsync(SimulationRequest request, CancellationToken cancellationToken);
    Task<UserProfileResponse> GetProfileAsync(int userId, CancellationToken cancellationToken);
    Task<UserProfileResponse> UpdateProfileAsync(int userId, UpdateProfilePreferencesRequest request, CancellationToken cancellationToken);
    Task<GeneratedPdfResponse> GenerateApplicationPdfAsync(int applicationId, bool sendEmail, CancellationToken cancellationToken);
    Task<GeneratedPdfResponse> GenerateCardPdfAsync(int cardId, bool sendEmail, CancellationToken cancellationToken);
    Task<ApplicationDocumentResponse> UploadApplicationDocumentAsync(
        int applicationId, string documentType, string fileName, byte[] content, int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationDocumentResponse>> GetApplicationDocumentsAsync(
        int applicationId, int userId, CancellationToken cancellationToken);
    Task<StoredDocumentResponse> GetApplicationDocumentAsync(
        int applicationId, int documentId, int userId, CancellationToken cancellationToken);
    Task NotifyApplicationEventAsync(int applicationId, string eventName, CancellationToken cancellationToken);
    Task NotifyLimitDecreaseAsync(int cardId, int officerUserId, decimal newLimit, CancellationToken cancellationToken);
}
