# Mainty — Sohbet Notları 10

Bu dosya iki ayrı oturumu kapsıyor: (1) Maintenance Manager prototipinin
tamamlanması (önceki oturumdan devreden, o zaman not tutulmamıştı), ve (2) bu
oturumda konuşulan shift oluşturma, database, teknoloji ve login/onay akışı
kararları.

---

## 1. Maintenance Manager Rolü — Tasarım Kararları (geçmişten devreden)

Maintenance Manager rolü, Engineer'ın bir üst kümesi olarak tasarlandı:

1. Manager, Engineer'ın gördüğü HER ŞEYİ görüyor: My Jobs, Pool, History,
   Tasks, Orders, Holiday, Create Report, Close Job — ayrı kısıtlı bir panel
   değil, tam superset.
2. Manager pool'dan iş alabiliyor (claim) ve kendisi tamir yapabiliyor —
   sadece admin/yönetim işi değil.
3. Performans/workload özeti şimdilik eklenmedi (ertelendi).
4. Manager, Pool kartı üzerinden doğrudan "Assign to Engineer" seçeneğiyle
   belirli bir mühendise iş atayabiliyor (Claim'in yanında).
5. Manager, zaten atanmış/claim edilmiş bir işi başka bir mühendise yeniden
   atayabiliyor (reassign).
6. Orders modülü: Manager kendi taleplerini + başkalarının taleplerini
   görüyor, herhangi bir talebi silebiliyor, "ordered" olarak
   işaretleyebiliyor — ekstra modül gerekmedi, mevcut Engineer Order
   mekanizması yeterli.
7. Home ekranına küçük bir "Factory Overview" özet kutusu eklendi (Production
   Manager'daki gibi).
8. **Holiday**: Manager kendi izin talebini göndermiyor — TAKIMININ
   taleplerini onaylıyor/reddediyor (Production Manager'ın holiday admin
   görünümüne benzer: Requests/Calendar sekmeleri, Approve/Reject butonları,
   aylık takvim görünümü, tıklanan talep için salt-okunur mini takvim).
9. **Factory Overview** kutuları tıklanabilir buton olarak tasarlandı — her
   biri kendi filtrelenmiş listesini açıyor:
   - **Open Jobs** — My Jobs + Pool birleşik liste, karta tıklayınca ilgili
     detay ekranına gidiyor (My Job Detail veya Pool Detail)
   - **In Pool** — sadece pool'daki işler
   - **Awaiting/Failed QA** — QA bekleyen veya reddedilen işler (History
     kartlarını kullanıyor)
   - **Closed Jobs** — kapanmış işler (History kartlarını kullanıyor)
   - **Open Tasks** — 5. kutu, aynı sırada, doğrudan Tasks ekranına
     yönlendiriyor (ayrı bir filtrelenmiş liste yok)

### Eski GitHub projesinden (`BBoyMemo/Mainty`, `MemoYnz/mainty-web-UI`) bulgular:
- Eski projede rol `EngineeringManager` olarak adlandırılmış, Engineer ile
  aynı WorkOrders erişimine sahipti (`[Authorize(Roles = "Engineer,
  EngineeringManager")]`).
- Sadece Manager (hardcoded `userId != 1` kontrolü ile) ShiftTask
  oluşturabiliyordu.
- `AssignEngineerAsync` metodu mevcuttu (belirli mühendise atama için).
- Frontend'de Manager, Engineer ile aynı menü/route'ları görüyordu. Dashboard
  sayfasında "Manager Navigation" kutusu sadece linklerden oluşuyordu,
  "(Counts/overview will be added later.)" yorumuyla — gerçek overview/liste
  mantığı hiç inşa edilmemişti.
- Eski projede Order/parça talebi kavramı hiç yoktu (yeni projede eklenen
  yeni bir konsept).
- **Sonuç**: Eski proje Manager'ı "Engineer superset" olarak ele almış ama
  yönetim eylemleri için (assign, overview, task creation) arayüz hiç
  tamamlanmamış — sadece backend kuralları vardı, çoğu eksik/yorum satırı
  halinde.

### Prototip dosyası:
`mainty_manager_prototype.html` — `mainty_engineer_prototype-1.html`'den
türetildi, persona Mike Sullivan (Maintenance Manager) olarak değiştirildi.
Yukarıdaki tüm kararlar kodlandı ve doğrulandı (div dengesi, JS syntax,
duplicate id yok, CHROME eşleşmesi tam).

---

## 2. Shift Oluşturma (Mobil) — Henüz Kodlanmadı, Tasarım Onaylandı

Kullanıcının talebi: Shift oluşturma yetkisi TÜM menajerlerde olacak (web
tarafında), ama MOBİL uygulamada sadece Maintenance Manager tarafında
görünecek.

- **Web tarafı**: Sürükle-bırak mantığıyla shift oluşturma (daha önce
  konuşulmuştu, web app aşamasına ertelenmiş durumda).
- **Mobil tarafı** (Maintenance Manager): Basit bir akış onaylandı —
  - Varsayılan olarak 3 shift tipi geliyor: sabah (06:00–14:00), öğlen
    (14:00–22:00), gece (22:00–06:00).
  - Manager bu shift'lerin saatlerini düzenleyebiliyor.
  - Yeni shift tipi ekleyebiliyor.
  - Kullanılmayan bir shift'i (örn. gece shift'i şu an aktif değilse) silmek
    yerine PASİFE alabiliyor — silme yok, sadece aktif/pasif toggle.
  - Aktif shift'ler sistemde durmaya devam ediyor, tekrar oluşturmaya gerek
    yok.
- **Durum**: Sadece konuşuldu, kullanıcı "sonra text'e geçtiğimizde yap
  dediğimde yaparsın" dedi — henüz hiçbir prototipte kodlanmadı.

> Not: Kullanıcı bu konuşma sırasında önemli bir not daha söylemek istediğini
> ama unuttuğunu belirtti. Aklına geldiğinde eklenmesi gerekiyor — takip
> edilmeli.

---

## 3. Teknoloji Kararları

- **.NET sürümü kararlaştırıldı: .NET 10.** Sebep: .NET 8 ve .NET 9, 10 Kasım
  2026'da aynı anda destek dışı kalıyor (.NET 9 zaten hiç LTS değildi). .NET
  10 güncel LTS sürümü, 3 yıl destek alıyor. Sıfırdan başlanan bir projede
  kısa ömürlü bir sürümle başlamanın anlamı yok.
- **Database**: PostgreSQL ile geliştirme/test aşaması (kullanıcının evindeki
  Ubuntu sunucuda), ileride firma isterse SQL Server'a geçiş — bu karar daha
  önceki oturumlarda alınmıştı, bu oturumda tekrar teyit edildi.
- **Login/Auth**: JWT (JSON Web Token) kullanılacak — teyit edildi.

---

## 4. Login / Kayıt Onay Akışı — Netleşen Kararlar

Önceki oturumlarda genel akış çizilmişti (register formu → pending →
manager onayı → rol + alan ataması), bu oturumda detaylandırıldı:

1. **Herkes kendi parolasını kendisi belirliyor** — hem operatör/QA gibi
   sıradan işçiler hem de manager'lar. Register formunda isim, email, telefon
   ve kendi seçtiği parola giriliyor.
2. Form gönderilince hesap oluşuyor ama **pasif/onay bekliyor** durumda
   kalıyor.
3. **Onay hiyerarşisi**:
   - Operatör, QA gibi sıradan roller → ilgili bölgedeki/departmandaki
     manager onaylıyor (rol + çalışma alanı atayarak).
   - **QA'in şu an kendine ait bir manager'ı yok** — bu yüzden QA onayını da
     **Maintenance Manager (admin)** yapıyor. İleride ayrı bir QA manager
     rolü eklenebilir, bu esnek bırakıldı (açık madde olarak not edildi,
     gerektiğinde eklenecek).
   - Yeni bir **manager** geldiğinde, onu **Maintenance Manager (admin)**
     onaylıyor — yani Maintenance Manager hem kendi ekibini onaylıyor hem de
     sistemdeki tek admin/üst onaylayıcı konumunda.
4. Onaylandıktan sonra hesap aktif oluyor, kullanıcı kendi belirlediği
   parolayla giriş yapabiliyor.
5. **Kalıcı oturum (persistent login)**: Kullanıcı bir kere giriş yaptıktan
   sonra, uygulamayı her açtığında tekrar parola girmesine gerek kalmayacak
   — JWT + refresh token mantığıyla oturum arka planda kendini yeniliyor.
   Sadece kullanıcı kendi çıkış yaparsa (logout) veya manager hesabı pasif
   hale getirirse tekrar giriş istenecek. Kullanım kolaylığı için önemli bir
   karar olarak vurgulandı.
6. Admin (Maintenance Manager) da diğer herkes gibi istediği zaman kendi
   parolasını değiştirebiliyor.

---

## 5. Database Tasarımına Geçiş — Sıradaki Adım

Kullanıcı, görsel/UI akışlarının (prototipler) tamamlandığını, sıranın
database tasarımına geldiğini belirtti. Planlanan yöntem:

- Text sohbete geçildiğinde, Claude tüm sohbet notlarını (01–10) ve mevcut
  prototipleri (`mainty_engineer_prototype-1.html`,
  `mainty_manager_prototype.html`, `mainty_qa_prototype.html`,
  `mainty_production_manager_prototype-2.html`, `mainty_operator_prototype-2.html`)
  inceleyerek bir veri modeli / tablo taslağı hazırlayacak.
- Eski `BBoyMemo/Mainty` backend projesiyle karşılaştırma yapılacak — mantıklı
  gelen kısımlar (örn. WorkOrder entity yapısı, status enum'ları) referans
  alınabilir.
- Taslak hazır olunca kullanıcıya gösterilecek, kullanıcı görsel olarak
  değerlendirip geri bildirim verecek.
- **Sıra önemli**: Önce bu oturumda konuşulan Manager prototipi + shift
  oluşturma özelliği text'e geçilince kodlanacak, ONDAN SONRA database
  tasarımına geçilecek.

---

## 6. Yeni chat için hızlı özet

- **Proje**: Mainty — County Food Ingredients (CFI, Widnes, UK) fabrika
  bakım/operasyon uygulaması.
- **Bu oturumda netleşen ama henüz KODLANMAMIŞ konular**:
  1. Mobil shift oluşturma ekranı (Maintenance Manager, basit toggle/saat
     düzenleme mantığı) — tasarım onaylandı, kod bekliyor.
  2. Database/tablo tasarımı — henüz başlanmadı, sıradaki büyük adım.
- **Bu oturumda netleşen ve zaten kodlanmış** (önceki text oturumunda):
  Manager prototipinin Factory Overview butonları (5 kutu, tıklanabilir,
  filtrelenmiş listeler) ve Holiday onay/takvim ekranı.
- **Teknoloji kararları kesinleşti**: .NET 10, PostgreSQL (test) → SQL Server
  (ileride, opsiyonel), JWT + refresh token ile kalıcı oturum.
- **Login/onay hiyerarşisi netleşti**: Herkes kendi parolasını belirliyor,
  onay şart. Operatör/QA → ilgili manager onaylıyor (QA şimdilik
  Maintenance Manager onaylıyor). Yeni manager → Maintenance Manager
  (admin) onaylıyor.
- **Kullanıcının unuttuğu bir not var** — önemli olduğunu söyledi ama
  hatırlayamadı. Aklına geldiğinde eklenmeli, bir sonraki oturumda
  hatırlatılabilir.
- **Yükleme hatırlatması**: `mainty_manager_prototype.html` ve bu not dosyası
  (`Mainty_Sohbet_Notlari_10.md`) kullanıcı tarafından proje bilgi tabanına
  henüz yüklenmedi — bir sonraki oturumdan önce yüklenmesi gerekiyor.

### Hâlâ açık olan konular (eskiden devreden):
- FLT Driver rolü detayları — kullanıcı kendisi açana kadar gündeme
  getirilmemeli.
- "Fold'luklar" konusu — anlamı hâlâ netleşmedi, kullanıcı kendisi açana
  kadar sorulmamalı.
- BRC Food Certificate puanlama/uyumluluk detayları — henüz derinlemesine
  işlenmedi.
- Android tablet arayüzü (ortak saha cihazı) — proje sonuna ertelendi.
- Production Manager WEB tarafı (shift sürükle-bırak) — web app aşamasına
  ertelendi.
- QA'e ayrı bir manager rolü eklenmesi — ileride, şimdilik admin (Maintenance
  Manager) onaylıyor.

---

*Bu dosya, konuşmanın devamı sırasında yeni bölümler ile genişletilecektir.*
