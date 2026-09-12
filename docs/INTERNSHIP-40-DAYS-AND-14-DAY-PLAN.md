# KKBYS Staj Süreci: İlk 40 İş Günü ve Kalan 14 İş Günü

Bu belge, Kredi Kartı Başvuru Yönetim Sistemi projesinde tamamlanan ilk 40 iş gününü sunum dilinde özetler ve stajın kalan 14 iş günü için ölçülebilir geliştirme planını tanımlar. İlk bölüm gerçekleşen çalışmaları, ikinci bölüm ise henüz tamamlanmamış planlanan işleri içerir.

## İlk 40 iş gününde tamamlanan çalışmalar

### 1. Gün — Problem analizi ve kapsamın belirlenmesi

Kredi kartı başvurusunun müşteri kaydından başlayarak memur işlemleri, müdür değerlendirmesi, kart üretimi ve teslimata kadar uzanan yaşam döngüsü incelendi. Memur, müdür ve müşteri rollerinin sorumlulukları ayrıştırıldı; projenin yalnızca bir form uygulaması değil, süreç yönetim sistemi olması hedeflendi.

### 2. Gün — Gereksinim ve kullanım senaryoları

Müşteri sorgulama, müşteri oluşturma, kart başvurusu açma, onaylama, reddetme, revizyona gönderme, kart oluşturma ve başvuru takibi için temel kullanım senaryoları çıkarıldı. Yetki sınırları ve hatalı işlem senaryoları belirlendi.

### 3. Gün — Teknoloji ve mimari seçimi

Backend için .NET 8 ve ASP.NET Core Web API, veri erişimi için Entity Framework Core, prototip veritabanı için SQLite, frontend için Angular ve TypeScript seçildi. Katmanlı yapı Domain, Application, Infrastructure ve API projeleriyle oluşturuldu.

### 4. Gün — Proje iskeletinin hazırlanması

Backend çözümü ve Angular uygulaması oluşturuldu. Katman bağımlılıkları, servis kayıtları, API–frontend proxy bağlantısı ve geliştirme ortamı çalışma komutları düzenlendi.

### 5. Gün — Veri modelinin tasarlanması

Kullanıcı, rol, müşteri, kart tipi, başvuru, başvuru geçmişi ve kredi kartı varlıkları modellendi. Birincil anahtarlar, yabancı anahtarlar, benzersizlik kuralları ve ilişkisel bütünlük kararları belirlendi.

### 6. Gün — Personel kimlik doğrulama sistemi

Sicil numarası ve parola ile personel girişi geliştirildi. Parolalar BCrypt özetiyle saklandı; açık parola görüntüleme kaldırıldı. Memur ve müdür için JWT tabanlı oturum üretildi.

### 7. Gün — Rol bazlı yetkilendirme

Officer ve Manager rolleri API endpoint’lerinde ayrı ayrı korundu. Memurun yönetici kararlarına, müdürün memura özel oluşturma işlemlerine erişmesi engellendi. Angular route guard ve API 401/403 davranışları oluşturuldu.

### 8. Gün — Güvenli giriş ekranı

Tarayıcı otomatik doldurma davranışı sınırlandırıldı, alan doğrulamaları eklendi, hatalı giriş mesajları standartlaştırıldı ve giriş problemi bildirme akışı geliştirildi. Başarılı ve başarısız girişler güvenlik geçmişine alındı.

### 9. Gün — Uygulama kabuğu ve navigasyon

Ortak header, açılır yan menü, bildirim alanı, profil kartı ve çıkış işlemi geliştirildi. Sidebar ekranı sıkıştırmak yerine üstten açılan panel hâline getirildi ve durumunun oturum boyunca korunması sağlandı.

### 10. Gün — Memur dashboard

Başvuru sayıları, durum dağılımı, kart tipi dağılımı ve son işlemler gösterildi. Filtrelere göre tablonun yüksekliğinin değişmemesi ve grafik dilimlerinin hover ile doğru bilgiyi göstermesi sağlandı.

### 11. Gün — Müdür dashboard

Bekleyen başvuru, sonuçlandırılan işlem, onay/red oranı ve revizyon göstergeleri geliştirildi. Kart tipi dağılımı, başvuru eğilimi, SLA özeti ve ekip iş yükü görünür hâle getirildi.

### 12. Gün — Müşteri sorgulama

