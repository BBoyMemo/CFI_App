# CFI App — Kesinleşmiş Kararlar

## ⚠️ Kalite seviyesi — en üstteki kural

**Bu proje MVP / prototip / deneme değil. Doğrudan ÜRETİM (production) uygulamasıdır.**

Gerçek fabrikada, gerçek çalışanlarla, maaşı etkileyen mesai kayıtlarıyla ve BRC denetimine
giren bakım kayıtlarıyla çalışacak. Bunun kod tarafındaki karşılığı:

- Simülasyon / mock veri yok (prototiplerdeki "4.5 saniye sonra otomatik Approved olur" gibi şeyler koda geçmez)
- Yer tutucu ekran yok — hazır olmayan modül menüde görünmez
- Test, yetki testi, sayfalama, doğrulama ve hata durumları **fazın parçasıdır**, sonraya bırakılmaz
- Her faz sonunda Opus ile `/code-review`; kritik fazlarda `/security-review`
- Tam liste: `docs/ROADMAP.md` → "Üretim standardı — Definition of Done"

> Kaynak (depoda tutulmayan müşteri belgeleri): `Mainty_Sohbet_Notlari_01–10.md`, `CFIA_Mainty_Future_Vision_and_Requirements.md`,
> `Mainty_Project_Handoff.md` ve 5 HTML prototip.
> **Bu dosya o kaynakların özetidir — yeni oturumlarda önce burası okunur, orijinal dosyalar sadece gerekirse açılır.**

---

## Teknoloji

| Konu | Karar |
|---|---|
| Backend | .NET 10 (LTS, 3 yıl destek). .NET 8/9 Kasım 2026'da düşüyor, o yüzden seçilmedi |
| ORM | EF Core 10 |
| Veritabanı | PostgreSQL (dev/test, kullanıcının Ubuntu sunucusu). Firma isterse ileride SQL Server |
| Auth | JWT access token + rotating refresh token → **kalıcı oturum** (kullanıcı logout etmedikçe veya hesap pasife alınmadıkça tekrar parola sorulmaz) |
| Web | React + Vite, **JavaScript** (TypeScript değil — kullanıcı tercihi, eski projeyle tutarlı) |
| Web CSS | **Tailwind + kendi design token'larımız** (hazır bileşen kütüphanesi yok; prototiplerdeki palet birebir korunur) |
| Mobil | React Native (Android önce, sonra iOS), JavaScript |
| Repo | **Tek repo (monorepo)**: `backend/` + `web/` + `mobile/` aynı git geçmişinde. Eski projedeki ayrı repo (Mainty + mainty-web-UI) senkron sorunu tekrarlanmayacak |
| Push | Firebase Cloud Messaging (ücretsiz) |
| Konum | Cihazın kendi GPS'i — ücretli harita API'si yok |
| Bütçe ilkesi | Mümkün olan her yerde ücretsiz teknoloji |

---

## İsimlendirme

- Firma kısaltması **CFI** (CFIA değil — eski dosya adlarındaki "CFIA" hatalı).
- Uygulama adı: **CFI App**.
- Kod, değişken, yorum ve UI metinleri **İngilizce**. Dokümanlar Türkçe.

---

## Veri modeli kararları (2026-09-04)

- **Öncelik/önem derecesi: 4 seviye** — `Low`, `Medium`, `High`, `Critical` (eski Mainty'deki `Priority` enum'ı). Alan adı `Priority`.
- **Departmanlar: şimdilik sadece `Production` ve `Maintenance`.** Uygulamadaki her referans veri CRUD olacak, kalanlar admin panelinden eklenecek.
- **Yerleşim hiyerarşisi: `Area → Unit → Equipment`.** En üst seviye **Area** (resmi Fault Reporting Log formundaki "Area" alanı), onun içindeki oda/bölümler **Unit**. `Line` de Area'ya bağlı.
- **Seed verisi: sadece Area seviyesi** — `Unit 1`, `Unit 2`, `Unit 3`, `Yard`. Oda/ekipman listesi seed'e girmiyor, admin panelinden girilecek.
- **Kullanıcı başına tek rol.** Maintenance Manager'ın Engineer'ı kapsaması, rolüne Engineer permission'larının verilmesiyle çözülür.
- **Seed asla üzerine yazmaz** — eksik olanı ekler. Yönetici `Unit 1`'in adını değiştirdiyse bir sonraki deploy onu geri almaz.

## Roller

Mevcut roller: `Operator`, `Engineer`, `MaintenanceManager`, `QA`, `ProductionManager`.
(Eski projedeki `EngineeringManager` = yeni `MaintenanceManager`.)

- **Occupation** kavramı ayrıca var (Packer, FLT Driver …) ve admin panelden eklenebilir.
- **Maintenance Manager = Engineer'ın tam superset'i.** Manager mühendisin gördüğü her şeyi görür, pool'dan iş alabilir, kendisi tamir yapabilir. Ek olarak: assign/reassign, task oluşturma, tüm order'ları yönetme, ekibinin holiday onayı, Factory Overview.
- **Manager holiday talebi göndermez** — ekibinin taleplerini onaylar.
- **Görünürlük ilkesi:** her manager yalnızca sorumlu olduğu department/area'yı görür, tüm fabrikayı değil.
- **FLT Driver'lar merkezi bir departmanda toplanmaz** — her department'ın kendi FLT driver'ları olur. Detaylar ertelendi (kullanıcı açana kadar gündeme getirilmeyecek).
- Yetkilendirme **rol string'i ile değil, permission ile** yapılacak.

---

## Kayıt / onay akışı

1. Herkes kendi parolasını kendisi belirler (işçi de manager da). Form: isim, email, telefon, parola.
2. Hesap **Pending** olarak oluşur.
3. Onaylayan:
   - Operator / diğer sıradan roller → ilgili department manager'ı
   - **QA → şimdilik Maintenance Manager** (QA'in kendi manager'ı yok; ileride ayrı QA manager rolü eklenebilir)
   - **Yeni manager → Maintenance Manager (admin)**
