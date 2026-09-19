# CFI App — İlerleme Günlüğü

> Bu dosya **her faz/modül bitiminde güncellenir**. En yeni kayıt en üstte.
> Plan: [`ROADMAP.md`](ROADMAP.md) · Kararlar: [`DECISIONS.md`](DECISIONS.md) · Veri modeli: [`DOMAIN.md`](DOMAIN.md)

## Özet durum

| Faz | Durum |
|---|---|
| 0 — Kurulum | 🟩 Tamamlandı |
| 1 — Veri modeli | 🟩 Tamamlandı |
| 2 — Auth | 🟩 **Tamamlandı** |
| 3 — Maintenance API | 🟩 Tamamlandı |
| 4 — Web UI | 🟩 **Tamamlandı** — her modülün ekranı yazıldı |
| 5 — Tasks + Orders | 🟩 Tamamlandı (API + ekran) |
| 6 — Attendance | 🟩 Tamamlandı (API + ekran) |
| 7 — Shift Planning | 🟩 Tamamlandı (API + sürükle-bırak planlayıcı) |
| 8 — Messages | 🟩 Tamamlandı (API + ekran; gerçek push hariç) |
| 9 — Admin panel + BRC export | 🟩 Tamamlandı (PDF/print ve PPM hariç) |
| 10 — Deployment altyapısı | 🟩 Tamamlandı (parça — domain/TLS/sunucu kullanıcı girdisi bekliyor) |
| 11+ | ⬜ Sıradaki |

**Güncel sayılar:** 41 tablo · 205 test geçiyor · 22 ekran · 286 çeviri anahtarı × 4 dil ·
backend build 0 uyarı · frontend lint/i18n/build temiz

---

## 2026-09-04 (gece) — FAZ 4 TAMAMLANDI: web uygulamasının kalan tüm ekranları

Kullanıcı "sonuna kadar bitir, ben uyuyorum" dedi. Backend'i zaten hazır olan her modülün
ekranı yazıldı. Alınan kararlar `DECISIONS.md` sonuna işlendi.

**Yazılan ekranlar (22 sayfa)**

| Modül | Ekran |
|---|---|
| Tasks | liste (Hepsi / Benim / Bitenler sekmeleri), yeni task formu, detay + tamamlama (not + foto) |
| Orders | parça talebi oluştur, listele, "ordered" işaretle, sil |
| Attendance | clock in/out + günlük toplam + kendi geçmişi |
| Team hours | haftalık ekip saatleri, kişi başı toplam, **düzeltme formu** |
| Overtime | kendi beyanı + manager onay/red |
| Holiday | talep + manager onay/red (iş günü sayısı sunucudan) |
| Shift planner | hafta × vardiya tablosu, **sürükle-bırak** atama/taşıma/silme |
| My shifts | çalışanın yaklaşan vardiyaları, bugün vurgulu |
| Messages | inbox (okunmamış vurgulu), kişi/departman seçerek gönder, okundu makbuzları |
| Admin | Unit / Area / Makine / Vardiya CRUD + **Hours & leave** (geofence, resmi tatiller) |
| Profile | bilgiler, dil, parola değiştirme, **admin paneli butonu** |
| Dashboard | Maintenance Manager'a **Factory Overview** (5 tıklanabilir kutu) |
| History | tam metin + tarih filtresi, **CSV export butonu** |
| Work order detay | **mühendise atama** ve **rapor iptali** eklendi |

**Navigasyon yeniden düzenlendi** — 4 birincil bağlantı üst barda, geri kalanı tek "More"
menüsünde (prototipteki hamburger mantığı). Yetki haritası tek bir tabloda: yeni ekran
eklemek JSX'te yetki avlamak değil, tabloya bir satır eklemek.

**Backend'e eklenen eksik**

`POST /workorders/{id}/cancel` — `workorder.cancel` yetkisi ve `Cancelled` durumu Faz 1'den
beri vardı ama ucu hiç yazılmamıştı, yani mükerrer rapor havuzdan hiçbir şekilde çıkmıyordu.
Sebep zorunlu, silme değil, iptal sonrası terminal. 1 yeni test.

**Doğrulandı**

- `dotnet test` — **205/205**
- `npm run lint` 0 uyarı · `check:i18n` **286 anahtar × 4 dil** · `npm run build` başarılı
- **Uçtan uca smoke testi**: web uygulamasının çağırdığı **her ucu**, sayfaların çağırdığı
  gövde/parametrelerle çalıştıran bir betik yazıldı (`scratchpad/smoke.py`) — 60+ istek,
  hepsi beklenen cevabı verdi. Test paketinin göremediği boşluk bu: sayfanın `unitId`
  gönderip API'nin `areaId` beklemesi gibi sözleşme uyuşmazlıkları
- Smoke testi bir "hata" buldu ki doğru davranıştı: manager `holiday.request` iznine sahip
  değil (izni onaylıyor, kendisi talep etmiyor — eski karar). Test bunu artık **beklenen
  403** olarak doğruluyor

**Bilinçli olarak yapılmayanlar**

- **Seed verisi** — kullanıcı "web bitince ben hazırlayacağım" dedi. Şu an site planından
  ve prototiplerden gelen geçici veri var
- **Makine bölgeleri** — seed'e bağlı, seed'le birlikte yapılacak
- **PPM** ve **PDF/print** — Faz 9'un kalanı, ayrı konuşulacak
- **Occupation ve Line CRUD ekranı** — backend'de var, kullanımda değil (Occupation onayda
  artık sorulmuyor, Line operatör akışına ait ve o akış kullanılmıyor)
- Gerçek push (Firebase kimlik bilgisi yok) — Faz 8'den devreden

**Sırada:** kullanıcının web'i tarayıcıda incelemesi → geri bildirim → seed verisi →
onayıyla Faz 11 (React Native)

---

## 2026-09-04 — Prototip/not incelemesi sonrası düzeltmeler (kullanıcı geri bildirimi)

Kullanıcı prototipleri (`mainty_*_prototype.html`), site planını ve sohbet notları 01–10'u
referans göstererek eksikleri çıkardı. Hepsi uygulandı.

**Site hiyerarşisi yeniden adlandırıldı — `Unit → Area → Equipment`**