T.C. kimlik numarası, müşteri numarası, telefon ve ad-soyad kriterleriyle sorgulama geliştirildi. Kayıt bulunmadığında yeni müşteri oluşturma aksiyonu yalnızca sorgu sonucundan erişilebilir şekilde tasarlandı.

### 13. Gün — Müşteri oluşturma ve temel doğrulamalar

Kimlik, doğum tarihi, eğitim, meslek, iletişim ve finansal alanları içeren müşteri formu hazırlandı. On sekiz yaş kuralı, T.C. formatı, zorunlu alanlar ve benzersiz telefon/e-posta kontrolleri backend’de de uygulandı.

### 14. Gün — Uluslararası telefon ve iletişim doğrulama

Türkiye, İngiltere, ABD ve Almanya ülke kodları ile ülkeye özel telefon formatları eklendi. Telefon ve e-posta doğrulama kodları, süre aşımı ve yeniden gönderme akışları geliştirildi.

### 15. Gün — Yapılandırılmış adres yönetimi

İl–ilçe–mahalle bağımlı seçimleri oluşturuldu; üst seçim yapılmadan alt seçimlerin açılması engellendi. Açık adresin seçilen parçalardan üretilmesi, adres isimlendirme, kaydetme, varsayılan adres ve çoklu adres yapısı eklendi.

### 16. Gün — Finansal bilgiler ve diğer banka kartları

Diğer bankalardaki kartlar ayrı satırlar hâlinde kaydedildi ve toplam limit otomatik hesaplandı. Negatif tutar, başlangıçtaki gereksiz sıfır ve eksik finansal veri problemleri giderildi.

### 17. Gün — Kredi skoru ve KKB simülasyonu

Gerçek KKB/Findeks entegrasyonu yerine sunumda açıkça simülasyon olduğu belirtilen deterministik risk analizi geliştirildi. Risk kategorileri, puan açıklaması ve gerçek bankacılık entegrasyonu sınırları tanımlandı.

### 18. Gün — Müşteri detay ve durum yönetimi

Kimlik, iletişim, adres, finansal bilgiler, kartlar ve başvuru geçmişi tek detay akışında birleştirildi. Aktif/pasif müşteri durumu ve eksik bilgi varken işlem yaptırmama kuralları eklendi.

### 19. Gün — Kart ürün kataloğu ve limit hesaplama

Classic, Gold, Platinum ve Premium kart tipleri ürün kataloğunda tanımlandı. Gelirin üç katı, diğer banka limitleri ve bankadaki mevcut kart limitleri dikkate alınarak kullanılabilir azami limit hesaplandı.

### 20. Gün — Yeni kart başvuru sihirbazı

Müşteri seçimi, kart ve limit tercihleri, teslimat, ekstre ve onay adımlarından oluşan başvuru akışı geliştirildi. Eksik veya pasif müşteri seçildiğinde başvurunun ilerlemesi engellendi.

### 21. Gün — Teslimat ve kart kullanım tercihleri

Kayıtlı adres, farklı adres ve şubeden teslim seçenekleri geliştirildi. Şube tesliminde il ve ilçe üzerinden uygun şube seçimi; farklı adreste yapılandırılmış adres kaydetme desteği eklendi.

### 22. Gün — Ekstre ve hesap kesim tercihleri

E-posta, basılı ekstre ve mobil bildirim tercihleri; hesap kesim günü ve tahmini son ödeme tarihi alanları eklendi. Temassız kullanım, internet alışverişi ve düzenli limit artışı tercihleri kaydedildi.

### 23. Gün — Yinelenen başvuru ve sıralı değerlendirme

Aynı müşterinin aynı kart tipine kısa sürede yaptığı başvurular kontrol edildi. Aynı müşterinin birden fazla açık başvurusu varsa önceki başvuru sonuçlanmadan sonrakinin değerlendirmeye alınmaması kuralı oluşturuldu.

### 24. Gün — Başvuru listeleri ve detay ekranı

Memurun kendi başvuruları ve müdürün tüm başvuruları filtrelenebilir listelere dönüştürüldü. Durumlar Türkçeleştirildi; incele, revize et ve oluşturulan kartı görüntüle işlemleri doğru role göre gösterildi.

### 25. Gün — Müdür karar mekanizması

Onay, ret ve revizyon kararları tek değerlendirme penceresinde sekmeli yapıya taşındı. Ret ve revizyon için hazır neden listesi ile serbest açıklama desteği oluşturuldu; karar yetkisi yalnızca müdüre verildi.

### 26. Gün — Revizyon ve yeniden gönderim