4. Onay sırasında **occupation + department + çalışma alanı (oda/area)** atanır. Atama makine bazında değil, **area bazında**.
5. Onaydan sonra hesap **Active**, kullanıcı giriş yapabilir.
6. Admin dahil herkes kendi parolasını istediği zaman değiştirebilir.

---

## Maintenance — breakdown akışı

**Rapor oluşturma**

- **Operator:** Line seç → o hattın parçaları ikon grid olarak listelenir → parça seç → açıklama + **sınırsız foto** + severity → gönder.
- **Engineer:** Unit seç (Unit 1/2/3, Yard) → o unit'in önceden tanımlı ekipman/oda listesi → seç, **veya "Other" ile serbest metin** → aynı detay ekranı.
- **Manager ve QA:** line/part seçimi yok, doğrudan **General Report**.
- Severity: arızanın üretimi tamamen durdurup durdurmadığı işaretlenir (tam seçenek listesi netleşmedi).

**Atama**

- Her rapor **Pool'a** düşer. Mühendisin kendi oluşturduğu rapor bile otomatik kendisine atanmaz.
- Mühendis pool'dan işi kendisi alır (claim). **Aynı anda birden fazla aktif iş alabilir.**
- Manager pool'u bypass edip doğrudan bir mühendise atayabilir, atanmış işi başka mühendise devredebilir.
- Bir mühendis meşgulken o iş kilitlenmez; başka mühendis alabilir.
- Operatör hangi mühendisin boşta olduğunu **göremez**.

**Bildirimler (mühendis → operatör)**

- "I'm Busy" — şu an gelemiyorum, ilk fırsatta geleceğim.
- "On My Way" — şimdi geliyorum.
- Metinler İngilizce ve sabit.

**İşçilik süresi**

- **Tamamen otomatik.** Claim anından kapanışa kadar sistem hesaplar. Mühendis elle saat girmez.

**Parça bekleme**

- Mühendis "waiting for parts" derse iş kapanmış sayılır (sebebi net kayıtlı).
- Operatöre **tek seferlik** bildirim gider, sürekli takip ettirilmez.
- Production Manager ve Maintenance Manager parça bekleyen tüm işleri **kalıcı listede** görür.

**Close Job formu (resmi Fault Reporting Log ile birebir)**

- Root Cause (serbest metin)
- Corrective Action Taken (serbest metin)
- Able to Repair? Yes/No — No ise gerekçe alanı açılır
- Contractor Required? Yes/No — Yes ise **tek serbest metin kutusu** açılır (ayrı "Contractor Used" sorusu yok)
- Downtime (dakika)
- Tools & Parts Accounted For? → "hepsi sayıldı" veya "eksik var" (+açıklama). Her iki durumda da **opsiyonel foto**
- **Intervention into process stream post-deodorisation? Yes/No**
- Closing Photos
- Maliyet alanları: Parts Required, Price of Parts, Labour Hrs (otomatik), Labour Cost/Hr, PO Number

**QA döngüsü**

- Post-deodorisation = **Yes** → iş **QA swab testine** düşer. (Bu soru ayrı bir kontrol değil, QA swab testinin ta kendisidir.)
- Bu kontrolün ne zaman gerekli olduğuna dair sabit kural yok — **mühendisin muhakemesine bırakıldı**.
- QA **Pass** → iş kapanır. QA **Fail** (+**zorunlu not**) → iş mühendise geri döner.
- Mühendis **aynı formu** açar (önceki değerler dolu gelir, QA notu kırmızı kutuda görünür), düzeltir, **yeniden gönderir**. Yeni kayıt oluşturulmaz, mevcut kayıt güncellenir. Pass gelene kadar tekrarlanabilir.

**İmza**

- **Tek seferlik kayıtlı parmak imzası.** Kullanıcı her kapanışta yeniden imza çizmez; istediğinde imzasını değiştirebilir.
- Ayrıca **Production Management Sign Off** (Area/Item clean & tidy?, Released back into service?) ve **intrusive işlerde QA Sign Off** var.

**History**

- Kapanmış tüm işler (kendi + diğer mühendislerinki).
- Ay-yıl gruplu liste, sekmeler: This Month / This Year / All Time.
- Arama: unit, ekipman adı, WO numarası. Arama yapılınca tarih filtresi devre dışı kalır, tüm zamanlarda aranır.
- **Breakdowns / Tasks** ayrı sekmeler.
- Fotoğraflar kayıtta saklanır ve detayda gösterilir; liste kartında kamera ikonu + adet.
- Rozetler: "Awaiting QA", "QA Failed" + "Fix & Resubmit" butonu.

---

## Tasks

- **Sadece Maintenance Manager** task oluşturur/atar. Mühendis kendine task giremez.
- Bir task **birden fazla mühendise** atanabilir. Manager silebilir, atanan mühendisi değiştirebilir.
- Türler: **Daily / Weekend / Manager-assigned**. Daily ve Weekend aynı ekranda ama ayrı gruplarda.
- Her task'ın bir tarihi var.
- Opsiyonel öncelik: **Reactive / Corrective / Preventive** (seçilmezse etiketsiz).
- Tamamlama: kısa açıklama + opsiyonel foto + **Done**. Manager'a bildirim gitmez.
- Görünürlük: açık task'ı sadece atanan mühendis görür; **tamamlanmış task'ı herkes görür**.

## Orders (parça talebi)

- Mühendis talep oluşturur: parça adı, miktar, acil mi.
- Mühendis **sadece kendi** taleplerini görür/oluşturur/siler.
- Manager **tüm** talepleri görür, herhangi birini silebilir, "ordered" işaretleyebilir.
- Oluşturma ve sipariş verilme tarihleri kaydedilir. En yeni üstte. Ordered olunca bekleyen listeden düşer.
- **Bildirim yok** — herkes kendi listesine bakar.

