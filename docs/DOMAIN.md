# CFI App — Veri Modeli (Faz 1)

> **Durum: onaylandı, uygulanıyor.** Organizasyon modülü kodlandı ve migration alındı.
> Kalan bölüm 10'daki maddeler varsayımla ilerliyor, ilgili fazda teyit edilecek.

Kaynak kurallar: [`DECISIONS.md`](DECISIONS.md). Üretim standardı: [`ROADMAP.md`](ROADMAP.md#2b-üretim-standardı--her-faz-için-bitti-tanımı-definition-of-done).

---

## 0. Tüm tablolarda geçerli kurallar

| Kural | Gerekçe |
|---|---|
| Birincil anahtar `int` identity (`Id`) | Okunabilir, index'i küçük. Dış dünyaya açılan iş kayıtlarında ayrıca insan-okur numara var (`WorkOrder.Number`) |
| Tüm zaman alanları `timestamptz` / `DateTimeOffset`, **UTC** | Mesai ve vardiya doğruluğu buna bağlı. Yaz saati (BST) geçişi yılda iki kez sorun çıkarır |
| `CreatedAt`, `CreatedByUserId` — değişebilen kayıtlarda ayrıca `UpdatedAt`, `UpdatedByUserId` | BRC izlenebilirlik |
| Çekişme ihtimali olan kayıtlarda `RowVersion` (PostgreSQL `xmin`) | İki mühendis aynı işi claim edemesin |
| **Silme yok.** İş kayıtları hiç silinmez; referans veriler `IsActive=false` olur | Denetim kaydı silinemez; geçmiş iş emri silinen ekipmana referans vermeye devam etmeli |
| Offline'dan gelebilen kayıtlarda `ClientId` (Guid, unique) | Aynı kayıt iki kez gönderilirse ikincisi sessizce yok sayılır (idempotent sync) |
| Para alanları `decimal(18,2)`, süreler **dakika** (`int`) | Provider bağımsız, yuvarlama sürprizi yok |

---

## 1. Organizasyon ve yerleşim

Hepsi admin panelinden yönetilir. **Hiçbiri kodda sabit değildir.**

### Department
`Id, Name, IsActive, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId`

Seed: **Production**, **Maintenance**, **FLT**, **QA**. Kalanlar admin panelinden eklenecek.

Departman roldan türetilir, onay formunda elle seçilmez (`Permissions.RoleDepartments`).
FLT sürücüleri kendi departmanları — hat işletmiyorlar, sahanın iki tarafına da mal
taşıyorlar, Production'ın altına koymak onları ait olmadıkları bir ekibe yazmak olurdu.
**QA da kendi departmanı** (2026-09-16): önceden Maintenance altındaydı, tek sebebi onları
onaylayacak ve hesap verecekleri başka kimsenin olmamasıydı. Artık QA Manager var; orada
bırakmak QA Manager'a bütün mühendisleri, Maintenance Manager'a bütün QA'leri kendi ekibi
olarak gösterirdi.

> **Hiyerarşi: `Unit → Area (oda) → Line → Equipment → Equipment (parça)`.**
> En üst seviye **Unit**'tir (Unit 1/2/3, Yard) — resmi Factory Equipment Fault Reporting
> Log formundaki "Unit" alanı da budur. **Area** bir unit içindeki odadır (Filling Room,
> Plant Room, Packing). **Line** yalnızca Filling Room'da var. **Equipment** makinedir; bir
> makinenin *parçası* da yine Equipment'tır, üstündeki makineye bağlı.
> *(2026-09-04: isimlendirme baştan tersti, düzeltildi. 2026-09-07: fabrikanın verdiği
> gerçek liste seed'e girdi, site plan HTML'inden türetilen eski odalar silindi.)*

### Unit (en üst seviye)
`Id, Name, Code (unique), DisplayOrder, IsActive`

Seed: **Unit 1**, **Unit 2**, **Unit 3**, **Yard**.

### Area (Unit'in içindeki oda)
`Id, UnitId → Unit, Name, Code?, DisplayOrder, IsActive, IsWorkArea`

`IsWorkArea` (2026-09-16), `IsActive`'den ayrı bir alan: **oda var mı** değil, **kişi buraya
atanabilir mi**. P Tanks Room, Boiler House ve Office'te istasyonlanmış kimse yok, o yüzden
"works in" (onay) formunda çıkmıyorlar — ama arıza bildirme formunda hâlâ seçilebilirler,
çünkü Boiler House'daki kazan yine bozulabilir. Varsayılan `true`; admin panelinden tek tık
geçişle değiştirilebilir.

Seed — **Unit 1:** Filling Room, P Tanks Room, Plant Room, Melting Room, Packing,
Warehouse, Boiler House, Office. **Unit 2:** Blending Room, Warehouse.
**Unit 3:** Warehouse, Workshop (henüz makineleri girilmedi).
**Yalnızca Yard'ın odası yok** — dışarısı; kapılar ve DAF Plant doğrudan unit'te duruyor.

Çalışan ataması bu seviyede yapılır (`UserArea`). Bunun bir sonucu var: **odası olmayan bir
unit'e kimse atanamaz** — Yard'da düzenli çalışan biri (kapı, FLT) olacaksa oraya bir oda
açmak gerekir.

### Line
`Id, UnitId → Unit, AreaId? → Area, Name, DisplayOrder, IsActive`

Seed — Filling Room içinde: Line 1 (2kg), Line 2, Line 3, Line 4 (Box Line),
**Inkjet Printer**.

Inkjet Printer bir makine değil, hat seviyesinde bir istasyon (2026-09-16'da düzeltildi):
diğer dört hatla aynı seviyede duruyor ve altına makineleri sonra eklenecek. Line 1 (2kg)
gibi şu an altı boş.

Hattın kendi başına var olma sebebi: Line 2 ve Line 3'ün **ikisinde de** Seamer ve Conveyor
var. Hat seçilmezse arıza formu hangisinin bozulduğunu söyleyemez. Bu yüzden arıza formunda
hat adımı, sadece seçilen odada hat varsa görünür.

### Equipment (makine ve makine parçası)
`Id, UnitId → Unit, AreaId? → Area, LineId? → Line, ParentEquipmentId? → Equipment,
Name, IconKey?, PhotoAssetId? → MediaAsset, DisplayOrder, IsActive`

- `AreaId` null → makine odada değil, doğrudan unit'te duruyor (Yard'daki kapılar, DAF Plant)
- `LineId` yalnızca Filling Room'daki hat makinelerinde dolu
- `ParentEquipmentId` → **makinenin parçası**: Blender 2'nin FIBC1 ve FIBC2'si.
  **Tek seviye derinlik**: parçanın parçası olmaz — o bir yedek parça kataloğu olurdu ve
  arıza formunda gösterilecek yeri yok. Parça, makinesiyle **aynı yerde** durmak zorunda
  (aynı unit/oda/hat); API bunu 400 ile reddeder, yoksa tek tamir iki farklı yer adı ile
  kayda geçer.
- Seed: **62 makine** (2'si parça). Liste başlangıç noktası — gerisi admin panelinden.

### Occupation
`Id, Name, IsActive`

Operator, Engineer, QA, FLT Driver, Packer … Rolden ayrıdır: rol *yetkiyi*, occupation
*görevi* anlatır.

**Index:** `Unit(Code)` unique, `Area(UnitId, IsActive)`, `Area(UnitId, Name)` unique,
`Equipment(UnitId, IsActive)`, `Equipment(AreaId, IsActive)`, `Equipment(LineId, IsActive)`,
`Equipment(ParentEquipmentId, IsActive)`

---

## 2. Kimlik ve yetki

### User
```
Id, FullName, Email (unique, lowercase), PhoneNumber,
PasswordHash, SecurityStamp,
Status: UserStatus,
OccupationId? → Occupation,
DepartmentId? → Department,
PreferredLanguage (en|pl|bg|es, varsayılan en),
CreatedAt, ApprovedAt?, ApprovedByUserId?, DisabledAt?, DisabledByUserId?,
FailedLoginCount, LockoutEndsAt?,
RowVersion
```

`UserStatus`: `PendingApproval = 0`, `Active = 1`, `Disabled = 2`

- Kayıtta kullanıcı **kendi parolasını** belirler, hesap `PendingApproval` doğar.
- Onaylayan manager rol + area(lar) atar → `Active`. Department roldan türetilir, elle seçilmez.
- `SecurityStamp` değişince tüm refresh token'lar geçersizleşir (işten ayrılan çalışan).
- `PreferredLanguage` **her rolde** var — dil seçimi cihazda değil hesapta saklanır.

### UserArea
`UserId → User, AreaId → Area` (bileşik PK)

Çalışanın çalışma alanları. Atama **area (oda) bazında** — makine bazında değil, unit bazında da değil:
bir area zaten hangi unit'te olduğunu söylüyor.

### Role / Permission / RolePermission
```
Role:           Id, Name, IsSystem
Permission:     Id, Key ("workorder.assign"), Description
RolePermission: RoleId, PermissionId
User.RoleId → Role
```

Kullanıcı başına **tek rol** (karar verildi). Maintenance Manager'ın Engineer'ı kapsaması
çoklu rolle değil, rolüne Engineer permission'larının da verilmesiyle çözülüyor — daha basit
ve yetki hesabı tek sorguda çıkıyor.

Roller *(2026-09-16)*: `Operator`, `Supervisor`, `FltDriver`, `Engineer`,
`MaintenanceManager` (admin), `QA`, `QaManager`, `ProductionManager`.

`Supervisor` ve `FltDriver` **şimdilik sadece eklendi**: ikisi de operatörün taban yetkisiyle
başlıyor (giriş-çıkış, arıza bildir, izin talebi). Ne yapabilecekleri henüz kararlaştırılmadı;
sonradan alınacak bir yetkiyi geri almak, hiç verilmemiş olandan zordur. İkisini de
**Production Manager** onaylar — sahada çalışıyorlar, bakımın altında değiller.

`QaManager`, QA'nin üstü — Maintenance Manager'ın Engineer'ı kapsaması gibi QA'yi kapsıyor:
swab da yapar, ekibi de yönetir. `admin.manage` **almıyor**; QA'yi yönetmek sahayı yönetmek
değil. İzin talebi de yok (menejerler izni onaylar, buradan talep etmez).

**Onay zinciri** (`Permissions.ApprovableRoles`) — her menejer kendi insanını alır:

| Onaylayan | Kimi içeri alabilir |
|---|---|
| Maintenance Manager (admin) | Engineer, MaintenanceManager, **QaManager**, ProductionManager |
| QA Manager | QA |
| Production Manager | Operator, Supervisor, FltDriver |

Admin artık QA'leri **doğrudan onaylamıyor**: QA Manager'ı içeri alır, kimin QA işi yapacağına
QA Manager karar verir. Haritada olmayan bir rol kimseyi onaylayamaz — güvenli varsayılan bu:
`user.approve` yetkisi yanlışlıkla bir role verilse o rol sessizce admin olmaz.

Permission anahtarları modül.eylem biçiminde: `workorder.create`, `workorder.claim`,
`workorder.assign`, `workorder.close`, `qa.signoff`, `task.create`, `order.manageAll`,
`shift.plan`, `holiday.approve`, `attendance.viewTeam`, `message.send`, `admin.manage`,
`user.approve`.

### ManagerScope
`Id, UserId → User, DepartmentId? → Department, UnitId? → Unit`

Bir manager hangi departman/alanlardan sorumlu. **Tüm liste sorguları bu tabloya göre daraltılır** —
UI'da gizlemek yeterli değil, sorgu seviyesinde uygulanır.

### RefreshToken
`Id, UserId, TokenHash (SHA-256), DeviceId, CreatedAt, CreatedByIp, ExpiresAt, RevokedAt?, ReplacedByTokenId?`

- Ham token asla saklanmaz.
- **Rotating**: her yenilemede eskisi iptal, yenisi verilir.
- **Reuse detection**: iptal edilmiş bir token tekrar kullanılırsa o kullanıcının tüm zinciri iptal edilir.

### UserSignature
`Id, UserId, MediaAssetId → MediaAsset, CreatedAt, ReplacedAt?`

Tek seferlik kayıtlı parmak imzası. Değiştirilirse eski kayıt `ReplacedAt` ile durur, silinmez —
geçmiş iş emirleri o tarihteki imzaya referans verir.

**Index:** `User(Email)` unique, `User(Status)`, `RefreshToken(TokenHash)` unique, `ManagerScope(UserId)`

---

## 3. Medya

### MediaAsset
`Id, StorageKey, ContentType, ByteSize, Width?, Height?, Sha256, UploadedByUserId, UploadedAt`

Tüm fotoğraflar tek tabloda. `IFileStorage` arkasında bugün lokal disk, ileride S3/Azure —
tablo değişmez. `Sha256` aynı fotoğrafın iki kez yüklenmesini yakalar.

⚠️ Üretim limitleri kararlaştırılmalı: dosya başına maks. boyut (öneri 10 MB), sunucuda yeniden
boyutlandırma (öneri uzun kenar 2000 px), rapor başına maks. adet (öneri 20).

---

## 4. Maintenance — çekirdek

### WorkOrder
```
Id, Number (WO-1044, sıralı, insan-okur),
JobType: JobType,                                -- Reactive | Task | Ppm | Project
UnitId → Unit, AreaId? → Area, LineId? → Line,
EquipmentId? → Equipment, EquipmentFreeText?,   -- "Other" seçeneği
ReportedByUserId → User, ReportedAt,
Priority: Priority,
Description,
Status: WorkOrderStatus,
AssignedEngineerId? → User, ClaimedAt?,
ClosedAt?, ClosedByUserId?,
LabourMinutes?,                                  -- otomatik: ClaimedAt → ClosedAt
ClientId (Guid, unique), RowVersion
```

`WorkOrderStatus` *(2026-09-07'de fabrikanın listesine göre yeniden adlandırıldı)*:
```
New = 0           havuzda, kimse almadı
Accepted = 1      teknisyen üstlendi
InProgress = 2    çalışılıyor
WaitingParts = 3  parça bekleniyor (kapanmış sayılır, rapor edene tek seferlik bildirim)
AwaitingQa = 4    kapanış gönderildi, QA swab testi bekliyor
QaFailed = 5      QA reddetti, teknisyene geri döndü
Completed = 6     tamamlandı
Rejected = 7      geri çevrildi — arıza değil, mükerrer, veya bakımın işi değil
```

`AwaitingQa` ve `QaFailed` fabrikanın verdiği listede yok; **iç durum olarak kaldılar**
çünkü intrusive işte QA swab akışının başka yeri yok (kullanıcı onayladı, 2026-09-07).

`Rejected`, eski `Cancelled`'ın yerini aldı — aynı ordinal, aynı akış (sebep zorunlu,
terminal, kendi event'i). Fabrikanın durum listesinde `Cancelled` yoktu.

`JobType`: `Reactive = 0`, `Task = 1`, `Ppm = 2`, `Project = 3`. Arıza bildirimi her zaman
`Reactive` üretir; PPM ve Project'in ekranları henüz yok.

`Priority`: `Low = 0`, `Medium = 1`, `High = 2` — **3 seviye**. `Critical` 2026-09-07'de
kaldırıldı; migration mevcut `Critical` kayıtlarını `High`'a çevirdi.

**Geçerli durum geçişleri tek yerde tanımlanır** (`WorkOrderStateMachine`); geçersiz geçiş **409** döner.

### WorkOrderPhoto
`Id, WorkOrderId, MediaAssetId, Category: PhotoCategory (Report | ToolsAndParts | Closing), CreatedAt`

### WorkOrderEvent — **append-only**
`Id, WorkOrderId, Type, ActorUserId?, OccurredAt, CorrelationId, PayloadJson`

Her eylem buraya düşer: Created, Claimed, Assigned, Reassigned, StatusChanged, EngineerNotified
(I'm Busy / On My Way), ClosureSubmitted, QaRequested, QaPassed, QaFailed, SignedOff, Reopened.
**Güncelleme ve silme yoktur** — BRC denetiminin dayanağı budur.

### WorkOrderClosure — **versiyonlu**
```
Id, WorkOrderId, Version (1, 2, 3 …),
RootCause, CorrectiveAction,
AbleToRepair (bool), UnableToRepairReason?,
ContractorRequired (bool), ContractorUsed?,     -- tek soru, Yes ise serbest metin
DowntimeMinutes,
ToolsAndPartsAccounted (bool), MissingItemsNote?,
PostDeodorisationIntervention (bool),           -- Yes → QA swab testi tetiklenir
SubmittedByUserId, SubmittedAt
```

QA "Fail" verip mühendis düzeltince **yeni bir Version satırı** eklenir; önceki hali denetim için durur.
Güncel kapanış = en yüksek `Version`.

### WorkOrderCost
`WorkOrderId (PK), PartsRequired, PartsPrice, LabourHours, LabourCostPerHour, PoNumber`

Resmi kağıt formdaki maliyet bölümü. `LabourHours` `WorkOrder.LabourMinutes` üzerinden otomatik dolar,
elle düzeltilirse `UpdatedByUserId` kaydedilir.

### QaCheck
`Id, WorkOrderId, Attempt (1,2,3…), RequestedAt, Result: QaResult (Pending | Pass | Fail), ResultedAt?, ResultByUserId?, Note?`

`Fail` için `Note` **zorunlu** — mühendisin elinde somut bir sebep olmadan iş geri gönderilemez.

### SignOff
`Id, WorkOrderId, Kind: SignOffKind (Production | Qa), UserId, SignedAt, SignatureAssetId → MediaAsset, AreaCleanAndTidy (bool), ReleasedBackIntoService (bool)`

Production Sign Off her kapanışta; QA Sign Off yalnızca intrusive işlerde.

**Index:** `WorkOrder(Status, ReportedAt DESC)`, `WorkOrder(AssignedEngineerId, Status)`,
`WorkOrder(AreaId, ReportedAt DESC)`, `WorkOrder(Number)` unique, `WorkOrderEvent(WorkOrderId, OccurredAt)`

---

## 5. Tasks ve Orders

### MaintenanceTask
`Id, Title, Description?, Kind: TaskKind (Daily | Weekend | ManagerAssigned), ScheduledDate, Priority?: TaskPriority (Reactive | Corrective | Preventive), CreatedByUserId, CreatedAt, DeletedAt?, DeletedByUserId?`

Sadece Maintenance Manager oluşturur. `DeletedAt` ile pasife alınır, satır silinmez.

### TaskAssignment
`TaskId, UserId` (bileşik PK) — bir task birden çok mühendise atanabilir.

### TaskCompletion
`Id, TaskId, UserId, Note, CompletedAt` + `TaskCompletionPhoto (Id, TaskCompletionId, MediaAssetId)`

Görünürlük: açık task'ı sadece atanan görür; **tamamlanmış task'ı herkes görür**.

### PartOrderRequest
`Id, PartName, Quantity, IsUrgent, RequestedByUserId, RequestedAt, Status: OrderStatus (Pending | Ordered), OrderedAt?, OrderedByUserId?, DeletedAt?, DeletedByUserId?`

Mühendis kendi talebini görür/siler; manager hepsini görür/siler/"ordered" işaretler. Bildirim yok.

---

## 6. Attendance — maaşı etkileyen modül

### ClockEvent
```
Id, UserId, Type: ClockType (In | Out),
OccurredAtUtc,          -- cihazın bildirdiği an
ReceivedAtUtc,          -- sunucuya ulaştığı an
Source: ClockSource (AutoGeofence | Manual),
Latitude?, Longitude?, AccuracyMeters?,
IsMockLocation (bool),  -- Android sahte konum bayrağı
DeviceId,
ClientId (Guid, unique),
IsSuspect (bool), SuspectReason?,
CreatedAt
```

Üretim kuralları:
- **Cihaz saatine güvenilmez.** `OccurredAtUtc` ile `ReceivedAtUtc` farkı eşiği aşarsa (offline
  değilse) kayıt `IsSuspect` işaretlenir; manager listesinde görünür.
- **GPS doğruluğu kötüyse otomatik kayıt yapılmaz** — kullanıcıya manuel seçenek sunulur.
- `ClientId` sayesinde offline kuyruk iki kez gönderse bile tek kayıt oluşur.
- ⚠️ Konum saklama süresi kararlaştırılmalı (UK GDPR). Öneri: koordinatlar 6 ay sonra silinir,
  clock kaydının kendisi (kim/ne zaman) bordro süresince kalır.

### ClockCorrection — **append-only**
`Id, ClockEventId, CorrectedByUserId, Reason, NewOccurredAtUtc, CreatedAt`

Yanlış kayıt **düzeltilmez, üstüne düzeltme kaydı eklenir**. Orijinal her zaman görünür kalır.

### OvertimeDeclaration
`Id, UserId, Date, Minutes, Note?, Status: ApprovalStatus (Pending | Approved | Rejected), DecidedByUserId?, DecidedAt?`

Çalışan kendisi beyan eder; gerçek clock kayıtlarıyla çapraz kontrol edilebilir.

### GeofenceSetting
`Id, Name, Latitude, Longitude, RadiusMeters, ReentryToleranceMinutes, IsActive, UpdatedByUserId, UpdatedAt`

⚠️ Fabrika koordinatı ve yarıçap gerekli. Tolerans önerisi: 10 dakika.

**Index:** `ClockEvent(UserId, OccurredAtUtc DESC)`, `ClockEvent(ClientId)` unique, `ClockEvent(IsSuspect)`

---

## 7. Holiday ve Shift

### HolidayRequest
`Id, UserId, StartDate, EndDate, WorkingDays, Status: ApprovalStatus, RequestedAt, DecidedByUserId?, DecidedAt?, DecisionNote?`

`WorkingDays` hafta sonları **ve resmi tatiller** hariç hesaplanır.
Submit = çalışanın imzası, Approve/Reject = manager'ın imzası (ayrı imza kutusu yok).

### PublicHoliday
`Id, Date, Name, Region (varsayılan "England and Wales")`

⚠️ UK bank holiday listesi nereden gelecek? Öneri: yılda bir kez admin panelden içe aktarma.

### ShiftType
`Id, Name, StartTime (time), EndTime (time), IsActive, DisplayOrder`

Varsayılan seed: Morning 06:00–14:00, Afternoon 14:00–22:00, Night 22:00–06:00.
**Silme yok** — `IsActive=false`. Gece vardiyası gün aşırıdır (`EndTime < StartTime`), bu bilinçli.

### ShiftAssignment
`Id, UserId, Date, ShiftTypeId, CreatedByUserId, CreatedAt` — unique `(UserId, Date, ShiftTypeId)`

**Index:** `ShiftAssignment(Date, ShiftTypeId)`, `HolidayRequest(UserId, StartDate)`, `HolidayRequest(Status)`

---

## 8. Mesajlar ve bildirimler

### Message
`Id, SenderUserId, Body, Priority: MessagePriority (Normal | High), CreatedAt, ExpiresAt?`

### MessageRecipient
`Id, MessageId, UserId? → User, DepartmentId? → Department`

İkisinden tam biri dolu olur (CHECK constraint). Departman seçilirse gönderim anındaki üyeler
`NotificationLog` üzerinden çözümlenir — sonradan departmana katılan eski mesajı görmez.

### MessageRead
`MessageId, UserId, ReadAt` (bileşik PK)

### DeviceToken
`Id, UserId, Token, Platform (Android | iOS | Web), CreatedAt, LastSeenAt, RevokedAt?`

### NotificationLog
`Id, UserId, MessageId?, Type, SentAt, DeliveryStatus, FailureReason?`

Sistem bildirimleri de buradan geçer: "engineer on the way", "waiting for parts",
"holiday approved", "QA failed".

---

## 9. Denetim

### AuditLog — **append-only**
`Id, ActorUserId?, Action, EntityType, EntityId, OccurredAt, CorrelationId, ChangesJson`

Kim neyi değiştirdi. `WorkOrderEvent` iş akışının hikâyesini, `AuditLog` veri değişikliğini tutar —
ikisi ayrıdır ve ikisi de gerekir.

---

## 10. Açık sorular

### Cevaplandı (2026-09-04)

| Soru | Karar |
|---|---|
| Öncelik seviyeleri | ~~4 seviye~~ → **3 seviye**: `Low`, `Medium`, `High` (2026-09-07'de `Critical` kaldırıldı) |
| Departman listesi | **Production, Maintenance** — kalanlar admin panelinden eklenecek |
| Site planı seed'e girsin mi | ~~Hayır~~ → **Evet**, 2026-09-07'de fabrikanın kendi listesi seed'e girdi: 10 oda, 4 hat, 62 makine. Gerisi admin panelinden |
| Rol yapısı | **Tek rol** per kullanıcı |

### Hâlâ bekliyor (varsayımla ilerleniyor, ilgili fazda teyit edilecek)

1. **Fotoğraf limitleri** — varsayım: dosya başına 10 MB, uzun kenar 2000 px, rapor başına 20 adet *(Faz 3)*
2. **Konum verisi saklama süresi** — varsayım: koordinat 6 ay, clock kaydı bordro süresince *(Faz 6)*
3. **Fabrika GPS koordinatı ve yarıçap** — tablo kuruluyor, değer admin panelinden girilecek *(Faz 6)*
4. **UK resmi tatil listesi** kaynağı — varsayım: yılda bir admin panelden içe aktarma *(Faz 6)*
5. **CFI'nin mevcut çalışan bilgilendirme metni / İK süreci** var mı — UK GDPR için *(Faz 2)*