Müdür revizyon notunun memur tarafından görülmesi, izin verilen alanların güncellenmesi ve başvurunun yeniden gönderilmesi sağlandı. Önceki ve yeni değerlerin yan yana karşılaştırıldığı revizyon ekranı geliştirildi.

### 27. Gün — Açıklanabilir ön değerlendirme

Gelir, yaş, çalışma durumu, kart tipi, mevcut kart sayısı, eksik bilgi ve talep edilen limit ilişkisine göre 0–100 karar destek puanı üretildi. Sonucun otomatik onay olmadığı ve nihai kararın müdüre ait olduğu açıkça belirtildi.

### 28. Gün — Kredi kartı oluşturma ve kart detayı

Onaylanan başvurudan maskeli numaraya sahip kredi kartı üretildi. Kart tipi, limit, durum, hesap kesim bilgisi, kullanım tercihleri ve teslimat detayları kart ekranında birleştirildi; tam PAN ve CVV saklanmadı.

### 29. Gün — Üretim, basım ve teslim döngüsü

Kartın onay, üretim, basım, kargo ve teslim aşamalarını otomatik tarih kurallarıyla ilerleten fulfillment yapısı oluşturuldu. Tahmini basım ve teslim tarihleri hem personel hem müşteri görünümünde sunuldu.

### 30. Gün — PDF ve e-posta çıktıları

Başvuru ve kart bilgileri için görsel PDF şablonları oluşturuldu. Kart görselinin PDF’e eklenmesi, belge özeti ve dosya bütünlük hash’i geliştirildi; e-posta gönderimi geliştirme outbox’ıyla simüle edildi.

### 31. Gün — Ek kart başvuruları

Ek kartın ana karttan başlatılması, ana kart limitini paylaşması ve farklı bir müşteri adına oluşturulması sağlandı. Kişinin kendisine ek kart çıkarması engellendi; yakınlık, yaş ve limit kuralları eklendi.

### 32. Gün — Limit artırma ve azaltma

Limit artırımı müdür onayına gönderilen talep olarak; limit azaltımı ise iş kurallarına uyuyorsa otomatik uygulanan işlem olarak tasarlandı. Ek kartlara ayrılan limitin altına düşme engeli eklendi.

### 33. Gün — SLA ve iş kuyruğu

Başvuruların bekleme süresi normal, dikkat ve gecikmiş kategorilerine ayrıldı. Memur iş kuyruğu, müdür genel görünümü, başvuru sahiplenme, atama ve otomatik atama işlemleri geliştirildi.

### 34. Gün — Bildirim merkezi

Kullanıcıya özel, okundu/okunmadı ve ilgili kayda yönlendiren bildirim yapısı oluşturuldu. Yeni başvuru, revizyon, karar, SLA gecikmesi, ek kart ve limit işlemleri için bildirim üretildi; tümünü okundu işaretleme düzeltildi.

### 35. Gün — Personel mesajlaşması

Memur ve müdür arasında bireysel, başvuru bağlantılı, toplu ve CC destekli mesajlaşma geliştirildi. Boş mesaj ve uygunsuz ifade kontrolleri hem arayüzde hem API’de uygulandı.

### 36. Gün — Profil, güvenlik ve audit görünümü

Personel profili, kurumsal bilgiler, güvenlik özeti, giriş geçmişi, son işlemler, yetkiler ve bildirim tercihleri oluşturuldu. Değiştirilebilir ve sistem yöneticisine ait alanlar ayrıldı.

### 37. Gün — KVKK ve açık rıza takibi

Aydınlatma metni, SMS ve e-posta izinleri sürüm, kanal, tarih ve işlemi alan personel bilgisiyle kaydedildi. İzin geri çekildiğinde geçmiş silinmeden yeni durumun izlenmesi sağlandı.

### 38. Gün — Başvuru simülasyonu ve müşteri portalı

Gerçek kayıt oluşturmadan kart ve limit önerisi veren başvuru simülasyonu geliştirildi. Ayrı müşteri giriş rotası, müşterinin başvuru/kart takibi ve kart aktivasyon akışının temeli oluşturuldu; portal personel girişinden ayrı tutuldu.

### 39. Gün — Veritabanı ve güvenlik denetimi

Gereksiz tekrarlar, benzersiz indeksler, yabancı anahtarlar ve tablo sorumlulukları incelendi. SQLite quick_check, foreign_key_check, WAL, dosya kilidi, geliştirme/üretim ayarı ayrımı, login rate limit ve oturum iptali eklendi.

