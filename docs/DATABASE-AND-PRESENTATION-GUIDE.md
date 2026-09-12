# KKBYS Veritabanı ve Sunum Rehberi

## 1. Kullanılan veri erişim yapısı

Proje, .NET 8 üzerinde Entity Framework Core ve SQLite kullanır. `ApplicationDbContext`, tabloların alanlarını, benzersiz indekslerini ve ilişkilerini tek merkezden tanımlar. `DatabaseInitializer` ise eski yerel prototip veritabanlarını veri kaybetmeden güncel şemaya taşıyan idempotent uyumluluk katmanıdır. Aynı komut birden fazla kez çalışsa da aynı tabloyu veya aynı kaydı tekrar üretmez.

SQLite bağlantısında yabancı anahtar denetimi, 30 saniyelik kilit bekleme süresi ve WAL günlükleme modu etkinleştirilir. Uygulama başlarken `quick_check` ve `foreign_key_check` çalışır. Bozuk veya ilişkisel bütünlüğü ihlal edilmiş bir dosyayla API açılmaz. Ayrıca aynı proje klasöründen ikinci bir backend sürecinin aynı veritabanını açması dosya kilidiyle engellenir.

## 2. Tablolar neden var?

### Kimlik ve yetki

- `Users`: Memur ve müdür personel hesaplarıdır. Parola açık metin değil BCrypt özeti olarak tutulur.
- `Roles`: Officer ve Manager rol kataloğudur.
- `UserRoles`: Kullanıcı–rol çoktan çoğa ilişki tablosudur. Rolü kullanıcı satırında metin olarak tekrar etmek yerine normalizasyon sağlar.
- `CustomerAccounts`: Müşteri portalına ait kimlik doğrulama hesabıdır. `Customers` müşteri iş verisi, bu tablo ise giriş güvenliği verisidir; bu nedenle ayrı tutulmaları bilinçlidir.
- `LoginHistories`: Başarılı/başarısız girişlerin güvenlik geçmişidir.

### Müşteri ve açık rıza

- `Customers`: Müşterinin kimlik, iletişim, finansal özet ve güncel varsayılan adres bilgisidir.
- `CustomerAddresses`: Ev, iş veya farklı teslimat adresleri gibi tekrar kullanılabilir yapılandırılmış adreslerdir. `Customers.Address` güncel özet/varsayılan adresi hızlı göstermek için tutulur; adres geçmişi ve çoklu adres bu tabloda yönetilir.
- `OtherBankCards`: Prototipte dış risk kaynağından geldiği varsayılan diğer banka kartlarıdır.
- `CustomerConsents`: KVKK, SMS ve e-posta izinlerinin sürüm, kanal, tarih ve personel bilgisiyle geçmişini tutar. Tek bir `IsConsent` alanı geçmişi koruyamayacağı için ayrı tablodur.

### Başvuru ve karar akışı

- `CardTypes`: Kart ürün kataloğudur; BIN ve limit sınırları benzersizdir.
- `CardApplications`: Asıl kredi kartı başvurusunun güncel durumudur.
- `ApplicationHistories`: Başvurunun durum geçişleri ve açıklamalarından oluşan zaman çizelgesidir.
- `ApplicationRevisionSnapshots`: Revizyondan önceki ve sonraki alanların karşılaştırılabilmesi için anlık görüntüdür.
- `SupplementaryCardApplications`: Ek kart, asıl kartın limitini paylaşan farklı sahibi ve farklı yaşam döngüsü olan bir ürün olduğu için ana başvurudan ayrı tutulur.

### Kart, belge ve operasyon

- `CreditCards`: Onaylı başvurudan üretilen kartın maskeli numarası, limiti ve güncel durumudur. Güvenlik nedeniyle tam PAN veya CVV hiçbir zaman saklanmaz.
- `CardFulfillments`: Üretim, basım, kargo ve teslim zamanlarını ayrı bir operasyonel yaşam döngüsü olarak tutar.
- `GeneratedDocuments`: Oluşturulan PDF’in dosya yolu, SHA-256 özeti, e-posta ve doğrulama durumudur; PDF ikili verisi veritabanını şişirmemek için dosya sistemindedir.
- `Notifications`: Kullanıcıya özel sistem ve SLA bildirimleridir.
- `ChatMessages`: Personeller arası mesajlaşma kayıtlarıdır.
- `AuditLogs`: Müşteri ve başvuru dışındaki genel kritik işlemlerde “kim, neyi, ne zaman yaptı?” izidir.

`ApplicationHistories`, `AuditLogs` ve `LoginHistories` benzer görünse de aynı veri değildir: ilki başvuru durum geçmişi, ikincisi genel işlemsel denetim izi, üçüncüsü güvenlik giriş geçmişidir. Bunları tek tabloda toplamak sorguları ve saklama politikalarını belirsizleştirirdi.

## 3. Benzersizlik ve ilişki kuralları

