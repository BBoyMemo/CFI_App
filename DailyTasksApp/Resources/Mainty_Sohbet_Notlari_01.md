# Mainty — Sohbet Notları (Bölüm 1)

## 0. Bu dosyanın amacı

Bu dosya, Mainty projesinin yeniden sıfırdan (temiz mimari ile) tasarlanması sürecinde
sesli olarak konuşulan iş mantığı kararlarını kaydeder. Amaç, yeni bir chat açıldığında
kaldığımız yerden devam edebilmektir.

**İsimlendirme notu:** Firmanın kısaltması **CFI**'dir ("CFIA" değil — bu yanlış anlaşılma daha önce
düzeltilmişti ama önceki dosya adında/başlığında tekrar hatalı yazılmıştı, burada düzeltildi).
Uygulamanın kendi ismi ise **şimdilik "Mainty" olarak kalıyor**. İleride, uygulama sadece
maintenance'tan çıkıp CFI için genel bir şirket platformuna dönüştüğünde daha genel bir isim
düşünülebilir — kullanıcı "Mainty" ismini de beğeniyor, bu yüzden kesin bir değişiklik kararı yok.

Bu dosya şu iki mevcut dosyanın **devamı/eki** niteliğindedir, onların yerine geçmez:

- `Mainty_Project_Handoff.md` — eski (ChatGPT ile yapılmış) teknik handoff, backend/frontend mimarisi, JWT migration sorunu
- `CFIA_Mainty_Future_Vision_and_Requirements.md` — CFIA için genel gelecek vizyonu (Bulgarca)

Bu üç dosya birlikte okunmalı.

---

## 1. Genel bağlam

