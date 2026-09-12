-- DB Browser for SQLite > Execute SQL ekranında çalıştırılabilir.
-- Her IssueCount değeri 0 olmalıdır.

PRAGMA integrity_check;
PRAGMA foreign_key_check;

SELECT 'UsersWithoutRole' AS CheckName, COUNT(*) AS IssueCount
FROM Users u
WHERE NOT EXISTS (SELECT 1 FROM UserRoles ur WHERE ur.UserId = u.Id)
UNION ALL
SELECT 'UsersWithMultipleRoles', COUNT(*)
FROM (SELECT UserId FROM UserRoles GROUP BY UserId HAVING COUNT(*) > 1)
UNION ALL
SELECT 'DuplicateRegistrationNumber', COUNT(*)
FROM (SELECT RegistrationNumber FROM Users GROUP BY RegistrationNumber HAVING COUNT(*) > 1)
UNION ALL
SELECT 'DuplicateNationalIdentityNumber', COUNT(*)
FROM (SELECT NationalIdentityNumber FROM Customers GROUP BY NationalIdentityNumber HAVING COUNT(*) > 1)
UNION ALL
SELECT 'DuplicateEmail', COUNT(*)
FROM (SELECT lower(EmailAddress) FROM Customers GROUP BY lower(EmailAddress) HAVING COUNT(*) > 1)
UNION ALL
SELECT 'DuplicatePhone', COUNT(*)
FROM (SELECT PhoneCountryCode, PhoneNumber FROM Customers
      GROUP BY PhoneCountryCode, PhoneNumber HAVING COUNT(*) > 1)
UNION ALL
SELECT 'CustomerWithoutAddress', COUNT(*)
FROM Customers c
WHERE c.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM CustomerAddresses a WHERE a.CustomerId = c.Id AND a.IsActive = 1)
UNION ALL
SELECT 'MultipleDefaultAddress', COUNT(*)
FROM (SELECT CustomerId FROM CustomerAddresses WHERE IsActive = 1 AND IsDefault = 1
      GROUP BY CustomerId HAVING COUNT(*) > 1)
UNION ALL
SELECT 'InvalidStructuredAddress', COUNT(*)
FROM CustomerAddresses
WHERE IsActive = 1 AND (
    length(trim(Street)) < 2 OR length(trim(BuildingNo)) = 0
    OR PostalCode NOT GLOB '[0-9][0-9][0-9][0-9][0-9]')
UNION ALL
SELECT 'OtherBankTotalMismatch', COUNT(*)
FROM Customers c
WHERE abs(CAST(c.OtherBankTotalCardLimit AS REAL) - COALESCE((
    SELECT SUM(CAST(o.CardLimit AS REAL)) FROM OtherBankCards o
    WHERE o.CustomerId = c.Id AND o.IsActive = 1), 0)) > 0.01
UNION ALL
SELECT 'ApprovedApplicationWithoutCard', COUNT(*)
FROM CardApplications a
WHERE a.Status = 3
  AND NOT EXISTS (SELECT 1 FROM CreditCards c WHERE c.CardApplicationId = a.Id)
UNION ALL
SELECT 'CardWithoutFulfillment', COUNT(*)
FROM CreditCards c
WHERE NOT EXISTS (SELECT 1 FROM CardFulfillments f WHERE f.CreditCardId = c.Id)
UNION ALL
SELECT 'SupplementaryCardForSelf', COUNT(*)
FROM SupplementaryCardApplications
WHERE PrimaryCustomerId = SupplementaryHolderCustomerId
UNION ALL
SELECT 'SupplementaryLimitAbovePrimaryLimit', COUNT(*)
FROM SupplementaryCardApplications s
JOIN CreditCards c ON c.Id = s.PrimaryCreditCardId
WHERE CAST(s.RequestedLimit AS REAL) > CAST(c.CardLimit AS REAL)
UNION ALL
SELECT 'ExactDuplicateNotification', COUNT(*)
FROM (
    SELECT UserId, Type, Title, Message, Link, CreatedAtUtc
    FROM Notifications
    GROUP BY UserId, Type, Title, Message, Link, CreatedAtUtc
    HAVING COUNT(*) > 1
);