### 40. Gün — Otomatik ve canlı API testleri

37 adet .NET iş kuralı testi başarıyla çalıştırıldı. Canlı API’de sağlık, giriş, 401/403 rol ayrımı, dashboard, müşteri, başvuru, KKB, SLA, bildirim, çıkış sonrası token iptali ve limit hesaplama kontrolleri yapıldı. Testlerin curl tabanlı olduğu tespit edildi ve Postman’a taşınması planlandı.

## Kalan 14 iş günü geliştirme planı

### 41. Gün — Postman regresyon altyapısı

**Yapılacaklar:** Local environment, memur/müdür/müşteri token değişkenleri, pozitif ve negatif API istekleri, otomatik test assertion’ları ve klasör sırası hazırlanacak. Collection Runner ile rapor alınacak.

**Çıktı:** Versiyon kontrollü Postman collection ve environment dosyaları.

**Kabul kriteri:** Health, login, 401, 403, müşteri, başvuru, KKB, SLA, bildirim, limit ve logout senaryoları tek çalıştırmada sonuç vermeli.

### 42. Gün — İzole entegrasyon test verisi

**Yapılacaklar:** Ana sunum veritabanından ayrı test veritabanı oluşturulacak. Tekrarlanabilir müşteri, kart ve başvuru fixture’ları hazırlanacak; test sonunda veri temizliği doğrulanacak.

**Çıktı:** Sunum verisini kirletmeyen entegrasyon test ortamı.

**Kabul kriteri:** Aynı test paketi arka arkaya iki kez çalıştığında çakışma veya mükerrer kayıt üretmemeli.

### 43. Gün — Müşteri yaşam döngüsü regresyonu

**Yapılacaklar:** Müşteri oluşturma, mükerrer T.C./telefon/e-posta, 18 yaş, iletişim doğrulama, adres bağımlılıkları, eksik bilgi ve pasife alma senaryoları API ve UI üzerinden test edilecek.

**Çıktı:** Müşteri modülü hata listesi ve düzeltmeleri.

**Kabul kriteri:** Geçersiz veriler 400/409 ile reddedilmeli; geçerli müşteri tek ve tutarlı kayıt oluşturmalı.

### 44. Gün — Ana kart başvuru yaşam döngüsü

**Yapılacaklar:** Başvuru oluşturma, yinelenen başvuru, iş kuyruğu, sahiplenme, müdür atama, ön değerlendirme, onay, ret ve revizyon akışları uçtan uca tamamlanacak.

**Çıktı:** Başvuru durum geçiş matrisi ve test kanıtları.

**Kabul kriteri:** İzin verilmeyen durum geçişleri engellenmeli; her geçiş history ve audit kaydı üretmeli.

### 45. Gün — Revizyon karşılaştırma ve sıralama

**Yapılacaklar:** Önceki/yeni alan karşılaştırması, değişen alan vurgusu, yeniden gönderim ve aynı müşterinin sıralı başvuru kuralı stres testine alınacak.

**Çıktı:** Revizyon karşılaştırma ekranı ve API doğrulama raporu.

**Kabul kriteri:** Sadece gerçekten değişen alanlar gösterilmeli; önceki açık başvuru bitmeden sonraki değerlendirilmemeli.

### 46. Gün — Ek kart uçtan uca test ve iyileştirme

**Yapılacaklar:** Ana kart detayından ek kart başlatma, aynı kişi engeli, ek kart sahibi arama, yakınlık, limit rezervasyonu, müdür kararı ve ana kartta ek kart listesi test edilecek.

**Çıktı:** Ek kart başvuru ve detay akışının tamamlanmış sürümü.

**Kabul kriteri:** Onaylanan ek kart ana karta bağlanmalı; toplam ek kart rezervasyonu ana kart limitini aşmamalı.

### 47. Gün — Limit artırma/azaltma yaşam döngüsü

**Yapılacaklar:** Artırım talebi, müdür onayı/reddi, otomatik azaltım, ek kart rezervasyon sınırı, bildirim ve dashboard birleşik işlem listesi test edilecek.

**Çıktı:** Limit işlemi karar matrisi ve test kayıtları.

**Kabul kriteri:** Azaltım doğru koşulda anında uygulanmalı; artırım müdür kararı olmadan karta yansımamalı.

### 48. Gün — PDF, e-posta ve outbox doğrulaması

