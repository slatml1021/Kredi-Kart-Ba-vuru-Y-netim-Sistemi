# Kredi Kartı Başvuru Sistemi — V1 Teknik Kararlar

**Durum:** Kodlama için tek referans doküman  
**Tarih:** 21 Temmuz 2026  
**Amaç:** Önceki analiz belgelerindeki kapsam ve terminoloji farklarını V1 için kesinleştirmek.

## 1. V1'in amacı

Kredi Kartı Başvuru Sistemi; banka personelinin müşteri kaydı oluşturmasını veya bulmasını, müşteri için kredi kartı başvurusu hazırlamasını ve bir müdürün başvuruyu değerlendirmesini sağlayan eğitim amaçlı web uygulamasıdır. Sistemin ana amacı, kredi kartı başvuru sürecini başlangıçtan sonuçlandırmaya kadar dijital olarak yönetmektir.

V1, gerçek bankacılık sisteminin yerine geçmez. Kart üretimi ve limit hesaplama işlemleri kontrollü simülasyondur.

## 2. Roller ve yetkiler

| İşlem | Memur | Müdür |
|---|---:|---:|
| Sisteme giriş | ✓ | ✓ |
| Kendi dashboard'unu görüntüleme | ✓ | ✓ |
| Müşteri arama / ekleme / güncelleme | ✓ | — |
| Başvuru oluşturma | ✓ | — |
| Kendi başvurularını görme | ✓ | — |
| Revizyona dönen kendi başvurusunu güncelleme ve yeniden gönderme | ✓ | — |
| Bekleyen başvuruları görme | — | ✓ |
| Onaylama, reddetme, revizyona gönderme | — | ✓ |
| Başvuru ayrıntısını görme | Yalnızca kendi kaydı | Değerlendirme havuzundakiler |

Yetki hem Angular ekranında hem de API tarafında uygulanır. Arayüzdeki butonun gizlenmesi güvenlik kontrolü değildir.

## 3. Kesin iş akışı

```text
Müşteri seçilir/oluşturulur
        ↓
Limit hesaplanır
        ↓
Memur başvuruyu oluşturur (Pending)
        ↓
Müdür: Approved / Rejected / Revision
        ↓
Revision ise memur düzeltir ve yeniden gönderir (Pending)
        ↓
Approved ise demo kredi kartı üretilir (Inactive)
```

### Başvuru durumları

`Pending`, `Revision`, `Approved`, `Rejected`

- Yeni başvuru daima `Pending` olur.
- Sadece `Pending` başvuru müdür tarafından sonuçlandırılır.
- `Revision` için açıklama zorunludur.
- Memur, yalnızca kendisinin oluşturduğu `Revision` başvuruyu değiştirip tekrar `Pending` durumuna alabilir.
- `Approved` ve `Rejected` kayıtlar değiştirilemez veya silinemez.

## 4. Limit kuralı

```text
teorikToplamLimit = aylıkNetGelir × 3
kullanilabilirLimit = max(0, teorikToplamLimit − digerBankalardakiToplamLimit)
requestedLimit ≤ kullanilabilirLimit
```

V1'de kredi notu katsayısı, risk motoru ve bankadaki mevcut kart toplamı uygulanmaz. Bunlar gelecek sürüm konusudur.

## 5. Veri modeli — V1

V1 için **10 tablo** yeterlidir. Bu karar normalizasyon ile geliştirme hızını dengeler.

| Tablo | Sorumluluk |
|---|---|
| `Users` | Personel hesabı, sicil no, parola özeti, aktiflik |
| `Roles` | `Officer` ve `Manager` rolleri |
| `UserRoles` | Kullanıcı–rol ilişkisi |
| `Customers` | Müşteri kimlik, iletişim ve gelir bilgileri |
| `CardTypes` | Kart tipi, BIN, aktiflik, isteğe bağlı limit aralığı |
| `CardApplications` | Başvuru ve değerlendirme bilgileri |
| `ApplicationHistories` | Başvuru durum değişiklikleri ve açıklama |
| `CreditCards` | Onaylanan başvurudan oluşan demo kart |
| `LoginHistories` | Başarılı/başarısız giriş denemeleri |
| `AuditLogs` | Kritik eylemlerin izlenebilir kaydı |

### Bilinçli olarak dışarıda bırakılanlar

- Telefon/e-posta için ayrı tablolar ve tip lookup tabloları
- Sektör, eğitim, meslek, cinsiyet lookup tabloları
- Kart kullanım tercihi için ara tablolar
- Gerçek harcama/hareket, borç, ödeme ve ekstre modülleri
- Kart teslimat süreci

## 6. Güvenlik kararları

- Parola yalnızca BCrypt hash olarak saklanır.
- Giriş başarılı olduğunda JWT üretilir.
- API endpoint'lerinde rol tabanlı yetkilendirme kullanılır.
- Üç hatalı girişte hesap 10 dakika kilitlenir; V1'de ikinci 30 dakika kuralı uygulanmaz.
- Tüm API hata yanıtları tek biçimli Problem Details yapısında döner.
- Entity'ler istemciye doğrudan gönderilmez; Request/Response DTO kullanılır.
- CVV saklanmaz.
- Kart numarası ekranda ve API response'unda maskeli gösterilir: `12345678 **** 1234`.

