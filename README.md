# Kredi Kartı Başvuru Yönetim Sistemi

Angular 20 ve ASP.NET Core 8 ile geliştirilen bu proje; banka personelinin asıl kart, ek kart ve limit taleplerini tek uygulamadan yönetmesini sağlayan uçtan uca bir staj prototipidir. Müşteri, memur ve müdür ekranları; rol tabanlı yetkilendirme, iş atama, onay/revizyon akışları, kart üretim simülasyonu ve denetim kayıtlarıyla birlikte çalışır.

> Proje yerel geliştirme ve sunum amacıyla hazırlanmıştır. Gerçek KKB/Findeks, SMS, e-posta, kart basım ve kurye servisleri simüle edilir; gerçek kart numarası veya CVV saklanmaz.

## Öne çıkan özellikler

- Memur ve müdür için JWT tabanlı oturum ve rol yetkilendirmesi
- Müşteri arama, oluşturma, profil/KVKK ve çoklu adres yönetimi
- Asıl kredi kartı başvurusu, belge yükleme ve başvuru özeti
- Ana karttan başlatılabilen ek kart başvurusu ve ek kart sahibinin ayrı kontrolü
- Müdür değerlendirmesi: onay, ret, revizyon ve yüksek tutarda iki farklı müdür onayı
- Manuel veya otomatik memur atama, SLA seviyesi ve işlem geçmişi
- Limit artırım/azaltım talepleri ve müdür karar ekranı
- Kart üretimi, maskeli PAN, kart ağı, basım–kurye–teslim yaşam döngüsü
- Müşteri portalı, personel iletişim merkezi, bildirim ve denetim kayıtları
- Günlük, haftalık, aylık ve yıllık yönetim analizleri
- 200 müşteri, 50 memur, 5 müdür, 200 ana kart ve 200 ek karttan oluşan sunum veri seti

## Teknik yapı

```text
kart-basvuru-ui/                        Angular 20, TypeScript, SCSS
backend/src/
  CreditCardApplication.Api/            HTTP uçları, JWT ve ara katmanlar
  CreditCardApplication.Application/    kullanım senaryoları ve iş kuralları
  CreditCardApplication.Domain/         varlıklar ve durum sabitleri
  CreditCardApplication.Infrastructure/ EF Core, SQLite, veri hazırlama ve servisler
backend/tests/                           xUnit iş kuralı ve veri bütünlüğü testleri
docs/                                    test senaryoları, veri tabanı ve sunum belgeleri
```

Veri erişimi Entity Framework Core ve SQLite ile sağlanır. Yerel adres seçimi için uygulamayla birlikte ulusal adres kataloğu kullanılır. Uygulama başlangıcında yabancı anahtar ve bütünlük kontrolleri çalıştırılır.

## İş kuralları ve güvenlik

- Kart numarası Luhn kontrol hanesiyle üretilir; yalnızca maskeli gösterim saklanır.
- T.C. kimlik numarası biçim ve kontrol rakamlarıyla doğrulanır.
- Parolalar açık metin değil BCrypt özeti olarak tutulur.
- Güncel KVKK onayı, eksiksiz müşteri profili ve zorunlu belge kontrolleri olmadan asıl kart başvurusu oluşturulamaz.
- Aynı kart tipi için açık başvuru veya aktif/onaylı kart varsa mükerrer başvuru engellenir.
- 100.000 TL üzerindeki ya da yüksek riskli başvurular iki farklı müdürün onayını gerektirir.
- Kartın tam PAN/CVV bilgisi, hassas belge içeriği ve parola değerleri loglara yazılmaz.

## Yerel çalıştırma

Gereksinimler: .NET 8 SDK, Node.js 22 ve npm 10 veya üzeri.

Backend:

```bash
cd backend
dotnet restore CreditCardApplicationSystem.sln
dotnet run --project src/CreditCardApplication.Api/CreditCardApplication.Api.csproj --launch-profile http
```

Frontend, ayrı bir terminalde:

```bash
cd kart-basvuru-ui
npm ci
npm start
```

Uygulama `http://localhost:4200`, API ise `http://localhost:5057` adresinde açılır. Geliştirme verisi `ASPNETCORE_ENVIRONMENT=Development` ve `DemoData:Enabled=true` olduğunda idempotent olarak hazırlanır.

### Sunum hesapları

| Rol | Sicil / kullanıcı no | Parola |
| --- | --- | --- |
| Memur | `KBP000001` | `DemoLogin01!` |
| Müdür | `KBP000002` | `DemoLogin01!` |
| Müşteri portalı | `MUS000001` | `Musteri123!` |

Bu bilgiler yalnızca yerel geliştirme ortamı içindir.

## Doğrulama

```bash
cd backend
dotnet test CreditCardApplicationSystem.sln

cd ../kart-basvuru-ui
npm run build
npx tsc -p tsconfig.spec.json --noEmit
```

API regresyon koleksiyonları ve beklenen sonuçlar [Postman belgelerinde](docs/postman/README.md), elle doğrulanabilecek uçtan uca senaryolar ise [test senaryolarında](docs/TEST-SCENARIOS.md) bulunur.

## Dokümantasyon

- [Proje sunumu (PDF)](docs/presentation/Kredi_Karti_Basvuru_Yonetim_Sistemi_Proje_Sunumu_20260907_v4.pdf)
- [Veritabanı ve sunum rehberi](docs/DATABASE-AND-PRESENTATION-GUIDE.md)
- [Test senaryoları](docs/TEST-SCENARIOS.md)
- [Kart numarası üretimi ve Luhn](docs/card-number-generation.md)
- [Adres veri kaynağı](docs/address-data.md)
- [Veritabanı sağlık kontrolü](docs/database-health-check.sql)

## Üretime geçiş öncesi

SQLite yerine yönetilen bir kurumsal veri tabanı, merkezi gizli bilgi yönetimi, gerçek SMS/e-posta ve görüşme sağlayıcıları, zararlı dosya taraması, yük/penetrasyon/erişilebilirlik testleri, gözlemlenebilirlik ve yedekleme politikaları tamamlanmalıdır. Demo veri üretimi ve geliştirme doğrulama kodları üretim ortamında kapalı tutulmalıdır.

## Geliştirici

Sıla Temel — Yazılım Mühendisliği