Kod baştan tersti (Area en üstte). Kullanıcı netleştirdi: Unit = U1/U2/U3/Yard,
Area = Filling Room / Packing (unit içinde). 54 dosyada `Area ↔ Unit` takas edildi,
migration sıfırdan üretildi, dev veritabanı sıfırlandı. **204 test refactor'ü doğruladı.**
Takasın yakaladığı iki yan etki elle düzeltildi: `<textarea>` → `<textunit>` olmuştu ve
`ManagerScope` konfigürasyonu yanlışlıkla Area'ya bağlanmıştı.

**Rol bazlı ekran temizliği**

- **Reported By Me** her rolden kaldırıldı (ekran + menü + route + endpoint wrapper)
- **Sent Back By QA** ayrı ekran olmaktan çıktı: QA'nın geri gönderdiği iş artık
  **My Jobs içinde**, kırmızı uyarıyla ve **listenin en üstünde** (`needsMeFirst`
  sıralaması). QA bekleyen iş de listede kalıyor — iş gerçekten kapanana kadar düşmüyor
- `workorder.viewAll` ikiye ayrıldı → yeni **`workorder.history`** izni (arşiv + BRC CSV
  export) sadece Engineer + Maintenance Manager'da. Pool artık `workorder.claim` ile
  korunuyor: havuz sahiplenme kuyruğu, sahiplenmeyen rol görmüyor
- Production Manager dashboard'u = **açık arızalar** (`GET /workorders?open=true`),
  QA dashboard'u = swab testi bekleyenler

**Report Breakdown — gerçek kademeli dropdown**

Unit → Area → Makine. Serbest metin yalnızca "Other — not on the list" seçilince.
Seed: **31 area** site planından, **18 makine** prototiplerden (ikisi de admin panelinden
düzenlenebilir, seeder üzerine yazmıyor). API tarafında da doğrulanıyor — başka unit'in
makinesini raporlamak **400**.

**Onay akışı**

- Maintenance/Production dropdown'ı kaldırıldı; **departman roldan türetiliyor**
- `Permissions.ApprovableRoles`: MM → Engineer/QA/Manager'lar (Operator **değil**),
  PM → Operator. Rol listesi ucu da sadece verilebilecekleri döndürüyor
- Çalışma yeri ataması **area seviyesine kadar**: Unit seç → o unit'in area'larını işaretle

**Görünürlük modeli değişti**

`ManagerVisibility.IsUnrestricted` **kaldırıldı** — her zaman false olacaktı ve her zaman
false olan bir bayrak, bayrak olmamasından kötüdür. Yeni kural: manager kendi departmanını
görür + scope satırlarının eklediklerini. Maintenance Manager dahil kimse "herkesi"
görmüyor.

**Arama**

- History: tek kutu tüm kayıtta arıyor (numara, unit, area, makine, açıklama, rapor eden,
  mühendis, root cause, corrective action) + tarih aralığı
- Team: isim/e-posta araması eklendi

**Doğrulandı**

- `dotnet test` — **204/204** (6 yeni test: pool/history yetkileri, rol devri, konum
  bütünlüğü, açıklama araması)
- `npm run lint` 0 uyarı · `check:i18n` 147 anahtar × 4 dil · `npm run build` başarılı
- Canlı: Unit 1 → 16 area → 4 makine listesi API'den doğru dönüyor

**Yazarken bulunan test kırılganlığı**

Paylaşılan test veritabanı büyüdükçe "kişi ilk 100 satırda mı" varsayımına dayanan bir
test kırıldı. Sorulması gereken soru "manager bu kişiyi görebiliyor mu" — testler artık
e-postayla arayarak soruyor, sayfa 1'i okuyarak değil.

---

## 2026-09-04 — Faz 4 (temel + Maintenance akışı) TAMAMLANDI — kalan ekranlar diğer modüllerle birlikte yapılacak

> Kullanıcı bu ortamda tarayıcıda görsel doğrulamayı kendisi yapıyor; benim engelim değil,
> ekranı kendi tarayıcısında (`http://localhost:5173`) inceleyip onaylıyor. Bu netleşince
> Faz 4'e tam hızla devam edildi.

**Yapıldı**

- Design system'in gerçek ekranlarda kullanımı: `Button`/`Card`/`Badge`/`ErrorBanner` bileşenleri,
  CFI paleti token'ları (Faz 0'da tanımlanmıştı)
- `apiClient` — Bearer interceptor; 401'de **tek seferlik ortak refresh** denemesi (aynı anda birden
  fazla istek 401 alırsa hepsi aynı refresh'i bekler, ayrı ayrı refresh denemez), refresh de
  başarısız olursa token temizlenip `/login`'e yönlendirme. **403 asla logout tetiklemiyor** —
  CLAUDE.md'nin 401/403 kuralına birebir uygun
- `AuthContext` + `RequireAuth` + `RequirePermission` — yetki kontrolü permission string'iyle,
  rol string'iyle değil
- `AppLayout` — üst bar (uygulama adı, dil seçici, kullanıcı adı/rolü, çıkış), yetkiye göre
  gizlenen/görünen nav linkleri
- **Dil seçici tüm rollerde** — `AppLayout` içinde herkese açık, sadece operatöre özel değil
- Rol bazlı ana ekran: Operator → doğrudan "Report Breakdown" CTA'sı; Engineer/Manager →
  My Active Jobs + (yetkiyse) Sent Back By QA + Pool önizlemesi
- Work order akışının tamamı ekranda: rapor oluştur (foto ekleyerek) → pool / mine / sent-back /
  reported-by-me / history listeleri → detay ekranı (claim/start/waiting-parts/resume/notify/
  close/QA/sign-off, hepsi durum + yetki + atama kombinasyonuna göre koşullu) → kapanış formu
  (ayrı `DowntimeMinutes` alanı) → QA sonucu formu
- Team + Pending Approvals ekranları (onay bekleyenler listesi, rol ata, onayla/pasifleştir)
- Her ekranda yükleniyor/hata/boş durumu (`useApiData` hook + `ErrorBanner`) — beyaz ekran yok,
  DoD şartı karşılandı
- i18n: 141 anahtar × 4 dil, `check:i18n` script'iyle CI'da doğrulanıyor

**Bu fazda ortaya çıkan, backend'e eklenen eksikler**

- `MediaController`'da fotoğraf **indirme** ucu yoktu (sadece yükleme vardı) — UI fotoğrafı hiç
  gösteremezdi. `IFileStorage.OpenReadAsync` + `GET /api/v1/media/{id}` eklendi (path traversal
  koruması dahil), 2 yeni test
- `WorkOrderDetailDto`'da `AssignedEngineerId`/`ReportedByUserId` yoktu — "bu iş bana mı atanmış"
  sorusu isim karşılaştırmasıyla kırılgan olurdu. İki alan DTO'ya eklendi (saf ekleme, mevcut
  198 test değişmeden geçti)