---

## Attendance (Clock In/Out)

- Hem **otomatik** hem **manuel**, ikisi birlikte.
- Otomatik: **GPS geofence** — fabrikanın koordinatına belirli yarıçapta girince Clock In. Yarıçap netleşmedi (~500 m konuşuldu, daha az olabilir).
- Kısa sinyal kopması **Clock Out sayılmaz** — tolerans mantığı olacak (süre netleşmedi).
- Manuel: GPS/bağlantı sorununda kullanıcı butonla kendi yapar.
- **Tek gerçek zaman damgası** — "planlanan shift saati vs gerçek saat" ayrımı YOK.
- **Overtime ayrı ekran:** çalışan kendi beyan eder (yarım saat, bir saat …), gerçek clock kayıtlarıyla çapraz kontrol edilebilir.
- Çalışan kendi aylık saatlerini görür (maaş aylık), takvimden geçmiş aylara bakabilir.
- Manager sadece kendi department/area'sındaki çalışanların saatlerini görür.
- Offline: yerel kaydedilir, bağlantı gelince senkronize edilir. Veri kaybolmaz.

## Holiday

- Çalışan: Start Date + End Date → **iş günü sayısı otomatik hesaplanır** (hafta sonu hariç) → gönder.
- Durumlar: Pending (sarı) / Approved (yeşil) / Rejected (kırmızı). Eski talepler listede kalır.
- Form üzerinde Name/Department alanı **yok** — profilden bilinen bilgi tekrar sorulmaz.
- Ayrı imza kutusu yok: **Submit = çalışanın imzası**, **Approve/Reject = manager'ın imzası**.
- Manager tarafı: **Requests / Calendar** sekmeleri. Liste ana görünüm, onay/red listede yapılır. Talebe tıklayınca salt-okunur mini takvim. Ayrı tam takvimde ay ay gezinme; onaylı yeşil, bekleyen sarı; güne tıklayınca o gün izinli kişiler.
- Departman bazlı görünürlük.

---

## Shift

- Varsayılan 3 shift: **06:00–14:00 / 14:00–22:00 / 22:00–06:00**.
- Manager saatleri düzenleyebilir, yeni shift tipi ekleyebilir.
- **Silme yok** — kullanılmayan shift **pasife alınır**, aktif shift'ler sistemde kalır.
- Web: **drag & drop** planner (çalışanı tut, shift'e bırak). Haftalar/aylar ileri planlanabilir.
- Mobil: basit touch akışı (tarih → shift → çalışan).
- Shift oluşturma yetkisi web'de tüm manager'larda; **mobilde şimdilik sadece Maintenance Manager'da**.
- Çalışan kendi shift'lerini görür (bugün + sonraki).

---

## Messages / Notifications

- Manager mesaj gönderir. Hedef: tek çalışan / birden çok çalışan / **Department**.
- **Message = sistemde kalan içerik**, **Notification = telefondaki uyarı**. İkisi ayrı.
- Kullanıcı uygulamayı açınca mesaj uygulama içinde de duruyor olmalı.
- İleride: Inbox, okundu/okunmadı, priority, geçmiş.

---

## BRC Food Certificate

- CFI her yıl **BRCGS Food Safety** denetiminden geçiyor.
- Araştırma sonucu: **dijital sistem için ayrı bir onay/sertifika gerekmiyor.** Önemli olan üç şey:
  1. **İzlenebilirlik** — kim, ne zaman, ne yaptı; root cause ve corrective action aranabilir kayıtlı
  2. **Onay/imza akışı** — Production ve (gerekirse) QA onayı kim tarafından ne zaman verildi, dijitalde de görünür
  3. **Denetçiye anında gösterilebilirlik** — belirli tarih/olay saniyeler içinde bulunabilmeli
- Fallback olarak tamamlanmış form kağıda basılabilmeli (PDF/print).

---

## Tasarım ilkeleri (proje geneli, kullanıcı tarafından defalarca vurgulandı)

- **Sadelik**: az buton, az karmaşa, ama yeterli bilgi.
- **Az geçiş, az yazı, çok ikon/görsel.** Geçiş gerekiyorsa adım adım ve sade olsun.
- Operatör uygulamayı açınca **doğrudan breakdown raporlama ekranı** gelir. Clock In/Out hamburger menüde.
- Ana ekranda Clock durumu **sadece metin** ("Clocked In"/"Clocked Out"), gerçek buton **menü panelinin header'ında**.
- Mühendis ana ekranı: **My Active Jobs** (üstte, yeşil) + **Sent Back By QA** (kırmızı) + **Create Report** + **Pool** (altta) — sekme yok, tek ekranda.
- Manager ana ekranı ek olarak **Factory Overview**: Open Jobs / In Pool / Awaiting-Failed QA / Closed Jobs / Open Tasks — 5 tıklanabilir kutu, her biri filtrelenmiş liste açar.
- Renk paleti: kahve `#6B4A2E`, yeşil `#4E7A3B`, sarı `#E7A93A`, krem/beyaz zemin `#F5EFE2` / `#FFFFFF`. Sarı = ana aksiyon, yeşil = olumlu/tamamlandı, kahve = başlık/kritik.
- Diller: **EN (varsayılan), PL, BG, ES** — tüm arayüz, parça isimleri, takvim dahil.
- **Dil seçici TÜM rollerde bulunur** — sadece operatörde değil. Engineer, QA, Production Manager, Maintenance Manager dahil bir manager altında çalışan herkes kendi dilini seçebilir. Seçim kullanıcı profiline kaydedilir: cihaz değişse de korunur, web'de seçilen dil mobilde de gelir.
- **Prototipler piksel bazında bağlayıcı değil** — akış ve genel yerleşim referansıdır.

---

## Çalışma şekli (kullanıcının tercihi)

- Adım adım ilerle, tek seferde 10 dosya değiştirme.
- Dosya değiştirmeden önce mevcut halini iste/oku, ne değiştiğini açıkla.
- İlgisiz dosyalara dokunma.
- **Varsayım yapma — sor.** Kararı kullanıcı verir.
- Gereksiz övgü yok, doğrudan açıklama.
- Kurulumlar `D:\Program Files` altına yapılır (yerel tercih).

---

## Kapalı / gündeme getirilmeyecek konular

- **Fabrika planı görseli** — kullanıcı "boş ver" dedi, kapandı. (Şematik plan müşterinin `CFI_site_plan.html` dosyasındaydı: Unit 1/2/3, Yard, gate'ler, F1–F14 odaları. O çizimden gelen oda listesi 2026-09-07'de sahanın kendi listesiyle değiştirildi.)
- **"Fold'luklar"** — anlamı netleşmedi, kullanıcı kendisi açana kadar sorulmayacak.
- **FLT Driver detayları** — "uygulamayı denedikten sonra", kullanıcı açana kadar bekleyecek.
- Onaylanmayan fikirler: QR/barkod okutma, sesli açıklama, Start/Finish Job butonları, mühendis çeklistleri, meeting room / visitor management gibi genel modüller.

---

## Eski projeden (BBoyMemo/Mainty) taşınacak / taşınmayacak

**Referans alınabilir:** WorkOrder entity yapısı, `WorkOrderStatus` / `Priority` enum mantığı, clean architecture katmanları.

**Kesinlikle taşınmayacak:** `X-UserId` header, `HttpContext.Items["CurrentUser"]`, `RequireRoles<T>()` reflection helper'ı. Bunlar eski auth modeline ait ve 401/403 karışıklığına yol açıyordu (mühendis breakdown listesine tıklayınca login'e atılıyordu).

