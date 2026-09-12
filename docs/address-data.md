# Adres kataloğu

- İl, ilçe, mahalle ve posta kodu verileri TurkiyeAPI v2 üzerinden alınır.
- Posta kodu il plakasından üretilmez; seçilen mahalle kaydındaki beş haneli `postalCode` alanı kullanılır.
- Cadde/sokak/bulvar/meydan kataloğu, Mart 2026 tarihinde NVI adres sorgusuyla hizalandığını belirten MIT lisanslı `onurusluca/turkey-geo-api` veri paketinden üretilmiştir.
- API yalnızca seçilen mahalleye bağlı kayıtları salt okunur SQLite kataloğundan döndürür. Katalog uygulamanın müşteri veritabanından ayrıdır ve kişisel veri içermez.
- Resmî üretim ortamında bu açık veri kataloğu, kurumun yetkili UAVT/MAKS entegrasyonuyla değiştirilmelidir.

Kaynaklar:

- https://docs.turkiyeapi.dev/tr/v2/guide/concepts
- https://github.com/onurusluca/turkey-geo-api
- https://www.nvi.gov.tr/adres-hizmetleri