- Sicil numarası, müşteri numarası, T.C. kimlik numarası ve kart başvuru numarası benzersizdir.
- E-posta ile ülke kodu + telefon numarası kombinasyonu benzersizdir.
- Bir asıl başvurunun en fazla bir kredi kartı; bir kredi kartının en fazla bir üretim/teslim kaydı olabilir.
- Bir müşterinin en fazla bir portal hesabı vardır.
- Ek kart sahibi ile asıl kart sahibi aynı kişi olamaz; ek kart limiti asıl kartın kullanılabilir sınırını aşamaz.
- Onaylanmış asıl başvurunun kartı, oluşmuş kartın üretim/teslim kaydı bulunmalıdır.
- Diğer bankalardaki toplam limit, aktif `OtherBankCards` satırlarının toplamıyla uyumlu olmalıdır.

Bu kuralların önemli bölümü hem servis katmanında iş kuralı olarak hem de benzersiz indeks/yabancı anahtar ile veritabanı katmanında savunulur. Böylece yalnızca arayüz doğrulamasına güvenilmez.

## 4. Demo veri ile üretim verisinin ayrılması

Demo kullanıcıları ve örnek müşteriler yalnızca `Development` ortamında ve `DemoData:Enabled=true` iken hazırlanır. Üretim yapılandırmasında demo seed kapalı, doğrulama kodunun yanıtta gösterilmesi kapalı ve JWT anahtarı boş bırakılmıştır. Gerçek bir ortamda `Jwt__Key` güvenli ortam değişkeninden sağlanmadan API başlamaz.

Geliştirme hesapları `appsettings.Development.json` içinde açıkça “demo” kapsamındadır:

- Memur: `KBP000001` / `DemoLogin01!`
- Müdür: `KBP000002` / `DemoLogin01!`
- Müşteri: `MUS000001` / `Musteri123!`

Bu bilgiler yalnızca yerel sunum içindir. Parolaların veritabanındaki karşılığı açık metin değil BCrypt özetidir.

## 5. Sunumda dürüstçe belirtilmesi gereken prototip sınırları

- KKB/Findeks sonucu gerçek kuruma bağlanmaz; deterministik karar destek simülasyonudur. Otomatik onay vermez.
- E-posta ve SMS geliştirme ortamında dosya tabanlı outbox ile simüle edilir. Servis soyutlaması gerçek sağlayıcıya geçirilebilir.
- SQLite tek makine ve sunum prototipi için uygundur. Çok kullanıcılı üretimde PostgreSQL veya SQL Server, EF Core migration, merkezi secret yönetimi ve yedekleme politikası gerekir.
- Tam kart numarası ve CVV saklanmaz. Bu bir eksik değil, PCI DSS yaklaşımına uygun güvenlik kararıdır.
- `DatabaseInitializer` mevcut prototip dosyalarını bozmadan yükseltmek içindir. Kurumsal üretim sürümünde versiyonlanmış EF migration’lara dönüştürülmelidir.

## 6. Sunum öncesi hızlı kontrol

Backend:

```bash
cd "/Users/silatemel/Developer/kredi-karti-basvuru-sistemi/backend"
dotnet test CreditCardApplicationSystem.sln --no-restore
dotnet run --project src/CreditCardApplication.Api/CreditCardApplication.Api.csproj --launch-profile http
```

Frontend ayrı terminalde:

```bash
cd "/Users/silatemel/Developer/kredi-karti-basvuru-sistemi/kart-basvuru-ui"
export PATH="/opt/homebrew/opt/node@22/bin:/opt/homebrew/bin:$PATH"
npm start
```

API `http://localhost:5057`, Angular `http://localhost:4200` adresinde açılır. DB Browser for SQLite ile açılacak dosya:

`backend/src/CreditCardApplication.Api/credit-card-application.db`

SQL denetimleri için `docs/database-health-check.sql` dosyası DB Browser’ın **Execute SQL** sekmesinde çalıştırılabilir. Bütün `IssueCount` değerlerinin `0`, `integrity_check` sonucunun `ok` olması beklenir.

## 7. Sık sorulabilecek sunum soruları

**Neden SQLite?** Yerel, kurulumsuz ve taşınabilir bir staj prototipi olduğu için. Veri erişimi EF Core ile soyutlandığından sağlayıcı değişimi mümkündür.

**Neden bazı özet değerler ana tabloda da var?** Dashboard ve karar hesaplarında sık kullanılan güncel değerleri hızlı okumak için kontrollü denormalizasyon uygulanmıştır; denetim sorguları kaynak detayla uyumunu kontrol eder.

**Neden ek kart ayrı tablo?** Ek kart, asıl kartın limitini paylaşır fakat sahibi, değerlendirmesi, teslimatı ve kart üretim süreci farklıdır.

**Şifreler neden DB Browser’da okunmuyor?** Çünkü BCrypt tek yönlü parola özeti saklanır. Test için geliştirme parolaları yapılandırmada bulunur; gerçek ortamda secret store kullanılır.

**Neden telefon ve e-posta okunuyor?** Yetkili personel müşteriye ulaşabilmelidir. API rol ve oturum kontrolü uygular; dışa aktarılan belgelerde ve ekranda bağlama göre maskeleme yapılabilir. Kart PAN/CVV gibi ödeme verileri ise hiç tutulmaz.