**Eski projede eksik olup yeni projede olması gerekenler:** Able to Repair, Contractor Required/Used, Tools & Parts Accounted, post-deodorisation kontrolü, maliyet alanları (parts/price/labour/PO), Production Management Sign Off, Order (parça talebi) modülü, Task modülü, Attendance, Holiday, Shift, Messages.

---

## Rol bazlı ekranlar — 2026-09-04'te netleşen düzeltmeler

Kullanıcı prototipleri ve sohbet notlarını referans göstererek düzeltti:

**Her rolde kaldırılan**
- **Reported By Me** — hiçbir rolde ayrı ekran/menü olmayacak. Mühendis için gereksiz
  (kartta zaten rapor eden kişinin adı yazıyor), operatör/supervisor için de yok.
- **Sent Back By QA** — ayrı buton/ekran değil. QA'nın geri gönderdiği iş **My Active Jobs
  içinde kalıyor**, kartın üstünde kırmızı "QA sent this back" uyarısıyla ve listenin
  **en üstünde** sıralanıyor. Aynı şekilde QA testi bekleyen iş de listede kalıyor.

**Rol bazlı menü (uygulanan hali)**
| Ekran | Operator | Engineer | QA | Production Mgr | Maintenance Mgr |
|---|---|---|---|---|---|
| Report Breakdown | ✔ | ✔ | ✔ | ✔ | ✔ |
| My Jobs (+ QA durumları) | — | ✔ | — | — | ✔ |
| Pool | — | ✔ | — | — | ✔ |
| History | — | ✔ | — | **yok** | ✔ |
| Team | — | — | — | ✔ | ✔ |
| Pending Approvals | — | — | — | ✔ | ✔ |
| Dashboard içeriği | Report CTA | My Jobs + Pool | QA kuyruğu | **açık arızalar** | My Jobs + Pool |

Bunun için `workorder.viewAll` ikiye ayrıldı: **`workorder.history`** (arşiv arama + BRC CSV
export) yalnızca Engineer + Maintenance Manager'da. Pool artık `workorder.claim` ile
korunuyor — havuz "sahiplenme kuyruğu", sahiplenmeyen rol görmüyor.

**Report Breakdown formu**
- Unit (Unit 1/2/3/Yard) → **Area** (Filling Room, Packing…) → **Makine** kademeli dropdown. Serbest metin
  yalnızca "Other — not on the list" seçilirse çıkıyor (liste hiçbir zaman kapalı değil).
- Area'lar müşterinin `CFI_site_plan.html` çiziminden, makineler prototiplerden seed edildi. *(2026-09-07'de ikisi de sahanın verdiği gerçek listeyle değiştirildi — bu dosyanın sonuna bakın.)*
  İkisi de admin panelinden düzenlenebilir, seeder üzerine yazmıyor.
- API tarafında da doğrulanıyor: seçilen makine/area seçilen unit'e ait değilse **400**.
  (Mobil ve offline kuyruk aynı API'ye yazacak — "Yard / Filling Line" kaydı denetimde
  reddedilmiş rapordan daha kötü.)

**Onay (Pending Approvals)**
- **Maintenance / Production departman dropdown'ı kaldırıldı** — rol zaten hangi tarafta
  çalışıldığını söylüyor, iki kez sormak sadece yanlış girme yolu açıyor.
- Onay yetkisi rol bazlı sınırlandı (`Permissions.ApprovableRoles`):
  - **Maintenance Manager** → Engineer, QA, MaintenanceManager, ProductionManager
    (Operator **değil** — operatör maintenance altında değil)
  - **Production Manager** → Operator
  - Haritada olmayan rol kimseyi onaylayamaz (güvenli varsayılan).
  Rol listesi ucu da yalnızca verilebilecek rolleri döndürüyor, ekran sunucunun
  reddedeceği bir seçenek gösteremiyor.

**History araması**
Tek serbest metin kutusu tüm kayıtta arıyor: numara, area, oda, makine, açıklama, rapor
eden, işi yapan mühendis, root cause ve corrective action + tarih aralığı.

**Team**
Arama eklendi (isim/e-posta) — 80 kişilik listede sayfa çevirerek adam aramak arama değil.

