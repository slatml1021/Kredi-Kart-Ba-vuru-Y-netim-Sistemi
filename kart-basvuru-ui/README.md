# Kredi Kartı Başvuru Sistemi

Eğitim amaçlı kredi kartı başvuru yönetim uygulamasıdır. Memur müşteri ve başvuru işlemlerini yürütür; müdür başvuruyu onaylar, reddeder veya revizyona gönderir.

## Teknolojiler

- Backend: ASP.NET Core 8 Web API, Entity Framework Core, SQLite, JWT, BCrypt
- Frontend: Angular, TypeScript, SCSS, Reactive Forms

## Çalıştırma

İki terminal kullanın.

Arayüz için Node.js 22 kullanın. Proje kökündeki `.nvmrc` dosyası gereken ana sürümü belirtir.

```bash
cd "/Users/silatemel/Documents/Staj Projem/backend"
dotnet run --project src/CreditCardApplication.Api
```

API varsayılan olarak `http://localhost:5057` adresinde çalışır. İlk çalıştırmada geliştirme ortamında SQLite veritabanı, roller, demo kullanıcılar ve kart tipleri otomatik oluşturulur.

```bash
cd "/Users/silatemel/Documents/Staj Projem/kart-basvuru-ui"
npm install
npm start
```

Arayüz: `http://localhost:4200`

Angular geliştirme sunucusu `/api` isteklerini `proxy.conf.json` üzerinden `http://localhost:5057` adresindeki backend'e yönlendirir. Bu nedenle arayüz servislerinde sabit API adresi bulunmaz.

## Demo hesaplar

| Rol | Sicil numarası | Parola |
|---|---|---|
| Memur | `BSP000001` | `Demo123!` |
| Müdür | `BSP000002` | `Demo123!` |

## Ana kullanıcı akışı

1. Memur giriş yapar; müşteri arar, oluşturur veya günceller.
2. Memur kart tipi ve limit seçerek başvuruyu `Pending` oluşturur.
3. Müdür başvuruyu `Approved`, `Rejected` ya da `Revision` olarak değerlendirir.
4. Revizyona gönderilen başvuru, aynı memur tarafından güncellenip tekrar gönderilir.
5. Onaylanan başvuruda maskeli numaralı, `Inactive` durumunda demo kredi kartı oluşturulur.

## Temel API uçları

| Endpoint | Yetki | Açıklama |
|---|---|---|
| `POST /api/auth/login` | Herkes | JWT ile giriş |
| `GET/POST/PUT /api/customers` | Memur | Müşteri işlemleri |
| `GET/POST /api/card-applications` | Memur | Başvuru işlemleri |
| `POST /api/card-applications/{id}/evaluation` | Müdür | Onay, ret veya revizyon |
| `POST /api/card-applications/{id}/resubmit` | Memur | Revize başvuruyu tekrar gönderme |
| `GET /api/card-applications/{id}/detail` | Rol bazlı | Başvuru ve zaman çizgisi |
| `GET /api/cards/{id}` | Rol bazlı | Kart detayı |
| `GET /api/dashboard/officer` | Memur | Memur istatistikleri |
| `GET /api/dashboard/manager` | Müdür | Müdür istatistikleri |

## Doğrulama

```bash
cd "/Users/silatemel/Documents/Staj Projem/backend"
dotnet build CreditCardApplicationSystem.sln
dotnet test CreditCardApplicationSystem.sln
```

API hata yanıtları `Problem Details` biçimindedir. Parolalar yalnızca BCrypt özeti olarak saklanır; kart numarası maskeli döner.