- Kullanıcı (Memo), County Food Ingredients (**CFI**) firmasında **maintenance engineer** olarak çalışıyor.
- Amaç: bu uygulamayı geliştirip firmaya (CFI'ye) satmak / firma için tüm şirket çapında bir platform haline getirmek.
- Uygulamanın ismi **şimdilik Mainty olarak kalıyor** (bkz. Bölüm 0 — isimlendirme notu).
- Mevcut Mainty projesi eski bir AI (muhtemelen ChatGPT, ~Ocak ayı) ile başlamış:
  - Backend mantığı kullanıcıya göre "aşağı yukarı" iş görür durumda ama tam düzgün çalışmıyor olabilir.
  - Frontend (React) kullanıcı tarafından beğenilmiyor, düzgün çalışmıyor.
- **Yaklaşım:** Sıfırdan yeniden yapılacak, ama backend mantığından (ve bazı kod parçalarından) faydalanılabilir. Frontend tamamen farklı/yeni yapılacak.
- Çalışma metodu: Önce konuşarak (sesli) tüm iş akışları detaylandırılacak, aplikasyon ne yapıyor/ne yapmalı baştan konuşulacak. Sonra adım adım, dosya dosya inşa edilecek. Her adımda kullanıcıya "bu böyle mi kalsın?" diye sorulacak, tek taraflı karar verilmeyecek.

### Erişilen kaynaklar (bu oturumda kontrol edildi)
- Backend repo: `https://github.com/BBoyMemo/Mainty.git` (public, branch: master, son commit: "JWT Token added 21/01/26")
  - .NET clean architecture: `Mainty.Api`, `Mainty.Application`, `Mainty.Domain`, `Mainty.Infrastructure`
  - `WorkOrdersController.cs` ve `WorkOrderService.cs` içerikleri incelendi — JWT + `[Authorize(Roles=...)]` kısmen uygulanmış, ama eski `HttpContext.Items["CurrentUser"]` / `RequireRoles<T>()` kod hâlâ dosyada (yorum satırı + aktif karışık), kod dağınık, çok fazla comment-out edilmiş eski deneme bloğu var.
  - WorkOrder entity: `WorkOrderId, UnitId, AreaId, AssetId, ReportedByUserId, AssignedEngineerId, Priority, Status, Description, ResolutionSummary, RootCause, DowntimeMinutes, CreatedAt, ClosedAt, ClosedByUserId` + `Updates`, `Attachments`
  - `WorkOrderStatus` enum: New=0, Claimed=1, InProgress=2, WaitingParts=3, Done=4, Verified=5
  - `Priority` enum: Low=0, Medium=1, High=2, Critical=3
  - İş akışı: Create (New) → Claim/Assign (Claimed) → durum güncellemeleri → gerekirse QA test (RequestQaTest/Approve/Reject, kapatmayı bloklayabiliyor - `RequiredForClose`) → Close (Done) → Verify (Verified)
- Frontend repo: `https://github.com/MemoYnz/mainty-web-UI.git` — başlangıçta private idi, kullanıcı public yaptı, klonlandı.
  - React + Vite yapısı, handoff dosyasındaki yapıyla büyük ölçüde örtüşüyor (`AuthContext`, `RequireAuth`, `RequireRole`, `MainLayout`, `Dashboard`, `WorkOrders`, `PpmPlans`, `ShiftTasks`, `WorkOrderDetails`, `Login`, `AuthPanel` — bu sonuncusu handoff'ta yoktu, yeni eklenmiş olabilir)

---

## 2. Teknoloji kararları (henüz kesinleşmemiş, tartışılıyor)

- Backend: .NET — hangi sürüm olacağı (8/9/10) henüz **kararlaştırılmadı**, konuşulacak.
- Database: İş yerinde SQL Server kullanılıyor olabilir (ileride). Ama **test/geliştirme aşamasında**:
  - Kullanıcının evinde bir Ubuntu Linux server var.
  - Orada **PostgreSQL zaten kurulu ve hazır**.
  - İlk aşamada uygulama oraya deploy edilip PostgreSQL ile test edilecek.
  - İleride firma isterse SQL Server'a geçiş yapılabilir.
- Bu teknoloji kararları henüz detaylı konuşulmadı, ileride ayrı bir başlık olarak ele alınacak.

---

## 3. Fabrika yerleşimi (henüz gönderilmedi — TAKİP EDİLMELİ)

Kullanıcı fabrikanın elle çizilmiş bir planını **göndereceğini söyledi ama henüz göndermedi**.
Sonraki chat'te bu hatırlatılmalı.

Şimdiye kadar sözlü olarak anlatılanlar:

- **Unit 1** — Sıvı üretim hattı:
  - Yağ, separatörlerden geçiyor
  - Sıcak olarak tanklara konuyor
  - Tanklardan konservelere (kenlere) dolduruluyor, kapatılıyor
  - Bir **Packing Room** var
  - Unit 1'in kendi ofisi var
- **Unit 2** — Powder Blending (kuru süt / protein):
  - Kuru süt ve protein karıştırılıyor (blending)
  - 2 adet **blender** var
  - **Sakfil** (sack fill / çuvallama makinesi olabilir — tam terim netleşmedi) var
  - Karışım çuvallara dolduruluyor
  - Unit 2'nin kendi ofisi var
- **Unit 3** — Ağırlıklı olarak **depolama** (storage)

Ayrıca fabrikada **3 giriş kapısı (gate)** olduğu belirtildi, detayları plan gelince netleşecek.

**Oda/alan (room/area) listesi** — çalışma alanı ataması için bahsedilenler:
- Filling Room
- Packing (Room)
- Plantroom (separatörlerin olduğu yer)
- Melting

> Not: Bu oda listesi ileride **admin panelinden CRUD (ekle/çıkar/düzenle)** yapılabilir olacak, sabit kodlanmayacak (bkz. Bölüm 5).

---

## 4. Kullanıcı kayıt / onboarding akışı

1. Yeni işçi uygulamaya gelir, bir **register formu** doldurur:
   - İsim
   - Email
   - Telefon numarası
   - (Kesinleşmedi, başka alanlar eklenebilir)
2. Form gönderildiğinde kayıt **onay bekleyen (pending)** durumuna düşer.
3. **Tüm manager'lar** bu bekleyen kaydı görebilir (görünürlük genel, ama onay yetkisi ilgili manager'da).
4. O işçiyi tanıyan / işe alan manager kaydı **onaylar** ve bu onay sırasında:
   - İşçiye bir **rol/occupation** atar (örn. Operator, Engineer, FLT Driver, Packer vs.)
   - İşçiye bir **çalışma alanı (oda/bölge)** atar (örn. Filling Room, Packing, Plantroom, Melting) — makine bazında değil, **oda/alan bazında** atama yapılıyor.
5. Onay sonrası işçi artık normal şekilde giriş (login) yapabilir hale gelir.

**Kullanıcının prensibi:** Arayüz her zaman **sade** tutulmalı — yeterli bilgi olsun ama gereksiz buton/karmaşa olmasın. Bu çok önemli bir tasarım ilkesi olarak vurgulandı.

Claude'un önerisi (henüz karara bağlanmadı, ileride konuşulacak): Oda/alan listesinin sabit kodlanmak yerine yönetilebilir (CRUD) bir liste olması — kullanıcı bunu kabul etti ama "şimdilik sade tutalım" dedi, detay sonraki aşamalarda.

---

## 5. Roller (Occupation) ve yetkilendirme mantığı

### Mevcut (eski Mainty) roller:
- Engineer
- EngineeringManager
- Operator
- QA
- ProductionManager

### Yeni konuşulan ek kavramlar:

- **Occupation** kavramı — kullanıcı "rol" yerine bazen "occupation" terimini kullandı (örn. Packer gibi yeni bir görev tanımı). Bunlar admin panelinden eklenebilecek.
- **FLT Driver** (forklift sürücüsü) — ayrı bir occupation olarak tanımlanacak, Operator'dan farklı.
  - FLT Driver'lar merkezi/tek bir "Forklift Department" altında **toplanmayacak**.
  - Bunun yerine, her department'ın (örn. Production) **kendi FLT Driver'ları** olabilir.
  - İlgili department'ın manager'ı (örn. Production Manager) sadece **kendi department'ındaki** FLT Driver'ları görür/yönetir.
  - FLT Driver'lardan sorumlu, tam bir "manager" olmayan ama sorumluluğu olan ayrı bir pozisyon da olabilir (netleşmedi, ileride konuşulacak).

### Görünürlük (visibility) prensibi:

- Her **manager**, sadece **kendi sorumlu olduğu bölge/department'ları** görür — fabrikanın tamamını değil.
- Bu, bölge/department bazlı yetkilendirme demek: her manager'a hangi bölgelerden/department'lardan sorumlu olduğu atanacak.

### Admin Panel:

- Ayrı, tam yetkili bir **Admin Panel** olacak.
- Şu an admin panelini kullanacak kişiler: **kullanıcının kendisi** ve muhtemelen **maintenance manager**. (İleride kullanıcı admin olmaktan çıkabilir, ama şimdilik o olacak.)
- Admin panelinde **CRUD (tam yetki: ekle/düzenle/sil)** yapılabilecek şeyler:
  - **Department'lar** (yeni department eklenebilir/yönetilebilir)
  - **Oda/alan/bölgeler** (Filling Room, Packing, Plantroom, Melting vb.)
  - **Makineler**
  - **Occupation'lar / roller** (örn. yeni bir "Packer" rolü eklenebilir)
- Bu, sistemin esnek ve büyüyebilir olmasını sağlayacak — fabrika büyüdükçe veya değiştikçe kod değişikliği gerekmeden yönetilebilecek.

---

## 6. Henüz netleşmemiş / açık noktalar

- FLT Driver'ların department yapısı içinde tam olarak nasıl konumlanacağı (merkezi mi, department'a bağlı mı) — genel prensip belirlendi (department'a bağlı, merkezi değil) ama detaylar (örn. FLT driver'lardan sorumlu ayrı bir pozisyon olacak mı) netleşmedi.
- Register formundaki tam alan listesi kesinleşmedi (isim, email, telefon — başka alan olabilir mi?).
- Manager onay sırasında rol + alan dışında başka bir şey seçiyor mu, netleşmedi (soruldu ama tam yanıtlanmadan konu değişti).
- Teknoloji stack detayları (.NET sürümü vs.) henüz konuşulmadı.
- Fabrika planı (görsel) henüz gönderilmedi.

