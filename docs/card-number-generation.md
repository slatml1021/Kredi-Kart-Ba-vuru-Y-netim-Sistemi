# Kart Numarası Üretimi: BIN/IIN ve Luhn

Bu projede kredi kartı numarası 16 haneli bir PAN (Primary Account Number) olarak üretilir. İlk bölüm kart tipinde tanımlanan 6 veya 8 haneli BIN/IIN değeridir. BIN/IIN; kartın ürününü, kart ağını ve kartı çıkaran kuruluşu temsil eden, kurum tarafından tahsis edilmiş kabul edilen sabit ön ektir.

Kalan yapı aşağıdaki kurala göre oluşturulur:

1. Kart tipine ait 6 veya 8 haneli BIN/IIN başa yazılır.
2. Son kontrol hanesine kadar olan orta bölüm kriptografik olarak güvenli rastgele rakamlarla tamamlanır.
3. Son hane Luhn (mod 10) algoritmasıyla hesaplanır.
4. Üretilen 16 haneli PAN yeniden Luhn doğrulamasından geçirilir.
5. Tam PAN veritabanına kaydedilmez; yalnız BIN/IIN ve son dört haneyi içeren maskeli gösterim saklanır.

Örnek maskeler:

- 6 haneli BIN/IIN: `450001 ** **** 1234`
- 8 haneli BIN/IIN: `97920102 **** 1234`

## Luhn doğrulaması

Kontrol hanesi dışındaki rakamlar sağdan sola işlenir. Her ikinci rakam ikiyle çarpılır; sonuç 9'dan büyükse 9 çıkarılır. Düzeltilmiş rakamların toplamını 10'un katına tamamlayan rakam son kontrol hanesidir. Böylece basit yazım hataları ve birçok komşu hane değişimi yakalanabilir.

Luhn algoritması güvenlik veya şifreleme sağlamaz. Sadece numaranın biçimsel bütünlüğünü kontrol eder. Gerçek üretim ortamında PAN üretimi HSM, PCI DSS kapsamı, kart ağı kuralları ve kurumun yetkili kart yönetim sistemi üzerinden yürütülmelidir.

## Projedeki uygulama

- Üretici: `PaymentCardNumberGenerator`
- Kart oluşturma noktası: `CardApplicationService.CreateDemoCard`
- BIN/IIN kaynağı: `CardType.Bin`
- Otomatik test: 6 ve 8 haneli BIN/IIN için uzunluk, ön ek, maskeleme ve Luhn geçerliliği test edilir.
