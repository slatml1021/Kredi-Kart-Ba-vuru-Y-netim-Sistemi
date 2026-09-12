# KKBYS Postman API Testleri

## Testlerin kaynağı

24 Ağustos 2026 tarihinde yapılan ilk canlı API kontrolleri Postman ile değil, terminalde curl komutları ve backend’de dotnet test ile çalıştırıldı. Bu klasördeki collection, aynı doğrulamaları Postman’da kalıcı ve sunumda gösterilebilir hâle getirir.

## İçe aktarma

1. Postman ana ekranında Import seçeneğini açın.
2. KKBYS-API-Regression.postman_collection.json dosyasını seçin.
3. KKBYS-Son-Revizyon.postman_collection.json dosyasını da seçin.
4. KKBYS-Local.postman_environment.json dosyasını seçin.
5. Sağ üstteki environment menüsünden KKBYS Local ortamını etkinleştirin.
6. Backend’in http://127.0.0.1:5057 adresinde çalıştığını doğrulayın.
7. Collection üzerindeki Run düğmesiyle klasörleri verilen sırada çalıştırın.

Mock server oluşturulmasına gerek yoktur. Mock server gerçek .NET API’yi test etmez; yalnızca örnek cevap döndürür. Bu proje için collection doğrudan yerel backend’e istek gönderir.

## Collection kapsamı

- API sağlık kontrolü
- Anonim erişimin 401 ile reddedilmesi
- Memur, müdür ve senaryo memuru girişi
- Memur/müdür rol ayrımı ve 403 kontrolleri
- Müşteri listesi, detay ve adresler
- Başvuru listesi, detay ve açıklanabilir ön değerlendirme
- KKB risk analizi
- Ek kart ve limit talebi listeleri
- SLA servisi
- Geçerli ve geçersiz limit hesaplama
- Bildirimlerin tümünü okundu işaretleme
- Çıkış sonrası token iptali
- Ayrı müşteri portalı girişi ve dashboard
- Ulusal sokak/cadde kataloğu
- Classic/Gold/Platinum ürünleri ile Visa/Mastercard/TROY ağ ayrımı
- Ece Aydın müşteri ve aktif kart detayının uçtan uca okunması
- Müdür paneli için seçilebilir ay/yıl ve 12 aylık yıllık analiz
- Kart ağı yerine Classic/Gold/Platinum/Platinum Plus ürün seviyesi dağılım kontrolü

Belge yükleme kuralında kimlik, gelir ve ikamet dosyalarının yüklenmiş ve reddedilmemiş olması yeterlidir. Müdürün isteğe bağlı kalite kontrolü onay için zorunlu değildir. Luhn kontrol hanesi doğrudan API’den tam kart numarası döndürülmeden backend otomatik testleriyle doğrulanır; tam PAN güvenlik nedeniyle Postman cevabına veya SQLite veritabanına yazılmaz.

## Sunum notu

Collection içinde token’lar giriş isteklerinden sonra otomatik olarak environment’a yazılır. customerId ve applicationId değerleri de liste cevaplarından alınır. Böylece sabit token kopyalamak veya her isteğe elle kimlik eklemek gerekmez.

Environment dosyasındaki parolalar yalnızca Development demo hesaplarına aittir. Gerçek ortam parolaları veya üretim token’ları bu dosyaya eklenmemelidir.