- Rol dropdown'u için `GET /api/v1/admin/roles` (`RolesController`) eklendi — onay ekranı rolleri
  başka yoldan bilemezdi

**Yazarken bulunan ve düzeltilen iki gerçek hata**

- **Bearer auth + `<img src>` uyumsuzluğu**: tarayıcı `<img>` isteğine Authorization header'ı hiç
  eklemiyor, korumalı foto endpoint'i düz `<img>` ile tarayıcıda çalışmaz. `AuthenticatedImage`
  bileşeni yazıldı — foto'yu `apiClient` üzerinden blob olarak çekip `URL.createObjectURL` ile
  gösteriyor
- **Enum sıra numarası uyuşmazlığı**: backend enum'ları varsayılan olarak sayısal ordinal ile
  serialize ediyor (`"status":0`), string olarak değil. `web/src/api/enums.js` backend enum
  sırasına birebir eşlenen diziler tutuyor (hangi backend dosyalarıyla senkron kalması gerektiği
  dosya içinde yorumla belirtildi) — backend'e `JsonStringEnumConverter` eklenmedi, çünkü bu
  ~198 backend testinin `ReadFromJsonAsync` varsayımını bozardı
- `oxlint`'in `react(set-state-in-effect)` kuralı, mount effect'i memoized (`useCallback`) bir
  fonksiyonu çağırdığında — o fonksiyon `await` sonrası `setState` çağırsa bile — uyarı veriyordu.
  Kök neden davranış hatası değildi (kod zaten doğruydu), ama kural effect'in çağırdığı fonksiyonun
  gövdesini iz sürüyor. Çözüm: mount effect kendi promise zincirini (`getMe().then/catch/finally`
  + `cancelled` bayrağı) doğrudan kuruyor, ortak `setState` mantığı ise `useCallback` **olmayan**
  düz fonksiyonlara (`applyProfileResponse`/`applyProfileFailure`) çıkarıldı — hem uyarı gitti hem
  `login()`'in kullandığı `loadProfile` ile kod tekrarı en aza indi

**Doğrulandı**

- `npm run lint` (`oxlint --deny-warnings`) — 0 uyarı
- `npm run check:i18n` — 141 anahtar × 4 dil tutarlı
- `npm run build` — başarılı, üretim bundle'ı oluşuyor
- Backend: `dotnet test` — 198/198 geçiyor (bu fazda eklenen 2 medya testi + 1 rol testi dahil)

**Kapsam dışı bırakılan (Faz 4'ün ROADMAP'te tanımlı kalanı, ileride ilgili modülle birlikte yapılacak)**

- Manager'ın "Factory Overview" tıklanabilir özet ekranı
- QA'nın swab test kuyruğu (backend'de henüz yok)
- Production Manager'ın team shifts / messages / holiday onay ekranları
- Tasks, Orders, Attendance/Overtime/Holiday, Shift Planning ekranları — backend API'leri
  Faz 5–7'de hazır ama frontend ekranı yok; bu ekranlar ilgili modülle aynı oturumda yazılacak

**Sırada:** Kullanıcı web uygulamasını kendi tarayıcısında (`http://localhost:5173`) inceleyip
geri bildirim verecek. Onay/geri bildirim sonrası: kalan ekranlar (Tasks/Orders/Attendance/
Shift/Messages UI) veya — kullanıcının açık onayıyla — Faz 11 (React Native)

---

## 2026-09-04 — Faz 9 (parça) + Faz 10 (parça) TAMAMLANDI / BRC export + Docker deploy

