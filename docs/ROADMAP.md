# CFI App — Yol Haritası (Roadmap)

> Bu dosya projenin **tek kaynak planıdır**. Her faz bittiğinde buradaki durum
> güncellenir. Kararlar müşterinin sohbet notlarından ve HTML prototiplerinden çıkarıldı;
> o belgeler **depoda değil** (müşterinin iç dokümantasyonu), ama **karar verilmiş her şey
> buraya ve `docs/DECISIONS.md` dosyasına taşındı** — plan bu iki dosyadan okunur.

---

## 0. Proje kimliği

| | |
|---|---|
| **Uygulama adı** | CFI App |
| **Müşteri** | County Food Ingredients (CFI) — Dennis Road, Widnes, UK |
| **Kapsam** | Fabrika bakım + operasyon platformu (Maintenance → Shift → Attendance → Communication) |
| **Backend** | .NET 10 (LTS), ASP.NET Core Web API, EF Core 10 |
| **DB** | PostgreSQL (dev/test) → ileride SQL Server geçişi mümkün |
| **Web** | React + Vite + JavaScript + Tailwind (kendi token'larımızla) |
| **Mobil** | React Native (önce Android, sonra iOS), JavaScript |
| **Repo** | Tek repo (monorepo): `backend/` + `web/` + `mobile/` |
| **Auth** | JWT access token + rotating refresh token (kalıcı oturum) |
| **Diller** | EN (varsayılan), PL, BG, ES |
| **Sıra** | Önce Web → sonra Mobil (aynı özellikler, aynı backend) |
| **Kalite hedefi** | **Üretim (production) — MVP DEĞİL.** Gerçek fabrikada, gerçek çalışanlarla, BRC denetimine giren kayıtlarla çalışacak. Simülasyon, mock veri, "sonra düzeltiriz" yok |

**Bağlantı (dev):**

```
Host=localhost;Port=5434;Database=cfiAppDb;Username=dev;Password=dev1234
```

---

## 1. Klasör yapısı (hedef)

```
CFI_App/
├─ CLAUDE.md                  # Her oturumda otomatik okunan proje kuralları
├─ docs/
│  ├─ ROADMAP.md              # bu dosya
│  ├─ DECISIONS.md            # kararlar (ADR) — kısa, madde madde
│  ├─ DOMAIN.md               # veri modeli özeti (Faz 1'de yazılır)
│  └─ API.md                  # endpoint listesi (fazlar ilerledikçe)
├─ backend/
│  ├─ CfiApp.sln
│  ├─ src/
│  │  ├─ CfiApp.Domain/         # entity + enum + domain kuralları (bağımlılık yok)
│  │  ├─ CfiApp.Application/    # servisler, DTO, interface
│  │  ├─ CfiApp.Infrastructure/ # EF Core, DbContext, migration, storage, push
│  │  └─ CfiApp.Api/            # controller, auth, middleware, Program.cs
│  └─ tests/CfiApp.Tests/
├─ web/                       # React + Vite
└─ mobile/                    # React Native (Faz 11)
```

---

## 2. Fazlar — özet tablo

| Faz | İçerik | Model | Tahmini oturum |
|---|---|---|---|
| **0** | Kurulum, iskelet, DB bağlantısı, test projesi, CI, CLAUDE.md | Sonnet (kararlar Opus) | 2–3 |
| **1** | Veri modeli + EF Core + migration + seed + index/concurrency | **Opus** tasarım / Sonnet yazım | 3–4 |
| **2** | Auth: register→onay, JWT+refresh, permission, admin CRUD, veri koruma | **Opus** | 4–5 |
| **3** | Maintenance core: Breakdown / Pool / Close Job / QA döngüsü / History | **Opus** akış, Sonnet endpoint | 6–8 |
| **4** | Web UI temeli + design system + i18n + rol bazlı routing | Opus (1 kez) → Sonnet | 6–8 |
| **5** | Tasks + Orders (mühendis / manager) | Sonnet | 3–4 |
| **6** | Attendance: Clock In/Out (GPS + manuel), Overtime, Holiday | **Opus** kural motoru, Sonnet UI | 4–5 |
| **7** | Shift Planning: shift tipleri + web drag & drop planner | **Opus** model/DnD, Sonnet UI | 4–5 |
| **8** | Messages + Push Notifications (FCM) | Opus (1 kez) → Sonnet | 3–4 |
| **9** | Admin panel UI, BRC audit/rapor, PDF/print, arama, PPM | Sonnet | 4–5 |
| **10** | Production hardening + deployment: staging, monitoring, yedek tatbikatı | **Opus** güvenlik, Sonnet altyapı | 4–5 |
| **11** | React Native (Android): offline DB + sync + geofence + push | **Opus** sync mimarisi, Sonnet ekran | 7–9 |
| **12** | iOS + tablet + saha devreye alma + destek | Sonnet | 4+ |
| **Sonra** | Incident Reporting, Production/Warehouse | — | — |

> Tahminler **üretim kalitesine göre** verildi: her sayı, o fazın testleri ve yetki
> testleri yazılmış, gözden geçirilmiş ve gerçek veriyle çalışır haldeki süresidir.
> MVP tahminlerinin yaklaşık 1.5 katı — kasıtlı.

---

## 2b. Üretim standardı — her faz için "bitti" tanımı (Definition of Done)

Bu proje MVP değil. Aşağıdakiler **her fazda** geçerli; bir madde eksikse o faz bitmemiştir.
Sonradan toplu "kaliteye çekme" turu yapılmayacak — o tur hiç gelmez.

**Doğruluk**

- [ ] Servis/kural katmanı için **unit test**; endpoint'ler için **integration test** (xUnit + Testcontainers ile gerçek PostgreSQL, in-memory provider değil)
- [ ] Her endpoint için **yetkisiz erişim testi** — yanlış rolle çağrıldığında 403 döndüğü test edilir
- [ ] Girdi doğrulama (FluentValidation), hata cevapları tek formatta (**ProblemDetails / RFC 7807**)
- [ ] İş kuralı ihlali sessizce geçmez: durum makinesi geçersiz geçişi reddeder (örn. kapanmış işi tekrar claim etmek)

**Veri**

- [ ] Her liste endpoint'i **sayfalı** (`page`, `pageSize`, toplam sayı) — sınırsız liste yok
- [ ] Sorgu planına göre **index**; **N+1 yok** (eski projedeki "id listesi al, döngüde tek tek yükle" deseni tekrarlanmayacak)
- [ ] Aynı anda iki kişi aynı kaydı değiştirirse **concurrency token** ile çakışma yakalanır (örn. iki mühendisin aynı işi claim etmesi)
- [ ] Transaction sınırları net; kısmi yazma bırakmaz
- [ ] Migration geri alınabilir, veri kaybettirmez; seed **idempotent**

**Güvenlik**

- [ ] Yetki reddi 403, token sorunu 401 — istisnasız
- [ ] Rate limiting (özellikle login ve register), hesap kilitleme
- [ ] Secret'lar repoda değil (JWT key, DB parolası, FCM anahtarı)
- [ ] Hassas veri loglanmaz: parola, token, tam GPS koordinatı
- [ ] Yükleme güvenliği: dosya tipi/boyut kontrolü, orijinal dosya adı doğrudan kullanılmaz

**İzlenebilirlik (BRC şartı)**

- [ ] `WorkOrderEvent` / `AuditLog` **append-only** — geçmiş kayıt silinmez, üzerine yazılmaz
- [ ] Kapanış formu düzeltilirse (QA Fail sonrası) **versiyonlanır**, önceki hali kaybolmaz
- [ ] Her kayıtta: kim, ne zaman, hangi cihazdan
- [ ] Structured logging + correlation id (bir isteğin tüm izi tek id ile bulunur)

**Kullanılabilirlik**

- [ ] 4 dilde **eksik çeviri anahtarı yok** (CI'da kontrol edilir)
- [ ] Yükleniyor / boş / hata durumları her ekranda tanımlı — beyaz ekran yok
- [ ] Ağ hatası kullanıcıya anlaşılır şekilde söylenir, ne yapacağı yazar
- [ ] Fabrika ortamı gerçeği: eldivenli parmak için yeterli buton boyutu, parlak ışıkta okunur kontrast

**Süreç**

- [ ] Faz sonunda **Opus ile `/code-review`**
- [ ] Auth, attendance, mobil sync fazlarında ayrıca **`/security-review`**
- [ ] CI yeşil: build + test + lint (GitHub Actions)

> **Mock veri yasağı:** Prototiplerdeki "4.5 saniye sonra otomatik Approved olur",
> "birkaç saniye sonra mühendis üstlenir" gibi simülasyonlar **koda geçmeyecek**.
> Prototip demoydu; uygulama gerçek veriyle çalışır.

> **Boş ekran yasağı:** Bir modül hazır değilse menüde görünmez. Prototipteki
> "PPM is coming later" yer tutucusu üretimde olmaz — PPM ya Faz 9'da yapılır ya menüden çıkar.

---

## FAZ 0 — Temel kurulum

**Amaç:** Boş ama çalışan bir iskelet. "Hello World" uçtan uca gitsin.

- [ ] `git init`, `.gitignore` (.NET + Node), branch stratejisi (`main` + feature branch)
- [ ] `CLAUDE.md` oluştur (proje kuralları, stack, çalışma şekli)
- [ ] `docs/DECISIONS.md` oluştur — sohbet notlarındaki **kesinleşmiş kararlar** buraya taşınır
- [ ] .NET 10 solution + 4 proje + referans grafiği
- [ ] EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` paketleri
- [ ] `appsettings.Development.json` (connection string) + user-secrets ile JWT key
- [ ] `GET /api/health` → veritabanına `SELECT 1` atıp OK dönsün (bağlantı doğrulaması)
- [ ] Swagger/OpenAPI, CORS (web için), Serilog (structured, correlation id), global exception handler → **ProblemDetails**
- [ ] **API versioning** (`/api/v1/...`) — mobil eski sürümde kalabilir, kırıcı değişiklik yapılamaz
- [ ] **Rate limiting** (ASP.NET Core built-in), request size limitleri
- [ ] Health check ayrımı: **liveness** (uygulama ayakta mı) + **readiness** (DB'ye ulaşıyor mu)
- [ ] **Test projesi ilk günden**: xUnit + Testcontainers (gerçek PostgreSQL) + FluentAssertions, ilk smoke test
- [ ] **CI (GitHub Actions)**: her push'ta build + test + lint. Kırmızı CI ile faz ilerlemez
- [ ] `web/` içinde Vite + React (JS) iskeleti, Tailwind kurulumu + palet token'ları, ESLint + Prettier, `.env` ile `VITE_API_BASE_URL`
- [ ] Web'den health endpoint çağrısı — uçtan uca ilk bağlantı
- [ ] `.editorconfig`, `Directory.Build.props` (nullable enable, warnings as errors)

**Çıktı:** `dotnet run` + `npm run dev` çalışıyor, web backend'e ulaşıyor, veritabanı bağlı, CI yeşil.

**Model:** Sonnet (tamamen mekanik). Yalnızca "solution yapısı nasıl olsun" kararını Opus ile 1 kez konuş.

---

## FAZ 1 — Veri modeli (en kritik faz)

**Amaç:** Tüm modüllerin tabloları tek seferde, tutarlı şekilde tasarlansın.
Sonradan tablo eklemek kolay; yanlış ilişki kurmak pahalı.

### Modül bazında entity taslağı

**Organizasyon / yerleşim** (admin panelinden CRUD edilebilir, hardcode YOK)

- `Department`, `Unit` (Unit 1/2/3, Yard), `Area` (Chiller, Filling Room, Packing, Plantroom, Melt Room, Boiler Room …), `Line`, `Equipment` (line veya area'ya bağlı, ikon ya da foto)
- `Occupation` (Operator, Engineer, QA, FLT Driver, Packer …) — dinamik liste

**Kimlik**

- `User` (ad, email, telefon, passwordHash, status: Pending/Active/Disabled, occupation, department, areas)
- `Role`, `Permission`, `RolePermission` — rol değil **permission** bazlı yetki
- `ManagerScope` — hangi manager hangi department/area'dan sorumlu (görünürlük kuralı)
- `RefreshToken` (rotating, cihaz bazlı, revoke edilebilir)
- `UserSignature` — tek seferlik kayıtlı parmak imzası (değiştirilebilir)

**Maintenance**

- `WorkOrder` — unit, area, line, equipment (veya serbest metin), reportedBy, assignedEngineer, severity, status, description, createdAt, claimedAt, closedAt
- `WorkOrderStatus`: `New(Pool) → Claimed → InProgress → WaitingParts → AwaitingQa → QaFailed → Closed`
- `WorkOrderPhoto` (rapor / tools&parts / closing kategorili)
- `WorkOrderEvent` — audit trail (kim, ne zaman, ne yaptı) → **BRC izlenebilirlik**
- `WorkOrderClosure` — Root Cause, Corrective Action, Able to Repair (+sebep), Contractor Required (+ad), Downtime, Tools & Parts Accounted (+eksik açıklaması), Post-Deodorisation Intervention (Yes/No)
- `WorkOrderCost` — Parts Required, Price of Parts, Labour Hrs (otomatik), Labour Cost/Hr, PO Number
- `QaCheck` — swab test; Pass/Fail + zorunlu not; Fail → mühendise geri, **aynı kayıt güncellenir**, tekrar denenebilir (iterasyon sayacı)
- `SignOff` — ProductionSignOff (Area clean & tidy?, Released back into service?) + QaSignOff (intrusive ise)

**Tasks / Orders**

- `MaintenanceTask` (Daily / Weekend / ManagerAssigned, tarih, opsiyonel öncelik: Reactive/Corrective/Preventive), `TaskAssignment` (çoklu mühendis), `TaskCompletion` (not + foto)
- `PartOrderRequest` (parça, adet, acil mi, createdAt, orderedAt, status)

**Attendance / Holiday / Shift**

- `ClockEvent` (userId, type In/Out, occurredAt UTC, source Auto/Manual, lat/lon, accuracy, deviceId, syncedAt, clientGeneratedId)
- `OvertimeDeclaration` (tarih, süre, açıklama, onay durumu)
- `HolidayRequest` (start, end, workingDays, status, approvedBy, decidedAt)
- `ShiftType` (ad, başlangıç, bitiş, isActive — **silme yok, pasife alma var**)
- `ShiftAssignment` (user, date, shiftType, createdBy)

**Messaging**

- `Message` (sender, body, priority, createdAt), `MessageRecipient` (user veya department), `MessageRead`
- `DeviceToken` (FCM), `NotificationLog`

### İşler

- [ ] Yukarıdaki modeli `docs/DOMAIN.md` içinde tablo tablo yaz, **onaya sun**
- [ ] Entity + enum tanımlarını `CfiApp.Domain` içine yaz
- [ ] `IEntityTypeConfiguration<T>` ile Fluent API konfigürasyonları
- [ ] **Index planı**: her filtre/sıralama alanına index (status, createdAt, assignedEngineerId, userId+date …)
- [ ] **Concurrency token** (PostgreSQL `xmin`) — claim, assign, approve gibi yarış ihtimali olan tüm kayıtlarda
- [ ] **Soft delete** stratejisi: iş kaydı, clock event, holiday **hiç silinmez**; referans veriler (equipment vb.) pasife alınır
- [ ] **Veri saklama (retention) politikası**: BRC kayıtları ne kadar saklanacak, GPS verisi ne kadar sonra silinecek — alanları buna göre tasarla
- [ ] İlk migration + `dotnet ef database update`
- [ ] Seed: Units/Areas (site planından), roller, permission listesi, ilk admin (Maintenance Manager), 3 varsayılan shift (06–14 / 14–22 / 22–06) — **idempotent**, tekrar çalıştırılabilir

### SQL Server geçişini kolaylaştıran kurallar

- PostgreSQL'e özel tip kullanma (`jsonb`, `citext`, array) — gerekirse soyutlamanın arkasına al
- Tüm zamanlar **UTC + `DateTimeOffset`**
- `decimal(18,2)` gibi precision değerlerini açıkça belirt
- Raw SQL yazma; LINQ kullan
- `Guid` yerine `int` identity yeterli, ama offline sync için ayrıca `ClientId (Guid)` alanı ekle

**Model:** Şema tasarımı ve ilişkiler → **Opus**. Entity/configuration dosyalarını yazma → **Sonnet**.

---

## FAZ 2 — Auth, roller, admin CRUD

- [ ] `POST /api/auth/register` → hesap **Pending** durumunda oluşur (kullanıcı kendi parolasını belirler)
- [ ] Onay akışı: Operator/QA → ilgili manager; QA → şimdilik Maintenance Manager; yeni manager → Maintenance Manager (admin)
- [ ] Onay sırasında occupation + department + area ataması yapılır → hesap **Active**
- [ ] `POST /api/auth/login` → access token (15 dk) + refresh token (60 gün, rotating, veritabanında hash'li)
- [ ] `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/change-password`
- [ ] `GET /api/auth/me` → user + occupation + permission listesi
- [ ] **Permission bazlı authorization**: `[Authorize(Policy="workorder.assign")]` — rol string'i controller'a gömülmez
- [ ] **401 / 403 disiplini**: yetki reddi = 403 (asla 401). Frontend sadece 401'de logout eder
- [ ] Manager görünürlük filtresi: her sorgu `ManagerScope` bilgisine göre daraltılır
- [ ] Admin CRUD API: Department, Unit, Area, Line, Equipment, Occupation, ShiftType
- [ ] `AuditLog` middleware — kim ne değiştirdi (BRC), append-only
- [ ] **Parola politikası**: minimum uzunluk, yaygın parola listesi kontrolü, Argon2id veya ASP.NET Identity hasher
- [ ] **Hesap kilitleme** + login rate limit (kaba kuvvet saldırısına karşı)
- [ ] Parola sıfırlama akışı (email veya manager üzerinden — kararı Faz 2 başında ver)
- [ ] Refresh token **reuse detection**: çalınmış token tekrar kullanılırsa tüm oturum zinciri iptal edilir
- [ ] Hesap pasife alma → tüm refresh token'lar anında geçersiz (işten ayrılan çalışan)

### UK GDPR / çalışan verisi — üretimde zorunlu

Bu uygulama gerçek çalışanların **konumunu** ve **çalışma saatlerini** kaydediyor.
İngiltere'de bu, işveren tarafından yapılan bir izleme faaliyeti ve yasal yükümlülüğü var.

- [ ] Hangi verinin niçin toplandığı uygulama içinde çalışana **açıkça yazılı** (privacy notice)
- [ ] GPS **sadece geofence kararı için** kullanılır: sürekli iz kaydı tutulmaz, sadece "sahada mı, değil mi" + olay zamanı saklanır
- [ ] Konum verisi için **saklama süresi** ve otomatik silme
- [ ] Çalışan kendi kayıtlarını görebilir (erişim hakkı); düzeltme talebi için akış var
- [ ] Manager başka departmanın verisini göremez — teknik olarak engellenmiş, sadece UI'da gizlenmiş değil
- [ ] Firmaya teslimde: veri işleme sorumluluğunun kimde olduğu yazılı olsun

> Bu maddeler firmayla konuşulmalı — CFI'nin kendi İK/GDPR sorumlusu var mı, mevcut bir
> çalışan bilgilendirme metni var mı? Faz 2 başında sorulacak.

> **Eski projenin hatasını tekrarlama:** `HttpContext.Items["CurrentUser"]`, `X-UserId`,
> `RequireRoles<T>()` **hiç yazılmayacak**. Tek kaynak: `HttpContext.User` claims.

**Model:** **Opus** (güvenlik + policy mimarisi). Basit CRUD controller'ları Sonnet'e devret.

---

## FAZ 3 — Maintenance core (uygulamanın kalbi)

- [ ] **Rapor oluşturma — 3 varyant** (aynı endpoint, farklı input):
  - Operator: Line → Part (ikon grid) → açıklama + foto + severity
  - Engineer: Unit → Equipment listesi (+ "Other" serbest metin) → açıklama + foto + severity
  - Manager / QA: doğrudan **General Report** (line/part seçimi yok)
- [ ] Her rapor **Pool** listesine düşer — oluşturana otomatik atanmaz (mühendisin kendi raporu dahil)
- [ ] `POST /workorders/{id}/claim` — mühendis havuzdan alır; **aynı anda birden çok iş alabilir**
- [ ] `POST /workorders/{id}/assign` — manager doğrudan atar / yeniden atar
- [ ] `POST /workorders/{id}/notify` — "I'm Busy" / "On My Way" → raporlayana bildirim (İngilizce sabit metin)
- [ ] **Otomatik işçilik süresi** — claim anından kapanışa kadar sistem hesaplar, elle girilmez
- [ ] `WaitingParts` → iş kapanmış sayılır, operatöre **tek seferlik** bildirim; manager'lar kalıcı listede görür
- [ ] **Close Job** — Faz 1'deki `WorkOrderClosure` alanlarının tamamı
- [ ] Post-deodorisation = **Yes** → `AwaitingQa`; QA swab **Pass** → Closed, **Fail** (+zorunlu not) → mühendise geri, **aynı form** ön dolu açılır, tekrar gönderilir
- [ ] İmza: operatörün **kayıtlı tek seferlik imzası** kapanışa iliştirilir (her seferinde yeniden çizdirilmez, kullanıcı isterse değiştirir)
- [ ] Production Sign Off + (intrusive ise) QA Sign Off
- [ ] **History**: ay-yıl gruplu liste, This Month / This Year / All Time, arama (unit / ekipman / WO no), fotoğraflar, Breakdowns / Tasks sekmeleri — **sunucu tarafı sayfalama ve arama**, tümünü çekip tarayıcıda filtreleme yok
- [ ] Foto yükleme: `IFileStorage` arkasında lokal disk (ileride S3/Azure) + thumbnail

### Üretim gereği ek maddeler

- [ ] **Claim yarışı**: iki mühendis aynı anda aynı işi alırsa biri net bir hata görür — concurrency token ile
- [ ] **Durum makinesi merkezî**: geçerli geçişler tek yerde tanımlı, controller'a dağılmış `if` yok. Geçersiz geçiş 409 döner
- [ ] **Fotoğraf gerçeği**: "sınırsız foto" kararı korunuyor ama üretimde sınır lazım — dosya başına boyut limiti, sunucuda yeniden boyutlandırma/sıkıştırma, rapor başına makul üst sınır (örn. 20), disk kotası izleme. Telefon fotoğrafı 5 MB, günde 50 rapor = ayda 7 GB
- [ ] **Fotoğraflar da yedeklenir** — sadece veritabanı yedeği yeterli değil
- [ ] **İşçilik süresi kenar durumları**: mühendis işi açık bırakıp eve giderse ne olur? Vardiya sonunda otomatik duraklatma veya manager düzeltmesi — kural Faz 3'te netleşecek
- [ ] Kapanış formu QA Fail sonrası düzeltilirse **versiyonlanır**, önceki hali denetim için durur

**Model:** Durum makinesi, QA döngüsü, yetki kuralları → **Opus**. Liste/filtre/detay endpoint'leri → **Sonnet**.

---

## FAZ 4 — Web UI temeli

- [ ] Design system: Tailwind config'de token olarak palet (kahve `#6B4A2E`, yeşil `#4E7A3B`, sarı `#E7A93A`, krem `#F5EFE2`), tipografi ölçeği, kart/badge/button bileşenleri — renk kodu bileşene gömülmez, hep token kullanılır
- [ ] i18n altyapısı (EN/PL/BG/ES) — `react-i18next`, anahtar bazlı; backend enum değerleri frontend'de çevrilir
- [ ] `apiClient` (axios) — request interceptor: Bearer; response interceptor: **401 → refresh dene → olmazsa logout**, 403 → sadece hata göster
- [ ] `AuthContext` + `RequireAuth` + `RequirePermission` (rol string'i değil, permission)
- [ ] `AppLayout` — sol menü, kullanıcı bilgisi, **dil seçici, clock durumu**
- [ ] **Dil seçici TÜM rollerde var** — Operator, Engineer, QA, Production Manager, Maintenance Manager. Sadece operatöre özel değil. Seçim kullanıcı profiline kaydedilir, cihaz değişse de korunur ve mobilde aynı dil gelir
- [ ] Her ekranda **yükleniyor / boş / hata** durumu tanımlı (üretim şartı — beyaz ekran yok)
- [ ] Rol bazlı ana ekranlar (prototiplerdeki akış):
  - **Engineer / Manager:** My Active Jobs + Sent Back By QA + Create Report + Pool
  - **Manager ek:** Factory Overview (Open Jobs / In Pool / Awaiting-Failed QA / Closed / Open Tasks — tıklanabilir, filtreli liste)
  - **Operator:** doğrudan Report Breakdown ana ekranı
  - **QA:** Swab test kuyruğu + General Report
  - **Production Manager:** Breakdown listesi + team shifts + messages + holiday onay
- [ ] Web'de liste/tablo görünümü mobilden farklı olabilir (masaüstü daha geniş), akış aynı kalır

**Model:** Design system + routing/permission mimarisi 1 kez **Opus**. Sonraki her ekran → **Sonnet** ("şu ekranı X ekranını örnek alarak yaz").

---

## FAZ 5 — Tasks + Orders

- [ ] Task oluşturma **sadece Maintenance Manager**; çoklu mühendise atanabilir; silinebilir; atanan değiştirilebilir
- [ ] Daily / Weekend / Manager-assigned grupları, opsiyonel öncelik (Reactive / Corrective / Preventive)
- [ ] Tamamlama: kısa açıklama + opsiyonel foto + Done. Bildirim yok
- [ ] Görünürlük: açık task'ı sadece atanan görür, **tamamlanmış task'ı herkes görür**
- [ ] Orders: mühendis kendi talebini oluşturur/görür/siler; manager hepsini görür/siler/"ordered" işaretler; en yeni üstte; ordered olunca listeden düşer; bildirim yok

**Model:** Sonnet.

---

## FAZ 6 — Attendance + Holiday

- [ ] Gerçek zaman damgası tek (planlanan/gerçek saat ayrımı yok)
- [ ] **Otomatik Clock In/Out:** GPS geofence — fabrika koordinatı + yarıçap (parametre, ~200–500 m, admin panelden ayarlanır)
- [ ] **Tolerans:** kısa sinyal kopması Clock Out sayılmaz (ör. 10 dk içinde aynı bölgede tekrar görülürse aynı oturum) — parametre
- [ ] **Manuel Clock In/Out** her zaman mümkün, `source=Manual` olarak damgalanır
- [ ] Overtime ayrı ekran: çalışan beyan eder, gerçek clock kayıtlarıyla karşılaştırılabilir
- [ ] Aylık saat özeti + takvimle geçmiş aylara bakma
- [ ] Manager sadece kendi department/area çalışanlarının saatlerini görür
- [ ] Holiday: çalışan talep (start/end → otomatik iş günü hesabı), manager Requests/Calendar sekmeleri, Approve/Reject, mini takvim, aylık takvim (yeşil onaylı / sarı bekleyen)

### Üretim gereği ek maddeler — bu modül maaşı etkiliyor, hata affetmez

- [ ] **Saat dilimi**: veritabanı UTC, gösterim `Europe/London`. **Yaz saati (BST) geçişi** kritik: 22:00–06:00 vardiyası yılda iki kez 7 veya 9 saat olur. Bu iki gün için test yazılacak
- [ ] **Cihaz saati güvenilmez**: offline clock kaydında hem cihaz saati hem sunucuya ulaşma saati saklanır; aradaki fark eşiği aşarsa kayıt **şüpheli** işaretlenir, manager görür
- [ ] **GPS güvenilmez**: doğruluk (accuracy) değeri saklanır; kötü sinyalde otomatik kayıt yapılmaz, kullanıcıya manuel seçenek sunulur. Sahte konum (mock location) tespiti Android'de kontrol edilir
- [ ] **Düzeltme akışı**: yanlış kayıt silinmez, **düzeltme kaydı** eklenir; kim düzeltti, ne zaman, neden — hepsi audit'te
- [ ] **Resmi tatiller** (UK bank holidays) iş günü hesabına dahil edilmez — takvim kaynağı Faz 6'da kararlaştırılacak
- [ ] Aylık kapanış: maaş dönemi geçtikten sonra o ayın kayıtları kilitlenir mi? Faz 6 başında sorulacak
- [ ] Aynı gün çift Clock In / eksik Clock Out senaryoları tanımlı davranışa sahip — sessizce bozulmaz

**Model:** Geofence + tolerans + zaman/timezone kuralları → **Opus**. Form/liste ekranları → **Sonnet**.

---

## FAZ 7 — Shift Planning

- [ ] Varsayılan 3 shift (06:00–14:00 / 14:00–22:00 / 22:00–06:00), saatler düzenlenebilir, yeni tip eklenebilir, **silme yok → aktif/pasif toggle**
- [ ] Web: **drag & drop planner** — solda çalışanlar, tabloda günler × shift'ler, sürükle-bırak ata, haftalar arası gezinme, birkaç hafta/ay ileri planlama
- [ ] Mobil (Maintenance Manager): basit akış — tarih seç → shift seç → çalışan seç → ekle/çıkar
- [ ] Çalışan görünümü: bugünkü + sıradaki shift'ler, tarih/saat
- [ ] Shift oluşturma yetkisi web'de tüm manager'larda; mobilde şimdilik sadece Maintenance Manager'da

> **Netleşmesi gereken (Faz 7 başında sorulacak):** rotating shift var mı, 4-on/4-off,
> molalar, yerine geçme (swap), worker değişiklik talebi + onay, absence/holiday çakışması.

**Model:** Veri modeli + drag & drop state yönetimi → **Opus**. Ekranlar → **Sonnet**.

---

## FAZ 8 — Messages + Push Notifications

- [ ] Manager mesaj oluşturur: alıcı = tek çalışan / birden çok çalışan / **Department**
- [ ] Mesaj sistemde kalıcı (Inbox), okundu/okunmadı, tarih, gönderen, priority
- [ ] Push: Firebase Cloud Messaging (ücretsiz) — `DeviceToken` kaydı, mobilde bildirim, web'de opsiyonel
- [ ] **Ayrım:** Message = içerik (sistemde durur), Notification = uyarı (geçici)
- [ ] Sistem bildirimleri de aynı altyapıyı kullanır: "engineer is on the way", "waiting for parts", "holiday approved", "QA failed"

**Model:** FCM entegrasyonu 1 kez **Opus**, gerisi **Sonnet**.

---

## FAZ 9 — Admin panel + BRC / raporlama

- [ ] Admin panel UI: Department / Unit / Area / Line / Equipment / Occupation / Role-Permission / ShiftType CRUD, geofence parametreleri
- [ ] Ekipman ikon kütüphanesi (30–40 endüstriyel ikon) + "kendi fotoğrafını yükle" seçeneği
- [ ] **BRC denetim modu:** tarih/ekipman bazlı arama, saniyeler içinde kaydı aç, tam audit trail (kim – ne zaman – ne)
- [ ] Fault Reporting Log **PDF/print çıktısı** (kağıt formla birebir uyumlu, fallback olarak)
- [ ] Basit yönetim raporları: downtime toplamı, ekipman bazlı arıza sayısı, açık/kapalı iş sayıları
- [ ] **Veri dışa aktarma** (CSV/Excel) — denetçi kendi kopyasını isteyebilir
- [ ] **PPM kararı:** üretimde boş ekran olmayacağı için ikisinden biri:
  **(a)** PPM bu fazda gerçekten yapılır (aylık plan, ekipman bazlı periyot, atama, tamamlama, gecikme uyarısı), veya
  **(b)** menüden tamamen çıkarılır, sonraki sürümde eklenir.
  **Karar Faz 9 başında verilecek** — CFI'de şu an PPM nasıl takip ediliyor, önce o sorulacak

**Model:** Sonnet. PPM (a) seçilirse periyot/tekrar mantığı için **Opus**.

---

## FAZ 10 — Production hardening + deployment

Bu faz MVP planında "2 oturumluk deploy" idi. Üretim uygulamasında **ayrı bir iş kalemi**:
uygulama fabrikada çalışırken gece 03:00'te bozulursa ne olacağının cevabı burada.

**Altyapı**

- [ ] Ubuntu sunucu: Docker Compose (api + postgres + nginx)
- [ ] **İki ortam: staging + production.** Hiçbir şey doğrudan production'a çıkmaz, önce staging'de gerçek veri kopyasıyla denenir
- [ ] nginx reverse proxy + Let's Encrypt HTTPS + otomatik yenileme
- [ ] Log rotation, restart policy, kaynak limitleri

**Veri güvenliği**

- [ ] PostgreSQL otomatik yedek (günlük dump + retention) **+ fotoğraf dosyaları da yedeklenir**
- [ ] **Yedekten geri yükleme tatbikatı yapılır ve belgelenir** — denenmemiş yedek, yedek değildir
- [ ] Yedekler sunucunun dışında da bir kopyada durur
- [ ] **Zero-downtime migration disiplini**: önce kolon ekle → kod iki şemayla da çalışsın → sonra eski kolonu kaldır. Vardiya ortasında tablo kilitlenmez

**Görünürlük**

- [ ] Hata takibi (Sentry veya self-hosted GlitchTip — ücretsiz seçenek var)
- [ ] Uptime izleme + uyarı: API düşerse telefona bildirim gelir
- [ ] Structured log toplama (Seq veya dosya + grep — bütçeye göre)
- [ ] Temel metrikler: istek süresi, hata oranı, DB bağlantı havuzu

**Güvenlik**

- [ ] `/security-review` ve **Opus ile tam gözden geçirme** — canlıya çıkmadan önce
- [ ] Bağımlılık taraması (`dotnet list package --vulnerable`, `npm audit`) CI'da
- [ ] Secret rotasyonu mümkün: JWT key değişince sistem çalışmaya devam eder
- [ ] Sunucu sertleştirme: SSH key-only, firewall, otomatik güvenlik güncellemeleri

**Devreye alma**

- [ ] Geri dönüş planı: kötü sürüm çıkarsa nasıl geri alınır, yazılı
- [ ] Bakım penceresi ve duyuru akışı (uygulama içi mesaj modülüyle)
- [ ] Kağıt forma geri dönüş planı: sistem çökerse fabrika duramaz, elle form doldurup sonra girme akışı tanımlı olmalı

**Model:** Altyapı **Sonnet**, güvenlik gözden geçirmesi ve migration stratejisi **Opus**.

---

## FAZ 11 — React Native (önce Android)

- [ ] Proje iskeleti, ortak `apiClient`, aynı i18n anahtarları
- [ ] **Offline-first mimari:** SQLite lokal veritabanı + outbox (pending operations) kuyruğu
- [ ] Sync: `clientGeneratedId` ile idempotent gönderim, sunucu **source of truth**, conflict kuralları
- [ ] Offline kaydedilebilecekler: Clock In/Out, breakdown raporu (+foto), task tamamlama, order talebi
- [ ] Arka planda geofence takibi + izinler (Android background location)
- [ ] FCM push, deep link (bildirime tıkla → ilgili ekran)
- [ ] Ekranlar: prototiplerdeki 5 rol akışı (Operator, Engineer, Maintenance Manager, QA, Production Manager) — **hepsinde dil seçici**

### Üretim gereği ek maddeler

- [ ] **Sync testleri gerçekten yazılır**: uçak modunda 20 kayıt biriktir → bağlan → hepsi tek seferde ve tekrarsız gitsin. Bu modülün testi olmadan canlıya çıkılmaz
- [ ] **Kuyruk kalıcı**: uygulama kapatılsa, telefon yeniden başlatılsa bile bekleyen kayıtlar kaybolmaz
- [ ] **Çakışma kuralları yazılı**: aynı işi offline'da iki kişi kapatırsa ne olur, sunucu hangisini kabul eder, kullanıcı ne görür
- [ ] **Kullanıcı senkronizasyon durumunu görür**: "3 kayıt gönderilmeyi bekliyor" — sessizce beklemez
- [ ] **Şema sürümü**: eski mobil sürüm yeni API ile konuşabilir; zorunlu güncelleme ekranı var
- [ ] Foto yükleme: büyük dosya, kopan bağlantı, kısmi yükleme — devam ettirilebilir veya güvenle tekrarlanabilir
- [ ] Pil tüketimi ölçülür — arka plan konum takibi telefonu bitirirse çalışan uygulamayı kapatır, sistem çöker
- [ ] Play Store hesabı, imzalama anahtarı, sürüm yönetimi, gizlilik politikası (konum izni için Google zorunlu tutar)

**Model:** Offline sync + conflict mimarisi → **Opus** (projenin en zor teknik parçası). Ekranlar → **Sonnet**.

---

## FAZ 12 — iOS, tablet, saha devreye alma

- [ ] iOS build + TestFlight → App Store, iOS konum izinleri ve gizlilik beyanı
- [ ] Ortak saha tableti arayüzü (Android tablet) — paylaşılan cihazda oturum yönetimi ayrı düşünülecek
- [ ] **Kullanıcı eğitimi**: her rol için kısa kılavuz, 4 dilde. Uygulama ne kadar sade olsa da ilk gün desteği gerekir
- [ ] **Aşamalı geçiş**: önce bir vardiya/bir departman, kağıt formla paralel çalışma, sonra tam geçiş
- [ ] Destek akışı: sorun çıkınca çalışan kime söyleyecek, siz nasıl göreceksiniz (hata takibi + log)
- [ ] Devreye alma sonrası ilk hafta günlük kontrol

---

## Sonraki (sürüm 1 sonrası)

- **PPM** — Faz 9'da yapılmazsa buraya kayar (menüde boş ekran olarak durmaz)
- **Incident Reporting**
- **FLT Driver** rolü detayları
- Production / Products / Warehouse / Stock / Batch tracking — **önce departmanlarla konuş**, önden tasarlama

---

## 3. Model kullanım politikası (token verimliliği)

### Genel kural

| Ne zaman | Model |
|---|---|
| Mimari, veri modeli, güvenlik, durum makinesi, offline sync, "nasıl yapalım" tartışması | **Opus** |
| Karmaşık hata ayıklama, kod review, refactor kararı | **Opus** |
| Var olan bir desenin tekrarı (yeni controller, yeni ekran, yeni DTO) | **Sonnet** |
| CRUD, form, liste, i18n çevirileri, test yazımı, migration | **Sonnet** |
| Yeniden adlandırma, format, tek satırlık düzeltme | **Haiku** |

### Pratik çalışma şekli — "Opus tasarlar, Sonnet çoğaltır"

1. Faz başında **Opus** ile: tasarımı konuş, kararı `docs/` altına yaz, **ilk referans dosyayı** Opus yazsın (örn. ilk controller, ilk React sayfası) — testiyle birlikte, çünkü test de kopyalanacak desendir.
2. Sonra `/model sonnet` ile geç ve şunu de: *"WorkOrdersController dosyasını örnek alarak TasksController yaz, testlerini de aynı şekilde."* Sonnet desen kopyalamada Opus kadar iyi ve çok daha ucuz.
3. Faz sonunda tekrar **Opus** ile gözden geçir: `/code-review high`.
4. Auth (Faz 2), Attendance (Faz 6), mobil sync (Faz 11) ve deploy (Faz 10) fazlarında ayrıca **`/security-review`**.

> **Üretim uygulamasında gözden geçirme adımı atlanmaz.** MVP'de "sonra bakarız" denilebilir;
> burada faz sonu review, fazın parçasıdır. Sonnet ile 5 dosya yazıp Opus ile 1 kez gözden
> geçirmek, her şeyi Opus'a yazdırmaktan hem ucuz hem daha iyi sonuç verir.

### Token tasarrufu kuralları

- Her faz için **yeni oturum** aç, `/clear` kullan. Uzun context = pahalı context.
- Kararlar `CLAUDE.md` ve `docs/DECISIONS.md` içinde özetli — müşteri dokümanlarını oturuma taşımaya gerek yok (zaten depoda değiller).
- Tek seferde 10 dosya değil, **1–3 dosya** üzerinde çalış.
- Bir ekranın davranışı belirsizse kullanıcıya 5 satırla tarif ettir; prototip dosyaları depoda yok.
- Plan modunu (`Shift+Tab`) kullan: önce plan çıksın, onaylayınca kod yazılsın — yanlış yöne gidip token yakmaz.

---

## 4. Faz başına netleşmesi gereken sorular

Aşağıdakiler ilgili fazın **başında** sorulacak, önceden varsayım yapılmayacak:

**Faz 1 (veri modeli)**

- Department listesi gerçekte nedir? (Production, Maintenance, QA, Warehouse, …)
- Site planındaki oda/ekipman listesi seed verisine aynen girsin mi?

**Faz 3 (maintenance)**

- "Contractor Used" dropdown mu serbest metin mi? *(karar: serbest metin — teyit edilecek)*
- Severity seçeneklerinin tam listesi (şu an: üretimi durduruyor / küçük sorun)

**Faz 2 (auth / veri koruma)**

- CFI'nin çalışan verisi için mevcut bir gizlilik bildirimi / İK süreci var mı?
- Parola sıfırlama: email ile mi, manager üzerinden mi?

**Faz 6 (attendance)**

- Fabrikanın GPS koordinatı + yarıçap kaç metre?
- Yanlış clock kaydını kim düzeltebilir, manager onayı gerekir mi?
- Maaş dönemi kapandıktan sonra o ayın kayıtları kilitlenecek mi?
- UK resmi tatil takvimi nereden gelecek?

**Faz 7 (shift)**

- Kaç shift tipi, rotating var mı, 4-on/4-off var mı, overtime/mola kuralları?
- Worker değişiklik talep edebilir mi, onay gerekli mi?

**Faz 9 (PPM)**

- CFI'de PPM şu an nasıl takip ediliyor? Yapılacak mı, yoksa menüden çıkarılıp sonraki sürüme mi bırakılacak?

**Faz 10 (devreye alma)**

- Sunucu nerede duracak: evdeki Ubuntu mu, fabrikada bir makine mi, bulut mu? (Üretimde çalışacaksa erişilebilirlik ve yedek sorumluluğu değişir)
- Sistem çökerse fabrikanın kağıt forma dönüş planı ne?

---

## 5. Durum takibi

| Faz | Durum | Tarih |
|---|---|---|
| 0 | 🟩 **Tamamlandı** | 2026-09-04 |
| 1 | 🟩 **Tamamlandı** — 42 tablo, 64 test | 2026-09-04 |
| 2 | 🟩 Tamamlandı | 2026-09-04 |
| 3 | 🟩 Tamamlandı | 2026-09-04 |
| 4 | ⬜ Kullanıcı girdisi gerekiyor | |
| 5 | 🟩 Tamamlandı | 2026-09-04 |
| 6 | 🟩 Tamamlandı | 2026-09-04 |
| 7 | 🟩 Tamamlandı | 2026-09-04 |
| 8 | 🟩 Tamamlandı (backend, gerçek push hariç) | 2026-09-04 |
| 9 | 🟨 Kısmi (CSV export) | 2026-09-04 |
| 10 | 🟨 Kısmi (Docker altyapısı doğrulandı, domain/TLS/sunucu kullanıcı girdisi bekliyor) | 2026-09-04 |
| 11+ | ⬜ Sıradaki | |

### Faz 0 — ne yapıldı (2026-09-04)

Doğrulanmış çıktılar:

- `backend/CfiApp.slnx` — Domain / Application / Infrastructure / Api + Tests, .NET 10
- EF Core 10.0.11 + Npgsql 10.0.3, `CfiAppDbContext` (entity'ler Faz 1'de gelecek)
- `Directory.Build.props`: nullable açık, **TreatWarningsAsErrors** — build 0 uyarı ile geçiyor
- Serilog (konsol + günlük dosya, 30 gün saklama) + correlation id middleware
- ProblemDetails (RFC 7807), API versioning (`/api/v1/...`), rate limiting (global + `auth` politikası hazır)
- `/health/live` (veritabanına dokunmaz) ve `/health/ready` (PostgreSQL kontrolü, bağlantı bilgisi sızdırmaz)
- **8 entegrasyon testi geçiyor** — Testcontainers ile gerçek PostgreSQL 16, in-memory provider yok
- `web/` — Vite + React (JS) + Tailwind v4, CFI paleti token olarak, axios client + correlation id
- **Dil seçici 4 dilde çalışıyor** (EN/PL/BG/ES) + `npm run check:i18n` eksik anahtar kontrolü
- GitHub Actions CI: build + test + lint + i18n kontrolü + NuGet/npm güvenlik açığı taraması
- Veritabanı `cfiAppDb` oluşturuldu (PostgreSQL 16.13, port 5434)

Elle doğrulandı: `/health/ready` → `{"status":"Healthy","checks":[{"name":"postgres","status":"Healthy"}]}`

### Faz 1 — tamamlandı (2026-09-04)

> Detaylı günlük: [`PROGRESS.md`](PROGRESS.md)

- `docs/DOMAIN.md` — **tüm modüllerin tablo tasarımı, onay bekliyor** (8 açık soru listelendi)
- `Domain/Common/Entity.cs` — `IAuditable`, `IDeactivatable`, `IAppendOnly` sözleşmeleri
- `AuditableEntityInterceptor` — `CreatedAt`/`CreatedByUserId` otomatik damgalanır, **sonradan değiştirilemez**; `IAppendOnly` tablolarda update/delete **exception** atar
- `IClock` / `ICurrentUser` soyutlamaları — yaz saati (BST) geçişi test edilebilir olsun diye
- Organizasyon modülü tam: `Unit`, `Area`, `Line`, `Equipment`, `Department`, `Occupation` + EF konfigürasyonları
- Tüm `IAuditable` tablolara otomatik **concurrency token** (PostgreSQL `xmin`)
- İlk migration alındı ve `cfiAppDb` üzerine uygulandı (7 tablo)
- **Yerleşim hiyerarşisi düzeltildi: `Area → Unit → Equipment`** (Area en üst seviye, resmi Fault Reporting Log formundaki "Area" alanı)
- `Priority` enum'ı: `Low / Medium / High / Critical` (karar verildi)
- **Kimlik/yetki modülü tam**: `User`, `UserArea`, `Role`, `Permission`, `RolePermission`, `ManagerScope`, `RefreshToken`, `UserSignature`, `MediaAsset`
- `Permissions.cs` — 31 permission anahtarı ve rol→yetki haritası **kodda tek kaynak**; seed veritabanına yazar, test ikisinin uyumunu doğrular
- Yetki dağılımı: MaintenanceManager 26, ProductionManager 17, Engineer 14, QA 11, Operator 8
- `ManagerScope` üzerinde CHECK constraint: departman veya alan, tam biri dolu olmak zorunda
- `UserSignature` filtreli unique index: kullanıcı başına tek geçerli imza, eskiler `ReplacedAt` ile durur
- `DatabaseSeeder` — sadece eksik olanı ekler, **var olanın üzerine asla yazmaz**. Seed içeriği: `Unit 1/2/3`, `Yard`, departmanlar `Production` ve `Maintenance`
- Migration ve seed **startup yan etkisi değil**: `Database:ApplyMigrationsOnStartup` / `RunSeedOnStartup` bayrakları production'da kapalı, sadece Development'ta açık
- **23 test geçiyor**: audit damgası, geçmişin üzerine yazılamaması, eşzamanlı düzenlemede çakışma, referans verinin silinememesi, migration'ın boş veritabanına uygulanması, seed'in iki kez çalışınca kopyalamaması, seed'in yönetici düzenlemesini geri almaması, manager'ın engineer'ı kapsaması, operatörün iş üstlenememesi, admin yetkisinin tek rolde olması, QA/Production sign-off ayrımı

**Maintenance modülü tam:** `WorkOrder`, `WorkOrderPhoto`, `WorkOrderEvent` (append-only),
`WorkOrderClosure` (versiyonlu), `WorkOrderCost`, `QaCheck`, `SignOff` + `WorkOrderStateMachine`.

Veritabanı seviyesinde zorlanan kurallar (7 CHECK constraint + filtreli unique index):

- Bir rapor ya ekipman seçer ya serbest metin yazar — ikisi de boş olamaz
- Offline'dan aynı rapor iki kez gelirse ikincisi reddedilir (`ClientId` unique)
- `WorkOrderEvent` güncellenemez ve silinemez — denemek exception atar
- QA "Fail" derse not zorunlu
- "Tamir edemedim" derse gerekçe, "kontraktör gerekti" derse isim, "eksik var" derse açıklama zorunlu
- İş başına tek Production sign-off, tek QA sign-off
- Kapanış formu düzeltilirse yeni `Version` satırı olur, öncekiler durur

**54 test geçiyor** (23 → 54). Veritabanında 23 tablo.

**Kalan tablolar:** task/order, attendance, holiday/shift, messaging, audit —
hepsi yukarıdaki desenden çoğaltılacak (Sonnet işi).