## 7. Backend kapsamı

Teknoloji: **ASP.NET Core 8 Web API + Entity Framework Core + SQLite**

Katmanlar:

```text
API (Controllers, middleware, DTO)
Application (iş kuralları, servisler)
Domain (entity ve enum'lar)
Infrastructure (EF Core, SQLite, repository ve güvenlik)
```

Zorunlu endpoint grupları: `auth`, `dashboard`, `customers`, `card-types`, `applications`, `cards`.

## 8. Frontend kapsamı ve bağlayıcı UI sözleşmesi

Teknoloji: **Angular + TypeScript + SCSS**

`7.1 UI / UX Tasarımları` belgesi V1'in bağlayıcı ekran ve görsel tasarım kaynağıdır. Bu belgede gösterilen hiçbir ana ekran, form bölümü, liste filtresi, durum göstergesi veya kullanıcı işlemi V1'den çıkarılmaz. Kodlama sırasında alan adları veri modeliyle uyumlu hâle getirilebilir; ancak kullanıcı akışı korunur.

### Görsel tasarım sistemi

- Ana renk koyu lacivert; vurgu rengi altın/sarıdır.
- Uygulama ekranlarında sabit lacivert sol menü ve beyaz üst çubuk kullanılır.
- İçerik zemini açık gri/kırık beyaz; kartlar beyaz ve ince gri çerçevelidir.
- Durumlar renkli rozetlerle gösterilir: beklemede gri/mavi, onaylandı yeşil, reddedildi kırmızı, revizyonda sarı.
- Başlık, boşluk, buton, tablo, form ve modal ölçüleri tüm ekranlarda aynı tasarım sistemiyle uygulanır.
- Mobil görünümde içerik uyarlanır; masaüstü yerleşimi esas tasarımdır.

### 7.1 belgesinden eksiksiz uygulanacak ekranlar

1. Login
2. Memur dashboard
3. Müdür dashboard
4. Müşteri arama / liste
5. Müşteri ekleme ve güncelleme
6. Başvuru oluşturma (adımlı form)
7. Başvurularım
8. Başvuru detay ve durum zaman çizgisi
9. Müdür değerlendirme kuyruğu
10. Kart detay
11. Revizyondaki başvuruyu güncelleme
12. Başvuru başarı/onay ekranı
13. Onay, red ve revizyon değerlendirme modalları

Dashboard istatistik kartları, analiz grafikleri, son başvurular tablosu, arama ve durum filtreleri tasarımda gösterildiği biçimde korunur. Veriler ilk aşamada örnek veriyle, API tamamlandığında backend yanıtlarıyla beslenir.

Zorunlu Angular kavramları: component, service, reactive form, routing, route guard, HTTP interceptor ve TypeScript interface.

## 9. Kabul kriterleri

- Memur giriş yapar, müşteri oluşturur, limit hesaplar ve başvuru gönderir.
- Müdür giriş yapar, bekleyen başvuruyu revizyona gönderir veya sonuçlandırır.
- Revize başvuru sahibi memur tarafından düzeltilip tekrar gönderilebilir.
- Onaylanan başvurudan yalnızca bir demo kart oluşur.
- Yetkisiz rol, endpoint'e eriştiğinde `403 Forbidden` alır.
- Form ve backend aynı temel doğrulamaları uygular.
- Uygulama test kullanıcıları ve seed kart tipleri ile ilk çalıştırmada demo yapılabilir.

## 10. Gelecek sürüm notları

Kredi notu/risk skoru, çoklu kart limit hesabı, kart aktivasyonu, gerçek kart numarası üretimi, e-posta/SMS, gelişmiş raporlama, yönetici ekranları ve dış banka entegrasyonları V1 dışındadır.

## 11. Dokümanların görev ayrımı

Proje belgeleri tek belgede birleştirilmeyecektir. Her belge kendi sorusuna cevap verir ve böylece konular karışmaz:

| Belge | Cevap verdiği soru |
|---|---|
| Yazılım Gereksinim Analizi | Sistem ne yapmalıdır? |
| İş Kuralları ve Sistem Politikaları | Hangi koşulda neye izin verilir? |
| Teknik Mimari ve Teknoloji Tasarımı | Sistem teknik olarak nasıl kurulacaktır? |
| Veritabanı Tasarımı | Hangi veri, tablo ve ilişkiler saklanacaktır? |
| ER Diyagramları | Tablolar birbirine nasıl bağlanır? |
| API Tasarımı | Frontend ve backend nasıl haberleşir? |
| UI/UX Analizi | Kullanıcı akışları ve kullanım ilkeleri nelerdir? |
| 7.1 UI/UX Tasarımları | Ekranlar tam olarak nasıl görünür ve davranır? |
| Proje Yol Haritası | Hangi sırayla geliştirilecektir? |
| V1 Teknik Kararlar | Belgeler çeliştiğinde V1 için hangi karar geçerlidir? |

V1 Teknik Kararlar belgesi diğer belgelerin yerine geçmez. Yalnızca çelişki çözüm ve kapsam kontrol belgesidir. Geliştirme tamamlandığında her uzmanlık belgesi kendi konusu içinde güncellenir.