---

## Site hiyerarşisi — isimlendirme düzeltmesi (2026-09-04)

Kullanıcının netleştirmesi: **Unit** = U1, U2, U3, Yard. **Area** = Filling Room, Packing
vb. (unit'in içinde). Area'nın içinde **makineler**, makinelerin içinde ileride
**makine bölgeleri**. Kod baştan tam tersi isimlendirilmişti (Area en üstte), bu yüzden
tüm kod tabanında `Area ↔ Unit` yeniden adlandırıldı (54 dosya, migration sıfırdan
yeniden üretildi, 204 test geçiyor).

> Neden isim değiştirildi de sadece etiket çevrilmedi: API'de `areaId` yazıp UI'da "Unit"
> göstermek, ileride bu kodu okuyan herkesi (ve sonraki oturumdaki beni) yanıltırdı.
> Üretim uygulamasında kodun sahadaki dili konuşması gerekiyor.

**Sonuç hiyerarşi:** `Unit → Area → Equipment` (+ ileride makine bölgesi)

**Çalışan ataması:** yalnızca **Area** seviyesine kadar. Makineye atama yok; unit zaten
area'dan çıkıyor. Onay ekranında: Unit dropdown → o unit'in area'ları checkbox, birden
fazla unit'ten area seçilebiliyor.

**Departman artık elle seçilmiyor** — roldan türetiliyor (`Permissions.RoleDepartments`):
Operator/ProductionManager → Production; Engineer/MaintenanceManager/**QA** → Maintenance.
QA'in kendi manager'ı olmadığı için (sohbet notu 10) Maintenance altında.

**Team görünürlüğü:** manager kendi departmanını görür + `ManagerScope` satırlarının
eklediklerini (başka departman, ya da bir unit'te çalışan herkes). **Artık kimse "herkesi"
görmüyor — Maintenance Manager dahil.** MM bakımı yönetir (mühendisler + QA); operatörler
Production Manager'ın listesinde. Admin olmak hesap onaylamak ve site düzenini düzenlemek
demek, herkesin mesai kaydını okumak değil.

**Öncelik:** 4 seviye (Low / Medium / High / Critical) — kullanıcı teyit etti, prototipteki
2'li ("Line Stopped" / "Minor Issue") değil.

---

## Web uygulaması tamamlandı — 2026-09-04 gecesi alınan kararlar

Kullanıcı "sonuna kadar bitir, sorularım olmadan devam et" dedi. Aşağıdakiler varsayımla
karara bağlandı; hepsi geri alınabilir, sadece söylemesi yeterli.

**Navigasyon** — 4 birincil bağlantı üst barda (Dashboard, Report, My Jobs, Pool), geri
kalan her şey tek bir **"More"** menüsünde. Prototipteki hamburger mantığı: 12 bağlantıyı
yan yana dizmek tablette bir kelime duvarı olurdu, "az buton az yazı" ilkesine aykırı.

**Admin paneli profilin altında** (kullanıcı isteği) — ana menüde yok. Site kurulumunu tek
kişi ara sıra açar; kalıcı bir menü yeri, hiç admin işi yapmayan herkesin önüne admin işi
koymak olurdu. `admin.manage` yetkisi olanın profilinde "Open site setup" butonu var.

**Web'den saat damgası her zaman Manual** (`ClockSource.Manual`). Geofence'li otomatik
kayıt cebindeki telefonun işi; ofisteki masaüstü tarayıcısının "kapıda" olduğunu iddia
etmek bordroya yanlış saat yazmak demek. Ekranda da yazıyor.

**Vardiya planlayıcı** — hafta × vardiya tablosu, isimler sürükle-bırak. Taşıma = sil+ekle
(API'de "taşı" fiili yok); bu sırayla, çünkü reddedilen bir ekleme kişiyi olduğu yerde
bırakır. Kütüphane yok, tarayıcının kendi drag & drop'u.

**Task tamamlama kişi başına** — ortak bir task'ı, payına düşeni yapan herkes kendi adına
tamamlıyor; grup adına tek seferlik değil.

**Manager kendi iznini buradan talep etmiyor** — `holiday.approve` var, `holiday.request`
bilinçli olarak yok (daha önceki karar). Holiday ekranı manager'a sadece onay tarafını
gösteriyor.

**Yeni eklenen backend ucu:** `POST /workorders/{id}/cancel`. `workorder.cancel` yetkisi ve
`Cancelled` durumu vardı ama ucu hiç yazılmamıştı — mükerrer rapor havuzdan böyle çıkıyor.
Sebep zorunlu (boş sebep 400), silme değil: kayıt sebebiyle birlikte duruyor, ve iptal
sonrası claim 409.

**Admin panelinde "Hours & leave" sekmesi** — geofence ve resmi tatiller. İkisi de boşken
sistem tahmin etmiyor: geofence yoksa otomatik clock reddediliyor, resmi tatil girilmemişse
o gün izinden düşüyor. Gerçek koordinat ve tatil listesi kullanıcıdan gelecek.

---

## 2026-09-05 — Prototip/not karşılaştırması sonrası düzeltmeler

Kullanıcı "notlar ve HTML doğrudur, kıyasla ve düzelt" dedi. Sohbet notları 01–05, 07–10
ve 5 prototip tek tek okundu, farklar aşağıda.

**Uygulama kabuğu baştan değiştirildi — en büyük uyuşmazlık buydu.**

Yaptığım: üst barda yatay menü + "More" açılır listesi. Prototiplerde olan ve şimdi
uygulanan (sohbet notu 04 §2–3, birden fazla tur sonunda kesinleşmiş):

- En üstte **durum şeridi**: bağlantı LED'i + "Factory network · connected" + saat
- Header: solda **hamburger**, ortada **"Hi, {isim}" + rol**, sağda **"Clocked in/out"
  metni** (buton değil — bu özellikle 5 turda kesinleşmiş bir karar) + dil seçici
- **Yan panel (hamburger)**: kendi header'ında **gerçek Clock In/Out butonu** (clocked out
  iken yeşil "Clock In", clocked in iken sarı "Clock Out"), altında "on site since",
  bugünkü toplam; sonra **ikonlu** menü satırları; en altta çıkış
- Dil seçici tek kompakt buton: 🇬🇧 EN / 🇵🇱 PL / 🇧🇬 BG / 🇪🇸 ES

**Eksik olan ve eklenen ekranlar**

- **Notifications** — her prototipin menüsünde var, hiç yapılmamıştı. Daha kötüsü: sistem
  Faz 3'ten beri bildirim satırı **yazıyordu** ama okunacak uç yoktu. `GET /notifications`
  + okunmamış sayacı (menüde rozet) eklendi
- **Who's In** (Production Manager) — kimin şu an sahada olduğu. `GET /attendance/who-is-in`.
  Süre **sunucuda** hesaplanıyor, cihaz saatinde değil — uygulama zaten başka hiçbir yerde
  cihaz saatine güvenmiyor. 16 saati geçen kayıt "muhtemelen çıkış unutulmuş" diye
  işaretleniyor
- **Aylık saat özeti + ay ay gezinme** — sohbet notu 02: "aylık olacak (maaş ödemesi aylık
  olduğu için)". Attendance ekranı günlük listeden aylık zaman çizelgesine çevrildi
- **Departman ve Occupation CRUD** — sohbet notu 01 §5 admin panelinde olacağını söylüyor,
  yoktu. Admin paneline "People" sekmesi eklendi

**Ana ekranlar prototipteki sıraya getirildi**

- **Operatör**: büyük "Something wrong? / Create report →" kartı en üstte, **altında kendi
  açık raporları**. Sohbet notu 03 §2: operatör kendi raporunu kimin üstlendiğini görür.
  "Reported By Me"yi menüden kaldırmıştım ama yeteneği tamamen düşürmüşüm — ekranda geri,
  ayrı sayfa olarak değil
- **Mühendis/Manager**: My Active Jobs → **rapor kartı** → Pool (prototipteki sıra;
  ortadaki kart eksikti)

**Bulunan ve düzeltilen gerçek hatalar**

- `crypto.randomUUID()` güvenli olmayan bağlamda tanımsız → telefondan **her istek**
  patlardı (ayrı olarak düzeltildi, `api/uuid.js`)
- `ManagerVisibility` değişikliğinin ortaya çıkardığı test varsayımı: "outsider" olarak
  ikinci bir Operator kullanılıyordu, ama departman artık roldan türediği için o da aynı
  departmanda. Engineer'a çevrildi
- `Date.now()` render sırasında çağrılıyordu (Who's In) — sunucu hesabına taşındı

**Kalanlar — kullanıcı 2026-09-05'te karara bağladı**

- **PPM** — beklemede kalıyor (kullanıcı kararı). Engineer ve Manager menülerinde
  prototipte var, backend'de hiç yok; ayrı bir modül olarak sonra.
- **Foto sayısı** — rapor başına 20 sınırı **kalıyor** (kullanıcı onayladı).
- Diğer üçü yapıldı, aşağıya bak.

---

## 2026-09-05 — Kalan eksiklerin tamamlanması

**Rapor edenin imzası** (sohbet notu 03 §6)

`SignOffKind.Reporter` eklendi. İş kapandığı anda rapor edene bildirim gidiyor
("bir raporun tamamlandı, doğrula"), ve **yalnızca** rapor eden imzalayabiliyor, yalnızca
iş kapandıktan sonra, yalnızca bir kez. Kayıtlı imza görseli varsa ona bağlanıyor.

> **İmza `Closed` durumunu kilitlemiyor**, bilinçli olarak. Kilitleseydi mühendisin
> işçilik saati (claim→close otomatik süre) operatör telefonunu eline alana kadar işlemeye
> devam ederdi — ve o rakam BRC denetçisine gidiyor. Kağıt form da böyle çalışıyor: tamir
> yapılır, sonra imza bloğu doldurulur. Notta bu nokta zaten "netleştirilmeli" diye
> işaretlenmişti; kilitlemesi isteniyorsa yeni bir durum (`AwaitingSignature`) gerekir.

**"Reported To / Position"** (kağıt formdaki isim-pozisyon kutuları)

`ReportedByPosition`, `ReportedToName`, `ReportedToPosition` eklendi. Pozisyon **anlık
kopya** olarak saklanıyor, sonradan bakılmıyor: form o günkü görevi kaydeder, kişi bir yıl
sonra görev değiştirince denetçinin gördüğü eski rapor değişmemeli. Kime söylendiği serbest
metin — gece vardiyasında söylenen kişinin hesabı bile olmayabilir.

Artımlı migration (`AddReportedToAndPosition`) — dev veritabanı sıfırlanmadı.

**Parça bekleyenler kalıcı listesi** (sohbet notu 03 §3)

`GET /workorders?waitingParts=true`. Rapor edene bir kerelik bildirim gidiyor, manager ise
kalıcı listeyi dashboard'ında görüyor — notta ayrımı tam olarak böyle tarif edilmiş.

**Doğrulandı**

- `dotnet test` — **211/211** (yeni: rapor eden imzası + yetki kapıları, isim/pozisyon
  kutuları, parça bekleyenler listesi, who-is-in, bildirimler)
- Uçtan uca smoke: **iki gerçek kullanıcıyla** (rapor eden + mühendis) tüm akış, 403
  durumları dahil — sadece admin token'ıyla test etmek bu kuralların hepsini gizlerdi
- Menü/rota yetki çapraz kontrolü: tutarlı. Bir gerçek uyuşmazlık bulundu ve düzeltildi —
  Messages menüsü `message.send`, rotası `message.read` istiyordu

---

## 2026-09-07 — Fabrikanın kendi listesi: site düzeni, durumlar, roller

Kullanıcı fabrikanın gerçek yerleşimini, durum listesini, iş tiplerini, öncelikleri ve
rolleri verdi. Bunlar **tahmin değil, sahanın kendi tarifi** — çakıştığı her yerde kod
değişti, liste değil.

### Site düzeni — 5 seviye

`Unit → Area (oda) → Line → Equipment → Equipment (parça)`

Seed'e giren: **4 unit, 12 oda, 4 hat, 62 makine** (2'si parça). Ayrıntılı liste
`docs/DOMAIN.md` §1'de. Unit 3'ün Warehouse ve Workshop'u var, makineleri henüz girilmedi.

Site plan HTML'inden türetilmiş eski odalar (F1–F12, Chiller, Hot Sauce Room, Tank Farm,
Weight Bridge, Extractors, Vacuum Generator…) **tamamen silindi**. O liste çizimden
okunmuştu ve fabrikanın gerçekte kullandığı isimlerle örtüşmüyordu.

Üç şey bu düzenden çıkan ve kod gerektiren sonuçlar:

**1. Hat, arıza formunda ayrı bir adım oldu.** Filling Room'da Line 2 ve Line 3'ün ikisinde
de "Seamer" ve "Conveyor" var. Hat sorulmazsa iki farklı makine formda tek isim olarak
görünür ve rapor hangisinin bozulduğunu söyleyemez. Adım yalnızca **seçilen odada hat varsa**
görünüyor — 10 odanın 9'unda hiç çıkmıyor, boşuna bir dokunuş eklemiyor.

**2. Makinenin parçası = yine Equipment, üstündeki makineye bağlı** (`ParentEquipmentId`).
Blender 2 → FIBC1, FIBC2. Ayrı tablo açılmadı: arıza bir parçaya da bir makineye de aynı
şekilde raporlanıyor, iki tablo iki ayrı akış demek olurdu.

İki kural API'de zorlanıyor (400):
- **Tek seviye derinlik.** Parçanın parçası olmaz — o bir yedek parça kataloğu olur ve arıza
  formunda gösterilecek yeri yok.
- **Parça, makinesiyle aynı yerde durur** (aynı unit/oda/hat). İkisi ayrışırsa tek tamir
  denetim kaydına iki farklı yer adıyla geçer.

**3. Odası olmayan unit'e çalışan atanamaz.** Çalışan ataması Area seviyesinde
(`UserArea`), Yard'ın ise odası yok. Yard'da düzenli çalışan biri olacaksa (kapı, FLT)
oraya bir oda açmak gerekecek. Kod bir şey uydurmuyor, liste ne diyorsa o.

Yard'daki Gate 1/2/3 ve DAF Plant, odası olmayan makineler olarak duruyor (`AreaId` null) —
Yard dışarısı, kapılar sokak kapısı, DAF Plant kanalizasyona vermeden önce suyu temizleyen
makine. CCTV Room ise Office'in içinde bir ekipman.

### Durumlar

```
New → Accepted → InProgress → WaitingParts → AwaitingQa → QaFailed → Completed / Rejected
```

`Claimed → Accepted`, `Closed → Completed`, `Cancelled → Rejected`. Ordinal değerler aynı
kaldı, yani veri taşınmadı.

- **`Rejected`, `Cancelled`'ın yerine geçti.** Fabrikanın listesinde `Cancelled` yoktu.
  Akış aynı: sebep zorunlu, terminal, kendi event'i. Uç nokta `POST /workorders/{id}/reject`,
  yetki `workorder.reject`.
- **`AwaitingQa` ve `QaFailed` listede yok ama kaldı.** Kullanıcı onayladı: kaldırılsaydı
  intrusive işlerdeki QA swab akışının gidecek yeri kalmazdı.

### İş tipleri

`JobType`: `Reactive | Task | Ppm | Project`, WorkOrder üzerinde bir alan. Arıza bildirimi
her zaman `Reactive` üretiyor. Task modülü günlük/haftasonu/menejer görevlerini zaten
taşıyor; PPM ve Project'in ekranı henüz yok. Eski `TaskPriority` enum'u
(Reactive/Corrective/Preventive) bununla çakışıyordu, kaldırıldı.

### Öncelik

`Critical` kaldırıldı → **Low / Medium / High**. Migration mevcut `Critical` kayıtlarını
`High`'a çevirdi (uygulamanın adlandıramayacağı bir tamsayı bırakmamak için).

### Roller

`Engineer` → **`Engineer`** (DB kolonu `AssignedEngineerId` dahil). Yeni: **`Supervisor`**
ve **`FltDriver`**.

İkisi de **şimdilik sadece eklendi** — kullanıcının talimatı buydu. Operatörün taban
yetkisiyle başlıyorlar: giriş-çıkış, arıza bildir, izin talebi. Hak edilmemiş bir yetkiyi
sonradan geri almak, hiç verilmemiş olanı eklemekten zordur. İkisini de **Production
Manager** onaylıyor; sahada çalışıyorlar, bakımın altında değiller.

### Seed'e girerken düzeltilen yazımlar

`Product feet tank` → **Product Feed Tank** · `Big shrink rapper` → **Big Shrink Wrapper** ·
`Diagphram pump RM6/RM7` → **Diaphragm Pump RM6 / RM7** · `Hot water circulation pimp` →
**Hot Water Circulation Pump**. Makine adları Title Case'e çevrildi. `Pasteurizer` kendi
yazımıyla bırakıldı (fabrika `Homogeniser` 's' ile, `Pasteurizer` 'z' ile yazmış).

### Doğrulandı

- `dotnet test` — **214/214** (yeni: site 4 seviye derinlikte seed'leniyor mu, aynı isimli
  iki makine iki ayrı yerde ayakta kalıyor mu, makine parçası kuralları)
- Uçtan uca smoke: odalar/hatlar/makineler, iki Seamer'ın ayrışması, parçaya arıza bildirimi,
  hat üzerinde arıza bildirimi, `Priority = 3` reddi (400), reject akışı ve sonrasının
  terminal olması (409)
- `npm run lint` (oxlint --deny-warnings), `npm run build`, `check:i18n` — 331 anahtar × 4 dil

### Dev veritabanı temizliği

Eski oda/makine kayıtları ve onlara bağlı 11 smoke-test iş emri elle silindi
(`SignOffs → QaChecks → WorkOrderCosts → Closures → Events → Photos → WorkOrders`, sonra
`UserAreas → Equipment → Lines → Areas`). Eski `Engineer` rolündeki hesaplar `Engineer`'a
taşınıp rol satırı silindi.

**Bu temizlik bilinçli olarak seeder'a konmadı.** Seeder sunucuda her açılışta çalışıyor ve
asla veri silmemeli; sıfır bir veritabanında zaten sadece doğru liste yazılıyor.

### Aynı gün, kullanıcı düzeltmeleri

**Unit 3'ün odaları var.** Warehouse ve Workshop eklendi — Warehouse'lar her unit'te oda
seviyesinde. Makineleri henüz girilmedi. Artık odası olmayan tek yer **Yard**.

**`Technician` → `Engineer` geri alındı.** Fabrikanın rol listesinde "Technician" yazıyordu,
ama saha "Engineer" demeyi tercih etti. DB kolonu da `AssignedEngineerId` olarak geri döndü.
Bunu `SiteLayoutAndJobTypes` migration'ını düzenleyerek değil, **ayrı bir migration** ile
yaptım (`RenameAssignedEngineerBack`): o migration çalışmıştı, uygulanmış geçmişi bir kolon
adı için yeniden yazmak taşıdığı riske değmez. Migration ayrıca `Technician` rolündeki
hesapları `Engineer`'a taşıyor — sıfır bir veritabanında hiçbir satır eşleşmiyor, zararsız.

**FLT kendi departmanı oldu.** Artık üç departman var: Production, Maintenance, **FLT**.
`FltDriver` rolü FLT'ye düşüyor (departman roldan türetiliyor).

> **Bunun bir sonucu var ve bilinçli:** manager kendi departmanını + `ManagerScope`'un
> verdiğini görür. Yani Production Manager, FLT sürücülerini **onaylayabiliyor** ama Team
> ekranında **göremiyor** — admin panelinden ona FLT departmanı için scope verilene kadar.
> Bu sahanın vereceği bir veri kararı, koda gömülecek bir kural değil. FLT'yi kimin
> yöneteceği netleşince tek satırlık bir scope kaydıyla çözülür.

**Arıza bildirme kartı tek satıra indi.** Kartta dört satır yazı vardı ("Something wrong?",
"Pick the unit, the area and the machine…", "Create report →"). Şimdi sadece **Report
breakdown**. Kart ekrandaki en büyük şey ve sarı — ne işe yaradığı zaten belli, anlatmak
okumayı yavaşlatıyordu. Kullanılmayan 3 i18n anahtarı 4 dilden de silindi.

### UI sadeleştirmeleri (kullanıcı geri bildirimi)

**Arıza formu.** "Reported to" ve "Position" kutuları kaldırıldı — rapor edenin pozisyonu
zaten hesaptan geliyor. **Description artık zorunlu değil.** Ne bozuldu ve nerede bilgisi
formda zaten var, fotoğraf da makinede tek elle yazılan bir satırdan çok şey anlatıyor;
hiç girilmeyen bir rapor, kısa olandan kötüdür. Alan `NotNull` kaldı (kolon NOT NULL), yani
boş string olarak geliyor. **Ne bozulduğu hâlâ zorunlu** — ne makine ne de serbest metin
veren bir kayıt rapor sayılmaz (DB'deki `CK_WorkOrder_EquipmentIdentified` ile aynı kural).

**"Turn this report down" → "Reject".** Etiket, durumun adıyla aynı kelimeyi kullanmıyordu
ve ne yaptığı anlaşılmıyordu. Dört dilde de düzeltildi.

**Downtime saat:dakika soruluyor.** 75 yerine `01:15`. Sahada duruş saatle konuşuluyor;
tamirin sonunda kafadan dakikaya çevirmek, denetçiye giden bir sayının yanlış yazılma yolu.
DB'de yine dakika olarak duruyor (her hesabın istediği birim), ekranda `1h 15m` okunuyor —
işçilik süresi de aynı biçimde. Duruş süresi kapanış kaydında ilk kez gösteriliyor;
toplanıyordu ama hiçbir yerde görünmüyordu.

**Cost bölümü UI'dan kaldırıldı** (parça adı, parça fiyatı, saatlik işçilik, PO numarası).
Saha tamir maliyetini burada tutmuyor. **Kolonlar kapanış kaydında duruyor** — geri açmak
sadece bir UI değişikliği.

**Bildirim ekranı sadece menejer mesajları.** Eskiden her iş adımını da tekrarlıyordu
("mühendis yolda", "parça bekleniyor"). İş kartı zaten nerede kalındığını daha ayrıntılı ve
insanların gerçekten baktığı yerde gösteriyor; burada tekrarlamak, başka yerde görünmeyen
tek şeyi — mesajları — gömüyordu.

> Log'un kendisi her satırı yazmaya devam ediyor, denetimde görünüyor; bu bir **gösterim
> filtresi**, kayıt kararı değil. Geri açmak tek bir predicate.
>
> Filtre `unseen-count`'a da uygulandı: rozet, ekranın listelediği satırları sayıyor.
> Tıklayınca orada olmayan üç şeyi vaat eden bir rozet, hiç rozet olmamasından kötüdür.

Mesaj satırı artık gönderenin adını da taşıyor (`NotificationDto.SenderName`) — "yeni mesaj
var" yazıp ikinci bir ekran açtırmak yerine kimden ve ne yazdığı doğrudan görünüyor.
