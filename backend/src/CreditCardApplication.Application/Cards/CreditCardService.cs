namespace CreditCardApplication.Application.Cards;

public sealed class CreditCardService(ICreditCardRepository creditCardRepository)
{
    public async Task<CreditCardResponse?> GetByIdAsync(int id, int? officerUserId, CancellationToken cancellationToken)
    {
        var card = await creditCardRepository.GetByIdAsync(id, cancellationToken);
        if (card is null) return null;
        if (officerUserId.HasValue && card.CardApplication.CreatedByUserId != officerUserId.Value)
            throw new UnauthorizedAccessException("Bu kartı görüntüleme yetkiniz yok.");

        return new CreditCardResponse(card.Id, card.MaskedCardNumber, card.CardLimit, card.Status.ToString(),
            card.IssueDate, card.ExpiryDate, card.CardApplicationId, card.CardApplication.ApplicationNumber,
            $"{card.CardApplication.Customer.FirstName} {card.CardApplication.Customer.LastName}", card.CardApplication.CardType.Name);
    }
}