**Yapıldı — BRC CSV export (Faz 9'un bir parçası)**

- `GET /api/v1/workorders/history/export` — history listesiyle aynı filtreler (arama, tarih), CSV olarak
  **stream** ediliyor (yıllık geçmiş belleğe sığmak zorunda değil)
- CSV alanları doğru kaçırılıyor (virgül, tırnak, satır sonu) — free-text root cause alanı bunları
  rahatça içerebilir
- Sadece `workorder.viewAll` yetkisi olanlar indirebiliyor

**Yapıldı — Deployment altyapısı (Faz 10'un bir parçası)**

- `backend/src/CfiApp.Api/Dockerfile` — multi-stage build, **gerçekten `docker build` ile test edildi**
- `docker-compose.yml` — api + postgres + nginx, health check zinciriyle bağımlı başlatma
- `nginx/nginx.conf` — TLS terminasyonu (sertifika yeri hazır, gerçek sertifika Faz 10 açık sorusu),
  `/api` ve `/health` proxy, web build için statik dosya sunumu (henüz boş — Faz 4/9 sonrası dolacak)
- `.env.example` — production sırrı repoda yok, `POSTGRES_PASSWORD`/`JWT_SIGNING_KEY`/`WEB_ORIGIN` zorunlu
  (boş bırakılırsa compose başlamayı reddediyor)
- Migration'lar **container başlarken otomatik uygulanmıyor** — ayrı, bilinçli bir adım

**Yazarken bulunan ve düzeltilen hata**

- Dockerfile'da `adduser` komutu .NET runtime imajında (Ubuntu 24.04 tabanlı, `adduser` paketi yok)
  çalışmıyordu — imaj zaten hazır bir `app` kullanıcısıyla geliyormuş (uid 1654), onu kullanacak şekilde
  düzeltildi. Gerçekten build edip çalıştırmasam bu prod'da patlardı

**Doğrulandı — gerçek Docker imajı build edilip çalıştırıldı**

- `docker build` başarılı, imaj ~200 MB civarı (SDK katmanı yok)
- Container **non-root** (`app` kullanıcısı) olarak çalışıyor
- `/health/live` → 200, `/health/ready` → PostgreSQL'e gerçekten bağlanıp Healthy dönüyor
- **Production ortamında Swagger 404** (Development dışında kapalı — doğru davranış doğrulandı)
- Docker'ın kendi `HEALTHCHECK`'i (curl ile) **healthy** raporluyor
- CI'ya üçüncü bir job eklendi (`docker`): imajı build eder, ayağa kaldırır, `/health/live`'ı
  bekler — Dockerfile bozulursa artık deploy anında değil, her push'ta yakalanıyor. Aynı
  akış yerelde de simüle edilip doğrulandı

**Sırada:** Faz 4 (Web UI — kullanıcıyla birlikte, görsel geri bildirimle) veya Faz 11 hazırlığı
(React Native — Firebase/Play Store kullanıcı girdisi gerektirir)

---

## 2026-09-04 — Faz 8 TAMAMLANDI / Messages backend

> Gerçek push gönderimi Firebase kimlik bilgisi gerektiriyor (yok). `IPushNotificationSender`
> soyutlaması kuruldu; şu an `NoOpPushNotificationSender` sadece loglayıp geçiyor. Firebase
> hesabı açılınca tek yapılacak iş bu arayüzün gerçek implementasyonunu DI'a eklemek —
> `MessageService`, controller'lar, testler hiç değişmeyecek.

**Yapıldı**

- `MessageService.SendAsync` — departman hedefliyse **üyelik gönderim anında çözülüyor** ve
  `NotificationLog`'a yazılıyor; sonradan departmana katılan kişi eski mesajı görmüyor
  (`DOMAIN.md`'deki karar birebir uygulandı)
- `MessagesController`: gönder (kişi/departman, ikisi karışık olabilir), inbox (adresli + fan-out
  birleşimi), detay (**açmak = okundu işaretlemek**, ayrı "read" endpoint'i yok), okundu makbuzları
  (gönderen görür)
- `DeviceTokensController` — cihaz token kaydı/iptali; aynı token başka hesapla kayıt olursa
  (ortak tablet senaryosu) sahibi güncelleniyor
- Mesaj boş hedefle gönderilmeye çalışılırsa **400**, veritabanına hiç dokunmadan

**Doğrulandı** (193 test)

- Kişiye özel mesaj sadece o kişinin inbox'ında; departmana mesaj o an departmandaki herkese ulaşıyor
- **Sonradan departmana katılan mesajı görmüyor** — gönderim anı esas alınıyor
- Mesajı açmak otomatik okundu işaretliyor, gönderen okundu makbuzunda görüyor
- Hedeflenmeyen kişi mesajı açamıyor (403); operatör mesaj gönderemiyor (403)
- Cihaz token kaydı + push denemesi (no-op) + iptal uçtan uca çalışıyor, hata vermiyor

**Sırada:** Faz 9 (admin panel UI, frontend gerektirir) veya Faz 10 hazırlığı (deployment altyapısı —
kullanıcı girdisi olmadan büyük ölçüde ilerlenebilir)

---

## 2026-09-18 — Shift Planning yeniden tasarlandı / şablon + havuz

**Neden:** Haftalık ızgara planlayıcı işin şeklini yanlış modelliyordu. Aynı insanlar her
hafta aynı vardiyada çalışıyor, dolayısıyla manager her hafta aynı tabloyu elle dolduruyordu.
Ayrıca `DELETE assignments/{id}` satırı kalıcı siliyordu: "geçen ay kim gecedeydi" sorusunun
cevabı sistemde hiç yoktu — BRC denetimi olan bir tesiste boşluk. Üstüne kullanıcı vardiya
oluşturma/silme istedi ve gece vardiyasının Pazar akşamı başlaması gibi vardiyaya ait
özellikler ortaya çıktı.

**Model — iki seviye.** `ShiftAssignment` (kişi + gün + shift) kaldırıldı:
- `ShiftType` artık **şablon**: ad, saat, `Weekdays` (flags), `StartsOn`. Serbestçe silinir,
  çünkü hiçbir şey ona bakmıyor.
- `ActiveShift` (yeni) = **havuz**. Şablonun kopyası: ad/saat/gün/başlangıç kendi üstünde.
  Şablonu silmek ya da düzenlemek havuzdakini değiştirmez — insanlar kopyaya çalışıyor.
  Havuzdan çıkarmak, hiç kullanılmamışsa siler, kullanılmışsa `EndsOn` ile kapatır.
- `ShiftRosterEntry` = kişi + havuz vardiyası + `EffectiveFrom`/`EffectiveTo`. Gün alanı yok:
  günler vardiyaya ait. Unique `(UserId, EffectiveTo)` → kimse aynı anda iki vardiyada olamaz.
- `ShiftOverride` = tarih aralıklı yerine geçme; `ActiveShiftId` null = "çalışmıyor".

Değişiklik hiçbir zaman silme değil: eski satır kapanır, yenisi açılır. Geçmiş böylece
kendiliğinden oluşuyor. Geriye dönük rota yazımı 400.

**Çözümleme tek yerde** (`IShiftResolver`): override → onaylı izin → resmi tatil → havuz
vardiyası (kendi günleri + başlangıcı geçerliyse). Aralık ne olursa olsun 4 sorgu.

**Uçlar:** `GET /shifts/roster?on=`, `POST/DELETE /shifts/pool`, `POST /shifts/roster`,
`POST /shifts/roster/{id}/end`, `POST/DELETE /shifts/cover`, `GET /shifts/changes`,
`GET /shifts/mine`; `DELETE /admin/shift-types/{id}` eklendi.

**Ekran:** üç kolon — ekip / çizilmiş vardiyalar / havuz. Vardiya havuza sürüklenince
kopyalanır ve yerinde kalır; kişi sürüklenince taşınır. Tarihe göre gezinme ayrı sayfaya
alındı (`/shifts/history`): tarih + arama (isim veya vardiya), o gün ne çalışmış ve kimler
varmış.

**Yol boyunca çıkanlar:**
- Sürüklenen şey yalnızca state'te tutulduğu için sayfanın **ilk sürüklemesi yutuluyordu**
  (`onDragOver`'daki `preventDefault` bir sonraki render'a kadar çalışmıyordu). Ref'e alındı.
- Bildirim ekranı `MessageId != null` filtreliyordu, yani gövdesi olmayan sistem bildirimleri
  hiç görünmüyordu. İsimli bir tip listesine çevrildi; iş emri ilerlemesi bilerek dışarıda.
- `hours.js` içindeki `weekStart`/`addDays` yerel gece yarısını `toISOString()` ile kesiyordu;
  BST'de bir gün geri veriyordu. Yerelden okuyacak şekilde düzeltildi — attendance ekranları
  da aynı hatadan etkileniyordu.
- Migration'da veri düzeltmesi: mevcut vardiya tipleri `Weekdays = 0` ile kalacaktı, yani
  hiçbir güne denk gelmeyen vardiya. Pzt–Cum'a çekildi.

**Kapsam dışı:** rotating shift / 4-on-4-off, molalar, çalışanın değişiklik talebi + onayı.

**Test:** 233/233 yeşil. Web: lint + i18n (378 anahtar × 4 dil) + build temiz.

---

## 2026-09-04 — Faz 7 TAMAMLANDI / Shift Planning backend

**Yapıldı**

- `ShiftsController` — `POST/DELETE /shifts/assignments` (drag&drop'un tek karşılığı: bırak = POST,
  taşı = DELETE + POST), `GET /shifts/assignments` (manager board, `ManagerScope` ile daraltılmış),
  `GET /shifts/mine` (çalışanın kendi yaklaşan vardiyaları)
- Aynı kişi aynı vardiyaya iki kez bırakılırsa **409** (DB unique index zaten vardı, ilk kez gerçek
  bir endpoint'te devrede)
- Manager kendi `ManagerScope`'u dışındaki birini planlayamıyor (403) — aynı kural her yerde tutarlı
- Pasif vardiya tipine atama **400** ile reddediliyor

**Yazarken bulunan iki hata (biri test, biri paylaşılan test kırılganlığı)**

- Testte `DateOnly` query parametresini örtük `ToString()` ile URL'ye koymuşum — kültüre bağlı format
  ASP.NET'in binder'ının beklediğinden farklı çıkıp `from`/`to` sessizce `0001-01-01`'e düşüyordu, board
  boş dönüyordu. `{date:O}` (ISO 8601) ile düzeltildi
- Faz 1'den kalma bir test (`The_three_default_shifts_are_seeded...`) vardiya tablosunda **tam olarak**
  3 satır olduğunu varsayıyordu; bu faz gerçek bir özellik olarak yeni vardiya tipi eklemeyi mümkün kılınca
  paylaşılan test veritabanında satır sayısı arttı ve test kırıldı. "Yard" örneğiyle aynı sınıf hata:
  varsayım isme değil listenin tamlığına dayanıyordu. `ShouldContain` ile gevşetildi

**Doğrulandı** (185 test)

- Bırakma → board'da görünüyor; taşıma (sil+ekle) → tek kayıt, yeni vardiyada
- Kapsam dışı planlama 403; pasif vardiyaya atama 400; operatör board'u açamıyor (403)
- Çalışan kendi yaklaşan vardiyalarını görebiliyor

**Sırada:** Faz 8 — Messages (gerçek push gönderimi Firebase gerektirir, dışarıda; mesaj oluşturma/okuma/
hedefleme backend'i şimdi kurulabilir, sistem bildirimleri zaten Faz 3/6'da `NotificationLog`'a yazılıyor)

---

## 2026-09-04 — Faz 6 TAMAMLANDI / Attendance backend

> Gerçek GPS koordinatı sizden gelecek — mekanizma hazır, `admin/geofences` üzerinden
> gerçek değer girildiği an devrede olacak. O ana kadar otomatik clock-in "geofence
> tanımlı değil" diyerek reddediyor, sessizce yanlış yer kabul etmiyor.

**Yapıldı**

- `GeoDistance.HaversineMetres` — saf fonksiyon, iki nokta arası mesafe (test edildi, Widnes ölçeğinde ~1m hassasiyet)
- `AttendanceService.RecordAsync` — Clock In/Out mantığı:
  - **Sırali zorunluluk**: ilk kayıt her zaman In olmalı, sonra In/Out dönüşümlü — art arda iki In **409**
  - Otomatik (geofence) kayıt: aktif geofence yoksa **409**; konum doğruluğu kötüyse **409**; sahte konum
    (`IsMockLocation`) işaretliyse **409**; alan dışındaysa **409**
  - Manuel kayıt: her zaman kabul — GPS/bağlantı sorununda çalışanın yedek yolu
  - **Cihaz saati sunucudan çok saparsa** (`MaxClockDriftMinutes`) kayıt **reddedilmiyor**, `IsSuspect`
    işaretiyle kaydediliyor — manager görür, çalışan dışarıda bırakılmıyor
  - Aynı `ClientId` ile tekrar gönderim → tek kayıt (offline kuyruk güvenliği)
- `ClockCorrection` — **append-only**, yanlış kayıt asla düzenlenmiyor, üstüne düzeltme ekleniyor
- `GeofenceSettingsController` — admin CRUD, aynı anda tek aktif geofence (yenisini aktive etmek eskisini kapatıyor)
- `PublicHolidaysController` — UK resmi tatil listesi (gerçek hard-delete — bu bir denetim kaydı değil, takvim verisi)
- `WorkingDaysCalculator` — saf fonksiyon, hafta sonu + resmi tatil hariç iş günü (test edildi)
- `HolidayController` — talep (otomatik gün hesabı) → manager onayı, **`ManagerScope` ile daraltılmış görünürlük**
- `OvertimeController` — kendi beyanı, manager onayı, aynı scope kuralı
- `AttendanceController` — clock geçmişi (kendi + scope'a göre ekip), düzeltme, aktif geofence bilgisi

**Doğrulandı** (178 test)

- Manuel giriş/çıkış dönüşümlü çalışıyor, art arda iki giriş 409
- Geofence aktifken alan içi + iyi doğruluk → kabul; alan dışı / kötü doğruluk / sahte konum → red
- Cihaz saati 3 saat kayıksa kayıt **reddedilmiyor** ama `IsSuspect=true` ile işaretleniyor
- Aynı offline kayıt iki kez gönderilince veritabanında tek satır kalıyor
- Manager sadece `ManagerScope`'una göre ekibinin kayıtlarını görüyor — kapsam dışı kişi görünmüyor
- Düzeltme orijinal kaydı **değiştirmiyor**, ayrı `ClockCorrection` satırı ekliyor
- Hafta sonu hariç iş günü doğru hesaplanıyor; resmi tatil eklenince o gün sayılmıyor
- Operatör overtime/holiday onaylayamıyor, geofence yönetemiyor (403)

**Sırada:** Faz 7 — Shift Planning (ShiftAssignment API, web drag & drop backend'i)

---

## 2026-09-04 — Faz 3 TAMAMLANDI / Maintenance API

**Yapıldı**

- `IFileStorage` + `LocalFileStorage` — dosyalar içerik hash'ine göre adlanıyor (aynı foto iki kez
  yüklenirse aynı satır), 10 MB / rapor başına 20 foto sınırı, sadece jpeg/png/webp
- `MediaController` (yükle → id al) ve `SignatureController` (tek seferlik kayıtlı imza, değiştirilince
  eskisi `ReplacedAt` ile durur, silinmez)
- **`WorkOrderService`** — tüm yaşam döngüsü: rapor oluştur (3 rol de aynı endpoint, hepsi havuza düşer,
  mühendisin kendi raporu dahil) → claim → assign/reassign → notify (busy/on-my-way) → start/waiting-parts/
  resume → close → QA pass/fail döngüsü → sign-off (production/QA)
- `WorkOrdersController` — pool, mine, sent-back-by-qa, reported-by-me, tam liste, **history** (arama +
  tarih filtresi, kapanmış + QA döngüsündeki işler), detay (sahip/atanan/viewAll yetkisi)
- **Sayaç yarışı çözümü**: WO numarası iki adımlı yazılıyor (geçici→gerçek Id sonrası), sıra/raw SQL
  gerekmeden çakışma imkansız
- **Concurrency**: `DbUpdateConcurrencyException` global handler'a bağlandı — iki mühendis aynı işi aynı
  anda claim ederse biri **409** alıyor (Faz 1'deki xmin token'ı ilk kez gerçek bir özellikte devrede)
- `DomainExceptionHandler` — `WorkOrderNotFoundException`/`WorkOrderForbiddenException`/
  `InvalidWorkOrderTransitionException` otomatik 404/403/409'a dönüşüyor, controller'lar temiz kalıyor
- History araması provider-bağımsız (`ToLower().Contains()`, Postgres'e özel `ILike` değil — SQL Server
  geçişi bozulmasın diye)

**Yazarken bulunan ve düzeltilen gerçek hata**

- `DowntimeMinutes` (kağıt formdaki üretim durma süresi, mühendis girer) ile `WorkOrder.LabourMinutes`
  (claim→close arası otomatik işçilik süresi) **aynı hesaplamayla** dolduruluyordu — ikisi farklı kavram.
  `CloseWorkOrderRequest`'e ayrı bir `DowntimeMinutes` alanı eklendi, `LabourMinutes` sadece otomatik kalmaya
  devam ediyor. Kod yazılırken yakalandı, teste yansımadan düzeltildi.

**Doğrulandı** (138 test + canlı sunucuda uçtan uca)

- Mühendisin kendi raporu bile havuza düşüyor, otomatik kendine atanmıyor
- **Yarış testi**: iki mühendis aynı işi aynı anda claim ediyor → tam olarak biri kazanıyor, diğeri 409
- Bir mühendis aynı anda birden fazla iş tutabiliyor
- Manager doğrudan atayabiliyor ve devredebiliyor; mühendis başkasına iş atayamıyor (403)
- Sadece atanan mühendis notify/start/waiting-parts/close yapabiliyor — başkası dener 403
- Atanmamış (New durumundaki) işi kapatmaya çalışmak 403 (kimse sahibi değil, herkes yasak)
- **Tam intrusive döngü**: close → AwaitingQa → QA Fail (not zorunlu) → QaFailed → "Sent Back By QA"
  listesinde görünüyor → aynı form düzeltilip yeniden gönderiliyor (2. closure versiyonu, ilki duruyor) →
  QA Pass → Closed. 2 closure kaydı, 2 QA check kaydı (biri Fail biri Pass)
- Production ve QA sign-off ayrı ayrı bir kez kaydediliyor, ikinci deneme 403
- Rapor eden kendi kaydını `workorder.viewAll` yetkisi olmadan görebiliyor; ilgisiz operatör başkasının
  kaydını göremiyor (403)
- History: kapanmış + AwaitingQa/QaFailed işleri kapsıyor, açık/pool'daki işleri kapsamıyor; numara/ekipman
  adına göre arama büyük-küçük harf duyarsız çalışıyor; operatör history okuyamıyor (403)
- **Canlı doğrulama**: gerçek HTTP istekleriyle rapor→pool→claim→close akışı çalıştırıldı,
  `downtimeMinutes:35` ile `labourMinutes:0` ayrı ayrı doğru kaydedildi, olay geçmişi eksiksiz

**Sırada:** Faz 4 — Web UI temeli (design system, i18n, rol bazlı ana ekranlar) — bu fazda gerçek ekran
kararları için kullanıcı geri bildirimi gerekecek, tamamen bağımsız ilerlenemez

---

## 2026-09-04 — Faz 2 TAMAMLANDI / Admin CRUD + ManagerScope görünürlüğü

**Yapıldı**

- Admin CRUD: `DepartmentsController`, `OccupationsController`, `ShiftTypesController`, `AreasController`,
  `UnitsController`, `LinesController`, `EquipmentController` — hepsi okuma herkese açık (dropdown'lar için),
  yazma `admin.manage` (ShiftTypes için ayrıca `shift.plan`) yetkisiyle sınırlı
- **Silme yok** — her entity'de sadece `activate`/`deactivate`, DECISIONS.md kuralına uygun
- Üst-varlık doğrulaması: geçersiz `AreaId`/`UnitId`/`LineId` ile kayıt **400** döner (veritabanı FK
  hatasına düşmeden önce)
- `ConflictExceptionHandler` — global exception handler, PostgreSQL unique/FK/check ihlallerini
  otomatik 409/400 ProblemDetails'e çeviriyor. Aynı `Area.Code` ile iki kayıt → **409** (yarış durumunda bile)
- **`ManagerScope` görünürlük mekanizması**: `IManagerScopeReader` — `admin.manage` yetkisi olan
  (MaintenanceManager) sınırsız görür; diğer her manager sadece kendi `ManagerScope` satırlarındaki
  departman/alanı görür; **hiç scope'u olmayan manager hiç kimseyi görmez** (güvenli varsayılan)
- `ManagerScopeController` (ata/listele/kaldır, sadece admin) + `TeamController` (`GET /api/v1/team`,
  scope'a göre daraltılmış çalışan listesi) — mekanizmanın ilk gerçek tüketicisi

**Bulunan ve düzeltilen hata**

- Yeni yazılan `AdminCrudTests`, tüm test paketiyle birlikte çalışınca arada bir düşüyordu. Sebep:
  `DatabaseSeederTests`, seed'in siteye dokunmadığını kanıtlamak için **kasıtlı olarak** "Yard" alanının
  adını "Outside Yard" yapıyor — ve bu değişiklik testler arasında paylaşılan veritabanında kalıcı
  kalıyor. Yeni testim isme göre arıyordu; koda göre aramaya çevrildi (`Code == "YARD"`, diğer testlerin
  zaten yaptığı gibi). Ürün hatası değil, test izolasyon hatasıydı — ama gerçek bir "flaky test" örneği

**Doğrulandı** (113 test, art arda 2 kez tam koşu — kararlı)

- Operatör alan listesini okuyabiliyor ama oluşturamıyor (403)
- MaintenanceManager alan oluşturup pasife alabiliyor; pasife alınan kayıt **silinmiyor**, listede duruyor
- Aynı kodla iki alan → 409; var olmayan `AreaId` ile Unit oluşturma → 400
- Gece vardiyası (22:00→06:00) kabul ediliyor, sıfır uzunluklu vardiya reddediliyor
- ProductionManager vardiya planlayabiliyor ama alan oluşturamıyor (rol ayrımı doğru)
- **Scope'suz manager kimseyi görmüyor**; departmana atanan manager sadece o departmanı görüyor;
  alana atanan manager, departmanından bağımsız o alanda çalışan herkesi görüyor
  (aynı kişi hem Maintenance departmanında hem Yard'da çalışabilir — alan bazlı görünürlük bunu yakalıyor)
- MaintenanceManager hiç scope satırı olmadan herkesi görüyor (admin ayrıcalığı)
- Scope kaldırılınca görünürlük **anında** kesiliyor
- Scope ataması sadece admin'de; departman+alan aynı anda verilirse 400

**Sırada:** Faz 3 — Maintenance API (breakdown raporlama, pool, claim, close job, QA döngüsü, history)

---

## 2026-09-04 — Faz 2 / Kimlik doğrulama akışı

**Yapıldı**

- `AuthService` — kayıt, giriş, refresh, çıkış, parola değiştirme
- **JWT access token** (15 dk) yetki listesini claim olarak taşıyor → yetki kontrolü veritabanına gitmiyor
- **Rotating refresh token** (60 gün): her yenilemede eskisi iptal, yenisi verilir. Sadece hash saklanıyor
- **Reuse detection**: döndürülmüş bir token tekrar kullanılırsa o kullanıcının **tüm oturumları** iptal
- `SecurityStamp`: parola değişince veya hesap pasife alınınca tüm cihazlar düşüyor
- Kayıt → `PendingApproval`; manager onaylayınca rol + departman + çalışma alanları atanıyor → `Active`
- `AuthController` (register/login/refresh/logout/change-password/me/me-language) + `UserApprovalController` (pending/approve/disable)
- **Girdi doğrulama**: FluentValidation + global filter, hatalar `ValidationProblemDetails` olarak tek formatta
- Parola politikası: min 10 karakter, yaygın parola listesi, e-posta içeren parola reddi
- Sayfalama: `pending` listesi `page`/`pageSize`, üst sınır 100

**Bulunan ve düzeltilen hata**

- Hesap pasife alınınca token'lar iptal ediliyordu; refresh bunu "token çalındı" sanıp **401** dönüyordu.
  Doğrusu: önce hesap durumu kontrol edilir → **403 "Account disabled"**. Ayrıca reuse alarmı artık sadece
  gerçekten *döndürülmüş* token için çalışıyor — çıkış yapılmış token'ı zayıf bağlantıda tekrar göndermek
  tüm cihazları düşürmüyor

**Doğrulandı** (96 test)

- Onaysız hesap giriş yapamıyor → 403 (401 değil)
- Yanlış parola ile bilinmeyen hesap **aynı** cevabı veriyor (hesap keşfi engelli)
- Token yokken 401, token var ama yetki yokken **403**
- Refresh rotasyonu: eski token ikinci kullanımda ölüyor
- Parola değişince diğer cihazlar düşüyor
- Pasife alınan hesap anında duruyor
- Rate limit gerçekten devrede (ayrı host ile test edildi)

**Sırada:** Admin CRUD (Area/Unit/Line/Equipment/Occupation/Role), `ManagerScope` görünürlük filtresi, GDPR maddeleri

---

## 2026-09-04 — Faz 2 / Güvenlik çekirdeği

**Yapıldı**

- `PasswordHasher` — PBKDF2-HMACSHA256, 600.000 iterasyon (OWASP), sabit zamanlı karşılaştırma.
  Hash kendi parametrelerini taşıyor: iterasyon sayısı ileride artırılınca eski parolalar
  çalışmaya devam ediyor, giriş anında sessizce yeni parametrelerle yeniden hash'leniyor
- `PermissionRequirement` + `PermissionHandler` + `PermissionPolicyProvider` —
  `[Authorize(Policy = "workorder.claim")]` yazmak yetiyor, `Program.cs`'e kayıt gerekmiyor.
  **Yeni endpoint yanlışlıkla korumasız kalamaz**
- Yetki reddi 403 kalıyor (handler `Fail` çağırmıyor) — frontend'in 401'de logout kuralı bozulmuyor
- Token claim'leri: `cfi:perm` (her yetki için bir claim), `cfi:stamp`, `cfi:lang`

**Doğrulandı**

- 74 test geçiyor (10 yeni parola testi)
- Aynı parola iki farklı hash üretiyor (rastgele salt)
- Bozuk/eksik hash çökme değil başarısız giriş

**Sırada:** JWT üretimi + rotating refresh token, register/onay endpoint'leri, admin CRUD

---

## 2026-09-04 — Faz 1 TAMAMLANDI / Operasyon modülleri

**Yapıldı**

- **Task/Order:** `MaintenanceTask`, `TaskAssignment`, `TaskCompletion`, `TaskCompletionPhoto`, `PartOrderRequest`
- **Attendance:** `ClockEvent`, `ClockCorrection` (append-only), `OvertimeDeclaration`, `GeofenceSetting`
- **Scheduling:** `ShiftType`, `ShiftAssignment`, `HolidayRequest`, `PublicHoliday`
- **Messaging:** `Message`, `MessageRecipient`, `MessageRead`, `DeviceToken`, `NotificationLog`
- **Auditing:** `AuditLog` (append-only)
- Seed'e 3 varsayılan vardiya eklendi: Morning 06–14, Afternoon 14–22, Night 22–06

**Veritabanının zorladığı kurallar**

- Aynı offline clock kaydı iki kez gelirse ikincisi reddedilir
- Yarım koordinat (enlem var, boylam yok) kabul edilmez
- `ClockCorrection` düzenlenemez — yanlış kayıt silinmez, üstüne düzeltme eklenir
- İzin bitiş tarihi başlangıçtan önce olamaz
- Mesaj ya kişiye ya departmana gider — ikisi birden veya hiçbiri olamaz
- Parça talebi adedi 0 olamaz
- Aynı kişi aynı görevi iki kez tamamlayamaz, aynı vardiyaya iki kez atanamaz

**Doğrulandı**

- 64 test geçiyor · 42 tablo · 14 CHECK constraint · 62 foreign key
- Vardiya seed'i dev veritabanında doğrulandı, gece vardiyası gün aşırı (`22:00 → 06:00`)

**Sırada:** Faz 2 — auth (register/onay akışı, JWT + rotating refresh, permission policy'leri, admin CRUD)

---

## 2026-09-04 — Faz 1 / Maintenance modülü

**Yapıldı**

- `WorkOrder`, `WorkOrderPhoto`, `WorkOrderEvent`, `WorkOrderClosure`, `WorkOrderCost`, `QaCheck`, `SignOff`
- `WorkOrderStateMachine` — geçerli durum geçişleri tek yerde, geçersiz geçiş exception
- 7 CHECK constraint veritabanı seviyesinde

**Doğrulandı**

- 54 test geçiyor (state machine 5 + veritabanı kuralları 7 + öncekiler)
- `WorkOrderEvent` update/delete denemesi exception atıyor
- Aynı `ClientId` ile ikinci rapor reddediliyor
- Kapanış düzeltmesi yeni `Version` satırı oluyor, öncekiler duruyor

---

## 2026-09-04 — Faz 1 / Hiyerarşi düzeltmesi + Kimlik modülü

**Yapıldı**

- **Hiyerarşi ters çevrildi: `Area → Unit → Equipment`** (kullanıcı düzeltmesi). Migration sıfırdan yeniden üretildi, dev veritabanı sıfırlandı
- `User`, `UserArea`, `Role`, `Permission`, `RolePermission`, `ManagerScope`, `RefreshToken`, `UserSignature`, `MediaAsset`
- `Permissions.cs` — 31 anahtar + rol→yetki haritası, kodda tek kaynak
- Seed: 4 area, 2 departman, 5 rol, 31 permission

**Doğrulandı**

- Yetki dağılımı: MaintenanceManager 26, ProductionManager 17, Engineer 14, QA 11, Operator 8
- Manager engineer'ı kapsıyor; operatör iş üstlenemiyor; `admin.manage` tek rolde

---

## 2026-09-04 — Faz 1 / Mimari çekirdek + Organizasyon modülü

**Yapıldı**

- `docs/DOMAIN.md` — tüm modüllerin tablo tasarımı
- `IAuditable` / `IDeactivatable` / `IAppendOnly` sözleşmeleri
- `AuditableEntityInterceptor` — audit damgası otomatik, geçmiş değiştirilemez, append-only zorlanır
- `IClock` / `ICurrentUser` soyutlamaları
- `Area`, `Unit`, `Line`, `Equipment`, `Department`, `Occupation`
- `DatabaseSeeder` — eksik olanı ekler, var olanın üzerine yazmaz
- Migration/seed startup yan etkisi değil, config bayrağına bağlı

**Kararlar** (kullanıcı cevapladı)

- Öncelik 4 seviye: `Low / Medium / High / Critical`
- Departmanlar: Production, Maintenance
- Seed sadece Area seviyesi
- Kullanıcı başına tek rol

---

## 2026-09-04 — Faz 0 / Kurulum

**Yapıldı**

- .NET 10 solution: Domain / Application / Infrastructure / Api + Tests
- PostgreSQL 16 bağlantısı (port 5434), `cfiAppDb` oluşturuldu
- Serilog + correlation id, ProblemDetails, API versioning, rate limiting
- `/health/live` ve `/health/ready`
- Web: Vite + React (JS) + Tailwind v4, CFI paleti token olarak
- **Dil seçici her rolde** — EN/PL/BG/ES + `check:i18n` CI kontrolü
- GitHub Actions CI: build + test + lint + i18n + güvenlik taraması

**Doğrulandı**

- `/health/ready` → postgres Healthy
- CORS: izinli origin header alıyor, diğerleri almıyor
- Web build + lint temiz

**Kararlar** (kullanıcı cevapladı)

- Frontend JavaScript, Tailwind + kendi token'larımız, tek repo

---

## 2026-09-07 — Fabrikanın site düzeni ve terminolojisi

Sahanın verdiği gerçek liste uygulandı. Kararların gerekçeleri `DECISIONS.md` sonunda,
veri modeli `DOMAIN.md` §1 ve §4'te.

**Yapıldı**

- **Seed:** 4 unit, 12 oda, 4 hat, 62 makine (2'si makine parçası). Site plan HTML'inden
  gelen eski odalar silindi
- `Equipment.ParentEquipmentId` — makinenin parçası (Blender 2 → FIBC1/FIBC2), tek seviye,
  parça makinesiyle aynı yerde durmak zorunda (API 400 ile zorluyor)
- **Arıza formunda hat adımı** — sadece seçilen odada hat varsa görünüyor
- Durumlar: `Accepted` / `Completed` / `Rejected` (ordinal'ler değişmedi, veri taşınmadı)
- `Cancelled` → `Rejected`: uç nokta `POST /workorders/{id}/reject`, yetki `workorder.reject`
- `JobType` (Reactive / Task / PPM / Project) WorkOrder'a eklendi
- `Priority`'den `Critical` kaldırıldı, migration eski kayıtları `High`'a çevirdi
- Yeni roller `Supervisor` ve `FltDriver` (şimdilik operatör yetkisi)
- **FLT kendi departmanı** — Production, Maintenance, FLT
- **QA Manager rolü + QA kendi departmanı** (2026-09-16) — admin QA Manager'ı onaylar,
  QA Manager kendi QA'lerini onaylar
- Admin paneline **Lines sekmesi** ve ekipmana hat + üst makine alanları
- Migration `SiteLayoutAndJobTypes`

**Doğrulandı**

- `dotnet test` — 214/214
- Uçtan uca smoke: hiyerarşinin beş seviyesi, iki Seamer'ın ayrışması, parçaya ve hatta
  arıza bildirimi, `Priority = 3` reddi, reject sonrası terminal davranış
- `npm run lint` / `build` / `check:i18n` (331 anahtar × 4 dil) temiz

**Sırada**

- PPM ve Project ekranları (kullanıcı PPM'i beklemede tuttu)
- Unit 3'ün Warehouse ve Workshop'unun makineleri henüz girilmedi
- Production Manager'a FLT departmanı için ManagerScope verilmeli (yoksa FLT sürücülerini
  onaylıyor ama Team ekranında göremiyor)
- Yard'a çalışan atanamıyor — odası yok. Oraya düzenli çalışan olacaksa bir oda gerekir