**Yapılacaklar:** Başvuru ve kart PDF’leri farklı veri setleriyle üretilecek; görsel taşmalar, maskeleme, hash, e-posta outbox kaydı ve hata mesajları kontrol edilecek.

**Çıktı:** Sunuma uygun örnek PDF’ler ve gönderim kanıtı.

**Kabul kriteri:** PDF’de tam kart numarası/CVV bulunmamalı; gönderim sonucu kullanıcıya doğru bildirilmelidir.

### 49. Gün — Memur arayüzü kalite turu

**Yapılacaklar:** Yeni başvuru hizalaması, müşteriler, birleşik başvurular, ek kart, simülasyon, geri navigasyonu, responsive görünüm ve boş durumlar Safari/Chrome’da test edilecek.

**Çıktı:** Memur paneli UI hata listesi ve düzeltmeleri.

**Kabul kriteri:** 1280, 1440 ve geniş ekranlarda taşma/sola yığılma olmamalı; tüm ana butonlar doğru route’a gitmeli.

### 50. Gün — Müdür arayüzü kalite turu

**Yapılacaklar:** Dashboard, tüm başvurular, bekleyenler, değerlendirme penceresi, operasyon, SLA, limit ve ek kart karar ekranları test edilecek.

**Çıktı:** Müdür paneli kabul testi.

**Kabul kriteri:** Karar butonları yalnızca uygun durumda görünmeli; memur işlemleri müdür ekranında yanlışlıkla açılmamalı.

### 51. Gün — Müşteri portalı ilk düzenleme turu

**Yapılacaklar:** Personelden ayrı giriş, müşteri oturumu, başvuru/kart görünümü, aktivasyon, hata durumları ve erişim kontrolü incelenecek. Portalın V1 kapsamındaki minimum özellikleri netleştirilecek.

**Çıktı:** Müşteri portalı V1 kapsam ve hata listesi.

**Kabul kriteri:** Bir müşteri başka müşterinin kartına veya başvurusuna erişememeli; personel token’ı müşteri endpoint’inde kullanılamamalı.

### 52. Gün — Güvenlik, performans ve erişilebilirlik

**Yapılacaklar:** Rate limit, token süresi, çıkış, doğrudan URL erişimi, büyük liste performansı, klavye navigasyonu, odak görünürlüğü, form label’ları ve renk kontrastı test edilecek.

**Çıktı:** Güvenlik ve erişilebilirlik kontrol listesi.

**Kabul kriteri:** Kritik güvenlik bulgusu kalmamalı; temel akışlar yalnızca klavye ile tamamlanabilmeli.

### 53. Gün — Veritabanı son denetim ve sunum verisi

**Yapılacaklar:** Integrity/foreign key kontrolleri, mükerrer kayıt sorguları, tablo açıklamaları, yedekleme ve deterministik demo senaryosu hazırlanacak.

**Çıktı:** Temiz sunum veritabanı, yedek ve DB Browser sorgu dosyası.

**Kabul kriteri:** Sağlık sorgularındaki tüm IssueCount değerleri 0 olmalı; demo akışı her kurulumda aynı sonucu üretmeli.

### 54. Gün — Sürüm dondurma, prova ve teslim

**Yapılacaklar:** Son regression, release build, sunum provası, bilinen sınırlar, mimari anlatım, staj defteri eşleştirmesi ve geri dönüş planı tamamlanacak. Yeni özellik ekleme durdurulup yalnızca kritik hata düzeltilecek.

**Çıktı:** Sunuma hazır V1 sürümü, test özeti ve anlatım dosyaları.

**Kabul kriteri:** Temiz makinede kurulum komutları çalışmalı; memur ve müdür demo senaryoları kesintisiz tamamlanmalı; test sonuçları sunumda gösterilebilir olmalı.

## Sunumda kullanılabilecek kısa proje özeti

İlk 40 iş gününde analizden başlayarak katmanlı backend, Angular arayüz, rol bazlı güvenlik, müşteri ve başvuru yönetimi, müdür karar süreci, kart üretim/teslim takibi, ek kart, limit değişikliği, KKB karar desteği, SLA, bildirim, mesajlaşma, KVKK, PDF ve müşteri portalı temeli geliştirildi. Kalan 14 iş günü yeni özellik yoğunluğundan çok regresyon, uçtan uca doğrulama, güvenlik, kullanıcı deneyimi, veritabanı bütünlüğü ve sunum kalitesine ayrılmıştır.
