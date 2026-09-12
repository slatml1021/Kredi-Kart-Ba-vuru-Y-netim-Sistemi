using System.Net.Mail;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;

namespace CreditCardApplication.Application.Customers;

public sealed class CustomerService(
    ICustomerRepository customerRepository,
    IContactVerificationSender? verificationSender = null)
{
    public ExternalRiskProfileResponse GetExternalRiskProfile(string nationalIdentityNumber)
    {
        var identityNumber = nationalIdentityNumber.Trim();
        if (!IsValidTurkishIdentityNumber(identityNumber))
            throw new ArgumentException("Geçerli bir TC Kimlik Numarası girilmelidir.");

        var cards = CreateDeterministicMockOtherBankCards(identityNumber);
        return new ExternalRiskProfileResponse(
            cards.Sum(x => x.CardLimit),
            cards.Select(x => new ExternalRiskCardResponse(
                x.BankName,
                new string((x.MaskedCardNumber ?? string.Empty).Where(char.IsDigit).TakeLast(4).ToArray()),
                x.CardLimit)).ToList());
    }

    public async Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        if (await customerRepository.NationalIdentityNumberExistsAsync(request.NationalIdentityNumber, cancellationToken))
        {
            throw new InvalidOperationException("Bu TC Kimlik Numarası ile kayıtlı bir müşteri bulunmaktadır.");
        }
        var phone = NormalizePhone(request.PhoneCountryCode, request.PhoneNumber);
        var email = request.EmailAddress.Trim().ToLowerInvariant();
        await EnsureContactIsUniqueAsync(phone.CountryCode, phone.Number, email, null, cancellationToken);

        var otherBankCards = CreateDeterministicMockOtherBankCards(request.NationalIdentityNumber);
        var otherBankTotal = otherBankCards.Sum(x => x.CardLimit);
        var nextCustomerSequence = await customerRepository.GetNextCustomerSequenceAsync(cancellationToken);
        if (nextCustomerSequence > 999_999)
            throw new InvalidOperationException("Müşteri numarası kapasitesi dolmuştur.");
        var customer = new Customer
        {
            CustomerNumber = $"MUS{nextCustomerSequence:D6}",
            NationalIdentityNumber = request.NationalIdentityNumber,
            FirstName = NormalizeName(request.FirstName),
            LastName = NormalizeName(request.LastName),
            PhoneCountryCode = phone.CountryCode,
            PhoneNumber = phone.Number,
            IsPhoneVerified = false,
            EmailAddress = email,
            IsEmailVerified = false,
            BirthDate = request.BirthDate,
            Gender = NormalizeOptional(request.Gender, "Belirtilmedi"),
            EducationLevel = NormalizeOptional(request.EducationLevel, "Belirtilmedi"),
            Occupation = NormalizeOptional(request.Occupation, "Belirtilmedi"),
            EmploymentStatus = NormalizeOptional(request.EmploymentStatus, "Çalışıyor"),
            City = request.City.Trim(),
            District = request.District.Trim(),
            Neighborhood = request.Neighborhood.Trim(),
            Address = request.Address.Trim(),
            MonthlyNetIncome = request.MonthlyNetIncome,
            OtherBankTotalCardLimit = otherBankTotal,
            CreditScore = CalculatePrototypeCreditScore(
                request.MonthlyNetIncome, otherBankTotal, request.BirthDate),
            IsActive = true,
            OtherBankCards = otherBankCards,
            SavedAddresses =
            [
                new CustomerAddress
                {
                    Name = "Ev Adresi", City = request.City.Trim(), District = request.District.Trim(),
                    Neighborhood = request.Neighborhood.Trim(), Street = request.Street.Trim(),
                    Avenue = NormalizeNullable(request.Avenue), BuildingNo = request.BuildingNo.Trim(),
                    ApartmentNo = NormalizeNullable(request.ApartmentNo), Floor = NormalizeNullable(request.Floor),
                    PostalCode = request.PostalCode.Trim(), FullAddress = request.Address.Trim(), IsDefault = true
                }
            ]
        };

        await customerRepository.AddAsync(customer, cancellationToken);
        return Map(customer);
    }

    public async Task<CustomerResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(id, cancellationToken);
        return customer is null ? null : Map(customer);
    }

    public async Task<CustomerDetailResponse?> GetDetailByIdAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetDetailByIdAsync(id, cancellationToken);
        if (customer is null) return null;

        var applications = customer.Applications
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CustomerApplicationSummary(
                x.Id, x.ApplicationNumber, x.CardType.Name, x.RequestedLimit,
                x.Status.ToString(), x.CreatedAtUtc))
            .ToList();
        var cards = customer.Applications
            .Where(x => x.CreditCard is not null)
            .OrderByDescending(x => x.CreditCard!.IssueDate)
            .Select(x => new CustomerCardSummary(
                x.CreditCard!.Id, x.CreditCard.MaskedCardNumber, x.CardType.Name,
                x.CreditCard.CardLimit, x.CreditCard.Status.ToString()))
            .ToList();

        return new CustomerDetailResponse(Map(customer), cards, applications);
    }

    public async Task<CustomerResponse> UpdateAsync(int id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var customer = await customerRepository.GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var phone = NormalizePhone(request.PhoneCountryCode, request.PhoneNumber);
        var email = request.EmailAddress.Trim().ToLowerInvariant();
        await EnsureContactIsUniqueAsync(phone.CountryCode, phone.Number, email, id, cancellationToken);
        var phoneChanged = customer.PhoneCountryCode != phone.CountryCode || customer.PhoneNumber != phone.Number;
        var emailChanged = customer.EmailAddress != email;
        customer.FirstName = NormalizeName(request.FirstName);
        customer.LastName = NormalizeName(request.LastName);
        customer.PhoneCountryCode = phone.CountryCode;
        customer.PhoneNumber = phone.Number;
        customer.EmailAddress = email;
        if (phoneChanged)
        {
            customer.IsPhoneVerified = false;
            customer.PhoneVerificationCodeHash = null;
            customer.PhoneVerificationExpiresAtUtc = null;
            customer.PhoneVerificationFailedAttempts = 0;
        }
        if (emailChanged)
        {
            customer.IsEmailVerified = false;
            customer.EmailVerificationCodeHash = null;
            customer.EmailVerificationExpiresAtUtc = null;
            customer.EmailVerificationFailedAttempts = 0;
        }
        customer.BirthDate = request.BirthDate;
        customer.Gender = NormalizeOptional(request.Gender, "Belirtilmedi");
        customer.EducationLevel = NormalizeOptional(request.EducationLevel, "Belirtilmedi");
        customer.Occupation = NormalizeOptional(request.Occupation, "Belirtilmedi");
        customer.EmploymentStatus = NormalizeOptional(request.EmploymentStatus, "Çalışıyor");
        customer.City = request.City.Trim();
        customer.District = request.District.Trim();
        customer.Neighborhood = request.Neighborhood.Trim();
        customer.Address = request.Address.Trim();
        customer.MonthlyNetIncome = request.MonthlyNetIncome;
        var otherBankCards = CreateDeterministicMockOtherBankCards(customer.NationalIdentityNumber);
        customer.OtherBankCards.Clear();
        foreach (var card in otherBankCards) customer.OtherBankCards.Add(card);
        customer.OtherBankTotalCardLimit = otherBankCards.Sum(x => x.CardLimit);
        customer.CreditScore = CalculatePrototypeCreditScore(
            request.MonthlyNetIncome, customer.OtherBankTotalCardLimit, request.BirthDate);
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await customerRepository.SaveChangesAsync(cancellationToken);
        return Map(customer);
    }

    public async Task<CustomerContactVerificationResponse> RequestContactVerificationAsync(
        int id,
        RequestCustomerContactVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var channel = NormalizeVerificationChannel(request.Channel);
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        var hash = HashVerificationCode(code);

        if (channel == "Phone")
        {
            customer.PhoneVerificationCodeHash = hash;
            customer.PhoneVerificationExpiresAtUtc = expiresAt;
            customer.PhoneVerificationFailedAttempts = 0;
        }
        else
        {
            customer.EmailVerificationCodeHash = hash;
            customer.EmailVerificationExpiresAtUtc = expiresAt;
            customer.EmailVerificationFailedAttempts = 0;
        }

        customer.UpdatedAtUtc = DateTime.UtcNow;
        await customerRepository.SaveChangesAsync(cancellationToken);
        if (verificationSender is not null)
        {
            var rawDestination = channel == "Phone"
                ? $"{customer.PhoneCountryCode}{customer.PhoneNumber}"
                : customer.EmailAddress;
            await verificationSender.SendAsync(channel, rawDestination, code, cancellationToken);
        }
        var destination = channel == "Phone"
            ? MaskPhone(customer.PhoneCountryCode, customer.PhoneNumber)
            : MaskEmail(customer.EmailAddress);
        return new CustomerContactVerificationResponse(
            channel, destination, expiresAt, verificationSender is null || verificationSender.ExposeDemoCode ? code : string.Empty);
    }

    public async Task<CustomerResponse> VerifyContactAsync(
        int id,
        VerifyCustomerContactRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var channel = NormalizeVerificationChannel(request.Channel);
        var hash = channel == "Phone"
            ? customer.PhoneVerificationCodeHash
            : customer.EmailVerificationCodeHash;
        var expiresAt = channel == "Phone"
            ? customer.PhoneVerificationExpiresAtUtc
            : customer.EmailVerificationExpiresAtUtc;
        var failedAttempts = channel == "Phone"
            ? customer.PhoneVerificationFailedAttempts
            : customer.EmailVerificationFailedAttempts;
        if (string.IsNullOrWhiteSpace(hash) || !expiresAt.HasValue || expiresAt.Value < DateTime.UtcNow)
            throw new ArgumentException("Önce yeni bir doğrulama kodu isteyin; kodun süresi dolmuş olabilir.");
        if (failedAttempts >= 5)
            throw new InvalidOperationException("Çok fazla hatalı deneme yapıldı. Yeni doğrulama kodu isteyin.");
        if (!VerifyCode(request.Code, hash))
        {
            if (channel == "Phone") customer.PhoneVerificationFailedAttempts++;
            else customer.EmailVerificationFailedAttempts++;
            await customerRepository.SaveChangesAsync(cancellationToken);
            throw new ArgumentException("Doğrulama kodu geçersiz.");
        }

        if (channel == "Phone")
        {
            customer.IsPhoneVerified = true;
            customer.PhoneVerificationCodeHash = null;
            customer.PhoneVerificationExpiresAtUtc = null;
            customer.PhoneVerificationFailedAttempts = 0;
        }
        else
        {
            customer.IsEmailVerified = true;
            customer.EmailVerificationCodeHash = null;
            customer.EmailVerificationExpiresAtUtc = null;
            customer.EmailVerificationFailedAttempts = 0;
        }

        customer.UpdatedAtUtc = DateTime.UtcNow;
        await customerRepository.SaveChangesAsync(cancellationToken);
        return Map(customer);
    }

    public async Task<CustomerResponse> SetStatusAsync(
        int id,
        SetCustomerStatusRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        if (!request.IsActive && await customerRepository.HasOpenApplicationsAsync(id, cancellationToken))
            throw new InvalidOperationException("Bekleyen veya revizyondaki başvurusu olan müşteri pasife alınamaz.");
        customer.IsActive = request.IsActive;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await customerRepository.SaveChangesAsync(cancellationToken);
        return Map(customer);
    }

    public async Task<IReadOnlyList<CustomerAddressResponse>> GetAddressesAsync(
        int customerId,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        return customer.SavedAddresses.Where(x => x.IsActive)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name)
            .Select(MapAddress).ToList();
    }

    public async Task<CustomerAddressResponse> AddAddressAsync(
        int customerId,
        CustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAddress(request);
        var customer = await customerRepository.GetTrackedByIdAsync(customerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        EnsureUniqueAddressName(customer, request.Name, null);
        var makeDefault = request.IsDefault || !customer.SavedAddresses.Any(x => x.IsActive);
        if (makeDefault) ClearDefaultAddresses(customer);
        var address = CreateAddress(customerId, request, makeDefault);
        customer.SavedAddresses.Add(address);
        if (makeDefault) SynchronizePrimaryAddress(customer, address);
        await customerRepository.SaveChangesAsync(cancellationToken);
        return MapAddress(address);
    }

    public async Task<CustomerAddressResponse> UpdateAddressAsync(
        int customerId,
        int addressId,
        CustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAddress(request);
        var customer = await customerRepository.GetTrackedByIdAsync(customerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var address = customer.SavedAddresses.FirstOrDefault(x => x.Id == addressId && x.IsActive)
            ?? throw new ArgumentException("Adres bulunamadı.");
        EnsureUniqueAddressName(customer, request.Name, addressId);
        if (request.IsDefault) ClearDefaultAddresses(customer);
        ApplyAddress(address, request);
        address.IsDefault = request.IsDefault || address.IsDefault;
        address.UpdatedAtUtc = DateTime.UtcNow;
        if (address.IsDefault) SynchronizePrimaryAddress(customer, address);
        await customerRepository.SaveChangesAsync(cancellationToken);
        return MapAddress(address);
    }

    public async Task<CustomerAddressResponse> SetDefaultAddressAsync(
        int customerId,
        int addressId,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetTrackedByIdAsync(customerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var address = customer.SavedAddresses.FirstOrDefault(x => x.Id == addressId && x.IsActive)
            ?? throw new ArgumentException("Adres bulunamadı.");
        ClearDefaultAddresses(customer);
        address.IsDefault = true;
        address.UpdatedAtUtc = DateTime.UtcNow;
        SynchronizePrimaryAddress(customer, address);
        await customerRepository.SaveChangesAsync(cancellationToken);
        return MapAddress(address);
    }

    public async Task DeleteAddressAsync(int customerId, int addressId, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetTrackedByIdAsync(customerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var address = customer.SavedAddresses.FirstOrDefault(x => x.Id == addressId && x.IsActive)
            ?? throw new ArgumentException("Adres bulunamadı.");
        if (customer.SavedAddresses.Count(x => x.IsActive) == 1)
            throw new InvalidOperationException("Müşterinin tek aktif adresi silinemez.");
        address.IsActive = false;
        address.IsDefault = false;
        address.UpdatedAtUtc = DateTime.UtcNow;
        if (!customer.SavedAddresses.Any(x => x.IsActive && x.IsDefault))
        {
            var replacement = customer.SavedAddresses.First(x => x.IsActive);
            replacement.IsDefault = true;
            SynchronizePrimaryAddress(customer, replacement);
        }
        await customerRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerResponse>> SearchAsync(
        string query,
        string? criterion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("En az bir arama kriteri girilmelidir.", nameof(query));
        }

        var normalizedCriterion = criterion?.Trim();
        if (normalizedCriterion?.Equals("NationalIdentityNumber", StringComparison.OrdinalIgnoreCase) == true
            && !IsValidTurkishIdentityNumber(query.Trim()))
            throw new ArgumentException("Geçerli bir TC Kimlik Numarası girilmelidir.");
        if (normalizedCriterion?.Equals("CustomerNumber", StringComparison.OrdinalIgnoreCase) == true
            && !System.Text.RegularExpressions.Regex.IsMatch(query.Trim().ToUpperInvariant(), "^MUS[0-9]{6}$"))
            throw new ArgumentException("Müşteri numarası MUS ile başlamalı ve ardından 6 rakam gelmelidir.");
        if (normalizedCriterion?.Equals("PhoneNumber", StringComparison.OrdinalIgnoreCase) == true
            && !query.Where(char.IsDigit).Any())
            throw new ArgumentException("Geçerli bir telefon numarası girilmelidir.");

        var searchValue = query.Trim();
        if (normalizedCriterion?.Equals("PhoneNumber", StringComparison.OrdinalIgnoreCase) == true)
            searchValue = NormalizePhoneSearch(searchValue);
        var customers = await customerRepository.SearchAsync(searchValue, normalizedCriterion, cancellationToken);
        return customers.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var customers = await customerRepository.GetAllAsync(cancellationToken);
        return customers.Select(Map).ToList();
    }

    private static void Validate(CreateCustomerRequest request)
    {
        if (!IsValidTurkishIdentityNumber(request.NationalIdentityNumber))
            throw new ArgumentException("Geçerli bir TC Kimlik Numarası girilmelidir.");

        ValidateCommon(request.FirstName, request.LastName, request.PhoneCountryCode, request.PhoneNumber, request.EmailAddress,
            request.MonthlyNetIncome, request.OtherBankTotalCardLimit, request.BirthDate, request.Address);
        ValidateProfileSelections(request.Gender, request.EducationLevel, request.Occupation, request.EmploymentStatus,
            request.City, request.District, request.Neighborhood);
        ValidateAddressParts("Ev Adresi", request.Street, request.Avenue, request.BuildingNo,
            request.ApartmentNo, request.Floor, request.PostalCode);
    }

    private static void Validate(UpdateCustomerRequest request)
    {
        ValidateCommon(request.FirstName, request.LastName, request.PhoneCountryCode, request.PhoneNumber, request.EmailAddress,
            request.MonthlyNetIncome, request.OtherBankTotalCardLimit, request.BirthDate, request.Address);
        ValidateProfileSelections(request.Gender, request.EducationLevel, request.Occupation, request.EmploymentStatus,
            request.City, request.District, request.Neighborhood);
    }

    private static void ValidateProfileSelections(string gender, string educationLevel, string occupation, string employmentStatus,
        string city, string district, string neighborhood)
    {
        if (new[] { gender, educationLevel, occupation, employmentStatus, city, district, neighborhood }
            .Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Cinsiyet, eğitim, meslek, çalışma durumu, il, ilçe ve mahalle seçimleri zorunludur.");
        var allowedEmploymentStatuses = new[]
            { "Çalışıyor", "İşsiz", "Öğrenci", "Emekli - Çalışıyor", "Emekli - Çalışmıyor" };
        if (!allowedEmploymentStatuses.Contains(employmentStatus, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Geçerli bir çalışma durumu seçilmelidir.");
        var educationRank = educationLevel switch
        {
            "İlköğretim" => 1, "Lise" => 2, "Ön Lisans" => 3,
            "Lisans" => 4, "Yüksek Lisans" => 5, "Doktora" => 6, _ => 0
        };
        if (new[] { "Mühendis", "Öğretmen", "Doktor", "Avukat", "Eczacı" }
            .Contains(occupation, StringComparer.OrdinalIgnoreCase) && educationRank < 4)
            throw new ArgumentException($"{occupation} mesleği için eğitim düzeyi en az Lisans olmalıdır.");
    }

    private static void ValidateCommon(
        string firstName,
        string lastName,
        string phoneCountryCode,
        string phoneNumber,
        string emailAddress,
        decimal monthlyNetIncome,
        decimal otherBankTotalCardLimit,
        DateOnly? birthDate,
        string address)
    {
        if (!IsValidName(firstName) || !IsValidName(lastName))
            throw new ArgumentException("Ad ve soyad yalnızca harf ve tekli boşluklardan oluşmalıdır.");

        _ = NormalizePhone(phoneCountryCode, phoneNumber);

        try { _ = new MailAddress(emailAddress); }
        catch { throw new ArgumentException("Geçerli bir e-posta adresi girilmelidir."); }

        if (monthlyNetIncome <= 0)
            throw new ArgumentException("Aylık net gelir sıfırdan büyük olmalıdır.");

        if (otherBankTotalCardLimit < 0)
            throw new ArgumentException("Diğer banka toplam kart limiti negatif olamaz.");

        if (!birthDate.HasValue)
            throw new ArgumentException("Doğum tarihi zorunludur.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (birthDate.Value > today.AddYears(-18))
            throw new ArgumentException("Müşteri kredi kartı işlemleri için en az 18 yaşında olmalıdır.");
        if (birthDate.Value < today.AddYears(-120))
            throw new ArgumentException("Doğum tarihi en fazla 120 yıl önce olabilir.");
        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length < 10)
            throw new ArgumentException("Açık adres en az 10 karakter olmalıdır.");
    }

    private async Task EnsureContactIsUniqueAsync(
        string countryCode,
        string phoneNumber,
        string emailAddress,
        int? excludedCustomerId,
        CancellationToken cancellationToken)
    {
        if (await customerRepository.PhoneNumberExistsAsync(
                countryCode, phoneNumber, excludedCustomerId, cancellationToken))
            throw new InvalidOperationException("Bu telefon numarası başka bir müşteride kayıtlıdır.");
        if (await customerRepository.EmailAddressExistsAsync(
                emailAddress, excludedCustomerId, cancellationToken))
            throw new InvalidOperationException("Bu e-posta adresi başka bir müşteride kayıtlıdır.");
    }

    private static (string CountryCode, string Number) NormalizePhone(string countryCode, string phoneNumber)
    {
        var code = countryCode.Trim().Replace(" ", string.Empty);
        var number = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (code == "+90" && number.Length == 11 && number.StartsWith('0')) number = number[1..];
        var isValid = code switch
        {
            "+90" => number.Length == 10 && number.StartsWith('5'),
            "+44" => number.Length == 10 && number.StartsWith('7'),
            "+1" => number.Length == 10 && number[0] is >= '2' and <= '9',
            "+49" => number.Length is 10 or 11,
            _ => false
        };
        if (!isValid)
            throw new ArgumentException(code switch
            {
                "+90" => "Türkiye telefonu +90 sonrası 5 ile başlayan 10 hane olmalıdır.",
                "+44" => "İngiltere telefonu +44 sonrası 7 ile başlayan 10 hane olmalıdır.",
                "+1" => "ABD telefonu +1 sonrası 10 hane olmalıdır.",
                "+49" => "Almanya telefonu +49 sonrası 10 veya 11 hane olmalıdır.",
                _ => "Desteklenmeyen telefon ülke kodu."
            });
        return (code, number);
    }

    private static int CalculatePrototypeCreditScore(
        decimal monthlyIncome,
        decimal otherBankLimit,
        DateOnly? birthDate)
    {
        var age = birthDate.HasValue
            ? DateTime.UtcNow.Year - birthDate.Value.Year
            : 18;
        var utilizationPenalty = monthlyIncome <= 0
            ? 500
            : (int)Math.Min(600, otherBankLimit / monthlyIncome * 160);
        var incomeScore = (int)Math.Min(650, monthlyIncome / 100);
        var ageScore = Math.Clamp((age - 18) * 8, 0, 220);
        return Math.Clamp(700 + incomeScore + ageScore - utilizationPenalty, 0, 1900);
    }

    private static string NormalizeName(string value)
    {
        var collapsed = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        return culture.TextInfo.ToTitleCase(collapsed.ToLower(culture));
    }

    private static bool IsValidName(string value)
    {
        var normalized = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length is >= 2 and <= 60
            && normalized.All(character => char.IsLetter(character) || character == ' ');
    }

    public static bool IsValidTurkishIdentityNumber(string value)
    {
        if (value.Length != 11 || !value.All(char.IsDigit) || value[0] == '0') return false;
        var digits = value.Select(character => character - '0').ToArray();
        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        return ((oddSum * 7 - evenSum) % 10 + 10) % 10 == digits[9]
            && digits.Take(10).Sum() % 10 == digits[10];
    }

    private static List<OtherBankCard> CreateDeterministicMockOtherBankCards(string identityNumber)
    {
        var seed = identityNumber.Aggregate(17, (current, character) => current * 31 + character);
        var banks = new[] { "Akbank", "Garanti BBVA", "Türkiye İş Bankası", "Yapı Kredi", "QNB" };
        var cardCount = Math.Abs(seed % 3);
        var result = new List<OtherBankCard>();
        for (var index = 0; index < cardCount; index++)
        {
            var bankIndex = Math.Abs((seed + index * 7) % banks.Length);
            var limitStep = Math.Abs((seed / (index + 1)) % 8) + 1;
            var suffix = Math.Abs((seed + index * 7919) % 10_000).ToString("D4");
            result.Add(new OtherBankCard
            {
                BankName = banks[bankIndex],
                MaskedCardNumber = $"**** {suffix}",
                CardLimit = limitStep * 5_000m,
                IsActive = true
            });
        }
        return result;
    }
    private static string NormalizeOptional(string value, string defaultValue) =>
        string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

    private static CustomerResponse Map(Customer customer)
    {
        var missingFields = GetMissingProfileFields(customer);
        var ownBankLimit = customer.Applications
            .Where(x => x.CreditCard is not null && x.Status == ApplicationStatus.Approved)
            .Sum(x => x.CreditCard!.CardLimit);
        var availableLimit = Math.Max(0, customer.MonthlyNetIncome * 3
            - customer.OtherBankTotalCardLimit - ownBankLimit);
        return new(
        customer.Id,
        customer.CustomerNumber,
        customer.NationalIdentityNumber,
        customer.FirstName,
        customer.LastName,
        customer.PhoneCountryCode,
        customer.PhoneNumber,
        customer.IsPhoneVerified,
        customer.EmailAddress,
        customer.IsEmailVerified,
        customer.BirthDate,
        customer.Gender,
        customer.EducationLevel,
        customer.Occupation,
        customer.EmploymentStatus,
        customer.City,
        customer.District,
        customer.Neighborhood,
        customer.Address,
        customer.MonthlyNetIncome,
        customer.OtherBankTotalCardLimit,
        customer.CreditScore,
        customer.IsActive,
        missingFields.Count == 0,
        missingFields,
        ownBankLimit,
        availableLimit,
        customer.OtherBankCards
            .OrderBy(x => x.BankName)
            .Select(x => new OtherBankCardResponse(
                x.Id, x.BankName, x.MaskedCardNumber, x.CardLimit, x.IsActive))
            .ToList());
    }

    private static void ValidateAddress(CustomerAddressRequest request)
    {
        if (new[] { request.City, request.District, request.Neighborhood, request.Street, request.BuildingNo }
            .Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("İl, ilçe, mahalle, sokak/cadde ve bina numarası zorunludur.");
        ValidateAddressParts(request.Name, request.Street, request.Avenue, request.BuildingNo,
            request.ApartmentNo, request.Floor, request.PostalCode);
    }

    private static void ValidateAddressParts(
        string name, string street, string? avenue, string buildingNo,
        string? apartmentNo, string? floor, string postalCode)
    {
        if (!Regex.IsMatch(name.Trim(), @"^[\p{L}\p{N}][\p{L}\p{N} .&'’-]{1,39}$"))
            throw new ArgumentException("Adres adı 2 ile 40 karakter arasında olmalı ve geçerli karakterlerden oluşmalıdır.");
        if (!Regex.IsMatch(street.Trim(), @"^[\p{L}\p{N}][\p{L}\p{N} .,'’()/-]{1,119}$"))
            throw new ArgumentException("Sokak/cadde bilgisi 2 ile 120 karakter arasında olmalıdır.");
        if (!string.IsNullOrWhiteSpace(avenue)
            && !Regex.IsMatch(avenue.Trim(), @"^[\p{L}\p{N}][\p{L}\p{N} .,'’()/-]{1,119}$"))
            throw new ArgumentException("Site/mevki bilgisi geçerli karakterlerden oluşmalıdır.");
        if (!Regex.IsMatch(buildingNo.Trim(), @"^[\p{L}\p{N}][\p{L}\p{N}/-]{0,19}$"))
            throw new ArgumentException("Bina numarası yalnızca harf, rakam, '-' ve '/' içerebilir.");
        if (!string.IsNullOrWhiteSpace(apartmentNo)
            && !Regex.IsMatch(apartmentNo.Trim(), @"^[\p{L}\p{N}][\p{L}\p{N}/-]{0,19}$"))
            throw new ArgumentException("Daire numarası yalnızca harf, rakam, '-' ve '/' içerebilir.");
        if (!string.IsNullOrWhiteSpace(floor) && !Regex.IsMatch(floor.Trim(), @"^-?\d{1,3}$"))
            throw new ArgumentException("Kat bilgisi -999 ile 999 arasında tam sayı olmalıdır.");
        if (!Regex.IsMatch(postalCode.Trim(), @"^\d{5}$"))
            throw new ArgumentException("Posta kodu ulusal katalogdan gelen 5 rakamlı bir değer olmalıdır.");
    }

    private static void EnsureUniqueAddressName(Customer customer, string name, int? excludedId)
    {
        if (customer.SavedAddresses.Any(x => x.IsActive && x.Id != excludedId
            && x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Bu adres adı müşteride zaten kayıtlıdır.");
    }

    private static CustomerAddress CreateAddress(int customerId, CustomerAddressRequest request, bool isDefault)
    {
        var address = new CustomerAddress { CustomerId = customerId, IsDefault = isDefault };
        ApplyAddress(address, request);
        return address;
    }

    private static void ApplyAddress(CustomerAddress address, CustomerAddressRequest request)
    {
        address.Name = request.Name.Trim();
        address.City = request.City.Trim();
        address.District = request.District.Trim();
        address.Neighborhood = request.Neighborhood.Trim();
        address.Street = request.Street.Trim();
        address.Avenue = NormalizeNullable(request.Avenue);
        address.BuildingNo = request.BuildingNo.Trim();
        address.ApartmentNo = NormalizeNullable(request.ApartmentNo);
        address.Floor = NormalizeNullable(request.Floor);
        address.PostalCode = request.PostalCode.Trim();
        address.FullAddress = ComposeAddress(address);
        address.IsActive = true;
    }

    private static void ClearDefaultAddresses(Customer customer)
    {
        foreach (var item in customer.SavedAddresses.Where(x => x.IsActive)) item.IsDefault = false;
    }

    private static void SynchronizePrimaryAddress(Customer customer, CustomerAddress address)
    {
        customer.City = address.City;
        customer.District = address.District;
        customer.Neighborhood = address.Neighborhood;
        customer.Address = address.FullAddress;
        customer.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string ComposeAddress(CustomerAddress address) => string.Join(", ", new[]
    {
        address.Neighborhood,
        address.Street,
        address.Avenue,
        $"Bina No: {address.BuildingNo}",
        string.IsNullOrWhiteSpace(address.ApartmentNo) ? null : $"Daire: {address.ApartmentNo}",
        string.IsNullOrWhiteSpace(address.Floor) ? null : $"Kat: {address.Floor}",
        address.PostalCode,
        $"{address.District} / {address.City}"
    }.Where(x => !string.IsNullOrWhiteSpace(x))!);

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CustomerAddressResponse MapAddress(CustomerAddress address) => new(
        address.Id, address.CustomerId, address.Name, address.City, address.District,
        address.Neighborhood, address.Street, address.Avenue, address.BuildingNo,
        address.ApartmentNo, address.Floor, address.PostalCode, address.FullAddress,
        address.IsDefault, address.IsActive);

    private static string NormalizeVerificationChannel(string channel) =>
        channel.Equals("Phone", StringComparison.OrdinalIgnoreCase) ? "Phone"
        : channel.Equals("Email", StringComparison.OrdinalIgnoreCase) ? "Email"
        : throw new ArgumentException("Doğrulama kanalı Phone veya Email olmalıdır.");

    private static string HashVerificationCode(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = SHA256.HashData(salt.Concat(Encoding.UTF8.GetBytes(code)).ToArray());
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyCode(string code, string encoded)
    {
        var parts = encoded.Split(':', 2);
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = SHA256.HashData(salt.Concat(Encoding.UTF8.GetBytes(code.Trim())).ToArray());
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string MaskPhone(string countryCode, string number) =>
        $"{countryCode} *** *** {number[^2..]}";

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        return parts.Length == 2 ? $"{parts[0][0]}***@{parts[1]}" : "***";
    }

    private static string NormalizePhoneSearch(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith('0') && digits.Length == 11) return digits[1..];
        return digits;
    }

    public static IReadOnlyList<string> GetMissingProfileFields(Customer customer)
    {
        var missing = new List<string>();
        if (!customer.BirthDate.HasValue) missing.Add("Doğum tarihi");
        if (string.IsNullOrWhiteSpace(customer.Gender) || customer.Gender == "Belirtilmedi") missing.Add("Cinsiyet");
        if (string.IsNullOrWhiteSpace(customer.EducationLevel) || customer.EducationLevel == "Belirtilmedi") missing.Add("Eğitim düzeyi");
        if (string.IsNullOrWhiteSpace(customer.Occupation) || customer.Occupation == "Belirtilmedi") missing.Add("Meslek");
        if (string.IsNullOrWhiteSpace(customer.EmploymentStatus) || customer.EmploymentStatus == "Belirtilmedi") missing.Add("Çalışma durumu");
        if (string.IsNullOrWhiteSpace(customer.City)) missing.Add("İl");
        if (string.IsNullOrWhiteSpace(customer.District)) missing.Add("İlçe");
        if (string.IsNullOrWhiteSpace(customer.Neighborhood)) missing.Add("Mahalle");
        if (string.IsNullOrWhiteSpace(customer.Address)) missing.Add("Açık adres");
        if (!customer.IsPhoneVerified) missing.Add("Telefon doğrulaması");
        if (!customer.IsEmailVerified) missing.Add("E-posta doğrulaması");
        return missing;
    }
}
