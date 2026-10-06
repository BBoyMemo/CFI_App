# Mainty — Sohbet Notları (Bölüm 6)

## 0. Bu dosyanın amacı

Bu dosya `Mainty_Sohbet_Notlari_01.md` – `05.md` dosyalarının **devamıdır**,
onların yerine geçmez. Yeni bir chat açıldığında proje içindeki tüm
`Mainty_Sohbet_Notlari_XX.md` dosyaları sırayla okunmalı.

Bu bölüm kısa: önceki oturum, kullanıcının "yeni sohbete geçeceğim" demesiyle
kapandı. Bölüm 5'te işlenen her şey (form analizi, BRC araştırması, Holiday
modülü, açık sorular, "fold'luklar", sırada ne var listesi) hâlâ **geçerli ve
değişmedi** — burada tekrar edilmiyor, sadece bu oturumda **yeni** eklenen tek
konu var: **uygulamanın fiyatlandırılması araştırması.**

Güncel prototip dosyası hâlâ: `mainty_operator_prototype.html` (1410 satır,
Holiday modülü dahil). Bu oturumda prototipe **kod değişikliği yapılmadı** —
sadece konuşma/araştırma oldu.

---

## 1. Bu oturumda yapılan — Uygulama Fiyatlandırma Araştırması

Kullanıcı sordu: "Böyle bir aplikasyon için kaç para alınabilir?" Web
araştırması yapıldı (CMMS yazılım fiyatlandırması, İngiltere'de özel/bespoke
yazılım geliştirme maliyetleri, freelance geliştirici günlük ücretleri, gıda
güvenliği/BRC uyumluluk yazılımı fiyatlandırması). Sonuç üç senaryo halinde
sunuldu, sonra kullanıcının isteğiyle sesli/konuşma diliyle tekrar okundu.

### 1.1 Senaryo A — CFI'a tek seferlik proje olarak satmak (bespoke)

- İngiltere'de bağımsız geliştirici günlük ücreti: ortalama **£430–500/gün**
  (medyan), ajanslar **£600–1200/gün** (blended rate).
- Mainty'nin şu anki kapsamı (breakdown raporlama, calendar/shifts/
  notifications, Holiday modülü, + ileride Fault Log + manager/engineer
  ekranları) orta-küçük ölçekli bir iş uygulaması sayılır.
- Tahmini proje fiyatı: **£15,000–£40,000**. Kapsam büyürse (örn. bordro/HR
  entegrasyonu) üst sınıra doğru çıkar.

### 1.2 Senaryo B — Aylık abonelik/destek ücreti (tek client — CFI)

- Küçük fabrikalara özel niş CMMS + gıda güvenliği uyumluluk yazılımları
  genelde **£150–500/ay** sabit ücretle satılıyor (örnek: BRC odaklı bir
  rakip ürün $250/ay ≈ £200/ay flat).
- Kullanıcı başına ücretlendirme de yaygın ($20–99/kullanıcı/ay), ama küçük
  fabrikalarda sabit fiyat modeli genelde tercih ediliyor.

### 1.3 Senaryo C — Ürünleştirip başka gıda fabrikalarına satmak (SaaS)

- Genel CMMS pazarı: $20–150/kullanıcı/ay, ya da 20 kişilik bir bakım ekibi
  için yıllık $25,000–80,000 civarı.
- Mainty'nin farkı: CMMS (bakım yönetimi) + gıda güvenliği/BRC uyumluluk imza
  akışını **birleştirmesi** — bu ikisini ayrı ayrı sunan çok ürün var, birlikte
  sunan az. Niş bir avantaj olabilir.
- Ama ürünleştirme (çoklu-fabrika desteği, satış, destek altyapısı) çok daha
  büyük ve ayrı bir yatırım kararı.

### 1.4 Verilen pratik öneri

Şu an tek client (CFI) için pilot aşamasında olunduğu için en gerçekçi
yakın-vadeli yol ya **tek seferlik bespoke proje ücreti** ya da **küçük aylık
abonelik/destek ücreti**. Ürünleştirip diğer gıda üreticilerine satmak asıl
büyük potansiyel ama ayrı, daha büyük bir yatırım kararı olarak bırakıldı —
şu an için karar verilmedi, sadece bilgi sunuldu.

*(Not: Bu bir fiyatlandırma tavsiyesi değil, pazar araştırması özetidir —
kullanıcı kendi kararını verecek.)*

---

## 2. Genel durum özeti (yeni chat'in hızlıca yakalaması için)

- **Proje**: Mainty — County Food Ingredients (CFI, Widnes/Dennis Rd, UK) için
  fabrika bakım/operatör uygulaması.
- **Prototip**: `mainty_operator_prototype.html`, 1410 satır. Modüller: home,
  breakdown reporting, calendar/shifts/notifications, Holiday Request
  (bu chat oturumunda değil, önceki oturumda eklendi — bkz. Bölüm 5).
- **Bekleyen/bloklu iş**: Fault Reporting Log modülü — 3 açık soru netleşmeden
  tasarlanmayacak (bkz. Bölüm 5, madde 2.3).
- **Reddedilen görev**: Site plan diyagramı — tekrar başlatılmadan önce mutlaka
  ne yanlış olduğu sorulmalı.
- **Açılmayı bekleyen konu**: "Fold'luklar" — kullanıcı kendisi açana kadar
  gündeme getirilmeyecek.
- **Bu oturumda yeni eklenen**: Sadece fiyatlandırma araştırması (yukarıda,
  Bölüm 1) — prototipe kod değişikliği yok.

## 3. Sırada ne var (Bölüm 5'teki liste hâlâ geçerli, değişmedi)

1. Fault Reporting Log modülü tasarımı (3 açık soru).
2. Yönetici tarafı Holiday takvim görünümü.
3. "Fold'luklar" konusu — kullanıcı açtığında.
4. Mühendis tarafının ekran akışı hâlâ detaylandırılmadı.
5. FLT Driver rolü detayları hâlâ konuşulmadı.
6. İmza akışı: tek seferlik kayıtlı dijital imza mı, her kapanışta yeniden mi.
7. *(Yeni)* Fiyatlandırma kararı — kullanıcı hangi senaryoya (A/B/C) yöneleceğine
   henüz karar vermedi, sadece bilgi topladı.

---

*Yeni sohbet açıldığında: önce Bölüm 1–6 (01–05 + bu dosya) sırayla okunmalı,
sonra kullanıcıya nereden devam etmek istediği sorulabilir.*