---

## 7. Sırada ne var — YARIM KALAN KONU

Konuşma **Clock In / Clock Out** akışının başında kesildi. Sonraki chat/konuşma buradan devam etmeli:

> Soru soruldu: "Yeni onaylanan operatör işe geldiğinde bunu nasıl işaretleyecek — otomatik mi olacak, yoksa telefonda bir butona mı basacak?"

Bu soru **henüz cevaplanmadı**. Sonraki konuşma buradan başlamalı.

Ardından planlanan sıradaki konular (kullanıcının kendi ifadesiyle):
1. Clock In / Clock Out akışı (detaylı)
2. Makine bozulduğunda (breakdown) operatörün tam olarak ne yaptığı, ekranda ne gördüğü, kime gittiği — adım adım, ekran ekran

---

## 8. Genel prensipler / hatırlatmalar (tüm proje için geçerli)

- Kullanıcı ile Bulgarca yazışılır (varsayılan tercih), ancak bu oturum **sesli modda Türkçe** olarak yürütüldü — bu dosya da o yüzden Türkçe hazırlandı.
- Kod örnekleri tercihen C# (backend) / React + Vite + JavaScript (frontend).
- Dosya oluşturmadan önce kullanıcıdan onay alınır (bu dosya, kullanıcının açık talebi üzerine oluşturuldu).
- Yeni chat'e geçilirken önemli bilgi/bağlam/talimat/ilerleme içeren .md dosyası hazırlanır (bu dosya bu amaçla hazırlandı).
- Uygulama kurulumları D:\Program Files altına yapılır (yerel bilgisayar bağlamı, muhtemelen bu proje için değil ama genel tercih olarak not edildi).
- Kullanıcı varsayımlarda bulunulmasını istemiyor — sorular sorulmalı, kararlar kullanıcıya bırakılmalı.
- **Sadelik** çok önemli bir tasarım ilkesi: az buton, az karmaşa, ama yeterli bilgi.

---

*Bu dosya sohbetin devamı sırasında güncellenecek/genişletilecektir. Numaralandırma yeni bölümler eklendikçe devam edecektir.*
