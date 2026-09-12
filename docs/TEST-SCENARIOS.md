# KKBYS Uçtan Uca Test Senaryoları

Bu belge geliştirme ve sunum ortamında kullanılacak hesapları ve kabul testlerini tanımlar. Hesaplar yalnızca yerel prototip veritabanında oluşturulur.

## Test kullanıcıları

| Rol | Sicil | Şifre | Kullanım |
|---|---|---|---|
| Memur | KBP000001 | DemoLogin01! | Ana memur akışı |
| Müdür | KBP000002 | DemoLogin01! | Ana değerlendirme akışı |
| Memur | KBP000003 | DemoLogin01! | İkinci memur ve mesajlaşma |
| Müdür | KBP000004 | DemoLogin01! | İkinci müdür ve toplu mesaj |
| Şube operasyon memuru | KBP000005 | DemoLogin01! | Şube teslim ve ekip iletişimi |

## Kabul senaryoları

1. Geçersiz TC, hatalı telefon, mükerrer telefon/e-posta ve 18 yaş altı müşteri kaydı reddedilir.
2. İl seçilmeden ilçe, ilçe seçilmeden mahalle açılamaz; açık adres seçimlerden oluşturulur.
3. Telefon ve e-posta için ayrı 6 haneli kod üretilir, 5 dakika sonra kod geçersiz olur ve yeniden gönderilebilir.
4. Eksik veya pasif müşteri için kart başvurusu açılamaz; pasife alma açık başvuru varken engellenir.
5. Aynı müşteri aynı kart tipine 15 gün içinde tekrar başvurduğunda uyarı/engel görülür.
6. Ürün minimumunun altındaki limit reddedilir; hesaplanan azami limitin üzerindeki talep risk uyarısı üretir.
7. Başvuru revizyona gönderilir, memur alanları günceller ve önceki/yeni değerler müdür ekranında karşılaştırılır.
8. Onaylanan başvurudan kart oluşur; kart durumu, basım/üretim/teslim akışı ve tahmini tarihler görünür.
9. Ana karttan ek kart başvurusu oluşturulur; aynı kişi kendisine ek kart çıkaramaz, yaş–yakınlık ve limit kuralları kontrol edilir.
10. Ek kart ana kart detayında listelenir ve ek kart başvuru detayına gidilebilir.
11. Limit azaltımı otomatik uygulanır ve sorumlu memura bildirim düşer; limit artırımı müdür kuyruğuna gider.
12. Başvuru ve kart PDF’leri indirilir, yalnızca belge şablonu yazdırılır ve e-posta sonucu ekranda gösterilir.
13. Tüm bildirimleri okundu işaretle sayacı ve okunmadı stillerini anında temizler.
14. Bireysel, toplu ve CC mesajları kaydedilir; uygunsuz ifade sunucu ve arayüz tarafından engellenir.
15. Memur ve müdür yetkileri çapraz denenir; rol dışı rotalar ve API işlemleri 403 ile engellenir.
