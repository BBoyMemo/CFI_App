# Daily Tasks — Kararlar

Bu uygulama CFI App'ten **tamamen bağımsızdır**: ayrı kod, ayrı veritabanı (ayrı Docker
container'ı), ayrı port'lar. Ortak hiçbir şey yok.

Kaynak: `daily-tasks-orders-prompt.md`. Prompt'taki "açık sorular" kullanıcı yokken
**varsayılan** olarak aşağıdaki gibi cevaplandı. Hepsi değiştirilebilir — değiştirmek
istediğiniz satırı söylemeniz yeterli.

## Açık sorulara verilen varsayılan cevaplar

| # | Soru | Varsayılan karar | Değiştirmek kolay mı? |
|---|------|------------------|----------------------|
| 1 | Proje adı | **Daily Tasks** (klasör `DailyTasksApp`, Android paketi `com.dailytasks.app`) | Evet |
| 2 | Kimlik doğrulama | **JWT**, 12 saat geçerli. Süre dolunca tekrar giriş. Refresh token yok (sade tutmak için). | Evet |
| 3 | Şifre kuralı | En az **8**, en çok 128 karakter. Başka kural yok. Şifreler PBKDF2-SHA256 (210.000 tur) ile hash'lenir. | Evet |
| 4 | Giriş adı | Büyük/küçük harf duyarsız ve **benzersiz** (iki "John Smith" olamaz — giriş adla yapıldığı için). | — |
| 5 | Fotoğraf nerede | **Sunucunun diskinde** (`backend/src/DailyTasks.Api/storage/`), `IFileStorage` arkasında → ileride bulut. Veritabanında sadece dosya anahtarı. | Evet |
| 6 | Fotoğraf limiti | Tek fotoğraf, **en çok 10 MB**, **JPG / PNG / WEBP**. Tür, dosya adına değil dosyanın ilk baytlarına bakılarak doğrulanır. Telefon/web fotoğrafı yüklemeden önce 1920 px'e küçültür. | Evet |
| 7 | Engineer hangi görevleri görür | **Açık / devam eden görevler:** sadece kendisine atananlar. **En az bir kez tamamlanmış görevler:** hepsi (kullanıcı kararı, History sekmesinde). Manager her şeyi görür. | Evet |
| 8 | Manager sipariş silebilir mi | **Evet.** | Evet |
| 9 | Engineer hangi siparişleri görür | **Hepsini** (aynı parçayı iki kez istememek için). | Evet |
| 10 | Devir (carry-over) nasıl tetiklenir | **İstek anında**: görev listesi her okunduğunda tamamlanmamış, tarihi geçmiş görevler bugüne taşınır. Zamanlayıcı yok, sunucu kapalı kalsa bile doğru çalışır. | Evet |
| 11 | "Gün sonu" hangi saat dilimi | **Europe/London** (fabrika saati; yaz saati dahil). | Evet (`Site:TimeZone`) |
| 12 | Manager görevi sonradan düzenleyebilir mi | **Evet** (başlık, açıklama, tarih, öncelik, vardiya, atananlar) — açık ve **devam eden** görevde her an (yeniden atama dahil). **Tamamlanmış** görev düzenlenemez (kayıttır). | Evet |
| 13 | Barındırma | Henüz karar yok. Şu an geliştirme bilgisayarında çalışıyor. API, derlenmiş web uygulamasını kendisi de sunabilir (tek sunucu kurulumu). | — |
| 14 | Diller | **EN** (varsayılan), **PL**, **BG**, **FIL** (Filipino; İspanyolca'nın yerine, kullanıcı isteği). Çeviriler web ve mobilde ortak dosyalardan gelir. | Evet |

## Diğer kurallar (prompt'tan)

- Roller: yalnızca **Manager** ve **Engineer**. Kayıt ekranı yok; kullanıcıyı Manager ekler (ad + şifre + rol).
- Çalışan listesinde **sadece ad** var.
- Görev: başlık (zorunlu), açıklama (isteğe bağlı), tarih, öncelik (Low / Medium / High),
  vardiya (Morning / Afternoon), en az bir kişi (Engineer veya Manager). Tarih geçmişte olamaz.
- Görev durumu: **Open → In progress → Completed**. Ayrıntılar: "Görev kartları" bölümü.
- Tamamlanan görevde **kim + tarih + saat** görünür (kullanıcı isteği).
- **History** sekmesi (Tasks ile Orders arasında, kullanıcı isteği): **en az bir kez tamamlanmış**
  görevler, en yeni tamamlanma üstte, o güne göre gruplu, sayfalı. Herkes hepsini görür ve kart
  ekleyebilir. Hiç tamamlanmamış "devam eden" görev History'de görünmez.
- **History araması** (kullanıcı isteği): başlıkta veya açıklamada geçen ifade (büyük/küçük
  harf duyarsız) ve/veya **tamamlanma günü** (fabrika saatiyle). İkisi birlikte de kullanılabilir.
- Devreden görevde "**↪ 6 Oct'tan**" etiketi görünür (ilk planlanan tarih). Manager tarihi
  elle değiştirirse bu etiket sıfırlanır.
- Sipariş: açıklama (zorunlu) + **en fazla 10 fotoğraf** (isteğe bağlı). Durum: **New** → **Ordered**; yalnızca Manager
  değiştirir. Manager "Ordered"ı **aynı gün gece 00:00'a kadar** (Londra saati) **Undo** ile geri alabilir; sonra kalıcıdır.
- Yetki reddi **403**, geçersiz/eksik oturum **401**. İstemci sadece 401'de çıkış yapar.

## İlk giriş

Veritabanı boşken API, yapılandırmadaki `Bootstrap:ManagerName` / `Bootstrap:ManagerPassword`
ile **bir** Manager hesabı oluşturur (geliştirmede: `Manager` / `Manager123!`). Bu değerler
sadece kullanıcı tablosu boşken kullanılır. **Canlıya geçmeden önce şifreyi değiştirin**
(şu an şifre değiştirme ekranı yok — prompt'ta olmadığı için eklenmedi).

## Görev kartları (kullanıcı isteği)

- Görevin verildiği hâli üstte **ana kart**tır; altında eklenen her işlem ayrı bir **kart** olarak
  sıralanır: sonuç (In progress / Completed), kişi, tarih + saat, yorum ve en fazla 10 fotoğraf
  (ikisi de isteğe bağlı). Ana kart ve eski kartlar **asla değişmez**.
- **Tamamlanana kadar** kart ekleyebilenler: göreve atananlar ve Manager (başkası 403 alır).
  "In progress" kartı görevi devam ediyor yapar; görev tamamlanana kadar her gün bugüne devreder.
- **Tamamlandıktan sonra** herkes kart ekleyebilir:
  - **Completed** → ek not; görev tamamlanmış kalır, tamamlanma zamanı/kişisi bu kart olur.
  - **In progress** → görevi geri alır: görev **yalnızca geri alanın** olur (atananlar = o kişi),
    bugünün listesine düşer ve **History'de kalmaya devam eder**.
- Manager, Engineer'ın yapabildiği her şeyi her görevde yapabilir.
- Bu özellikten önceki tamamlamalar (yorum + fotoğraflarıyla) otomatik olarak görevin **ilk kartı**
  yapıldı (geliştirme veritabanında 8 görev).

## Kayıtlar silinmez (kullanıcı isteği)

- **Üzerinde en az bir kart olan görev silinemez** (Manager dahil). Yalnızca hiç dokunulmamış
  (Open, kartsız) görevi Manager silebilir. Tamamlanan görevin aslı ve kartları değiştirilemez.
- **Sipariş verilmiş (Ordered) sipariş silinemez.** Yalnızca hâlâ "New" olan bir talebi Manager silebilir.
- **Kullanıcılar silinmez, devre dışı bırakılır** (Team'de kişiye dokun → kart → Deactivate). Giriş yapamaz, açık oturumu hemen kapanır,
  görev atama listesinde çıkmaz; Team'de "Deactivated" olarak görünür. Geçmişteki görev, kart ve siparişlerde
  adı aynen kalır. Kişiye atanmış açık (Open / In progress) görev varsa engellenir: önce başkasına atanmalı.
  Manager başka bir Manager'ı da devre dışı bırakabilir, **kendini bırakamaz**. Devre dışı kişi aynı karttan
  **Activate** ile geri açılır; eski şifresiyle tekrar girer.

## Arama (kullanıcı isteği)

- **History:** başlık, açıklama, **kart yorumları** (ve çevirileri), **kartı yazanların adı** veya
  **atanan kişilerin adları** + tamamlanma günü (takvimden).
- **Siparişler:** açıklama (parça adı vb.), **isteyen** veya **sipariş veren** kişinin adı + gün
  (istendiği ya da sipariş verildiği gün). Arama New ve Ordered'ın **ikisinde birden** arar ve her
  sonucun durumunu gösterir ("bu parça ısmarlandı mı?" sorusunun tek cevabı olsun diye).

## Görev fotoğrafları (kullanıcı isteği)

- Manager görev oluştururken/düzenlerken **en fazla 10 fotoğraf** ekler (kamera veya galeriden toplu).
  Tamamlanan görevde fotoğraflar değişmez. Her kartın kendi fotoğrafları (en fazla 10) ayrıca durur.
- Fotoğrafları görevi görebilen herkes görür (açık görev: atananlar + Manager; tamamlanan: herkes).

## Otomatik çeviri (kullanıcı isteği, DeepL API Free)

- Görev başlığı/açıklaması, kart yorumları ve sipariş açıklaması kayıttan sonra arka planda
  EN/PL/BG/FIL'e çevrilip saklanır (Filipino için DeepL hedefi `TL`); herkes kendi dilinde okur, 🌐 ile aslını görür. Adlar çevrilmez.
- Anahtar `backend/src/DailyTasks.Api/appsettings.Local.json` içinde (git dışı). Anahtar yoksa hiçbir
  metin dışarı gitmez. Kota/ağ sorununda orijinal görünür, çeviri sonra tekrar denenir.
- **Gizlilik:** DeepL'in ücretsiz planında gönderilen metinler DeepL tarafından saklanıp eğitimde
  kullanılabilir (DeepL gizlilik politikası, bölüm 13/3) — yorumlara kişisel bilgi yazılmamalı.

## Mobil gezinme (kullanıcı isteği)

- Göreve / siparişe dokununca tam ekran **detay**; fotoğraf tam genişlikte, dokununca tam ekran.
- Görev listesinde **sola kaydır = ertesi gün, sağa kaydır = önceki gün** (Android'in yerel sayfa
  kaydırması; eğik kaydırmada da çalışır).
- Tarihe dokununca **takvim** (hafta Pazartesi başlar). Görev formunda, History'de ve siparişlerde de.

## Hesap sayfası (kullanıcı isteği)

- Herkes üst bardaki adına dokunarak hesap sayfasını açar ve **kendi şifresini** değiştirir.
- Mevcut şifre zorunludur (açık unutulmuş bir telefondan şifre değiştirilemesin diye); yeni
  şifre en az 8 karakter, iki kez yazılır. Giriş gibi hız sınırına tabidir.
- Herkes **kendi adını** da değiştirir (mevcut şifreyle). Ad aynı zamanda giriş adıdır, benzersiz kalır.
- Mobilde hesap sayfasının altında **uygulama sürümü** görünür (tek kaynak: `mobile/app.json` → `version`).
- Tamamlanan görevlerin başlıkları **üstü çizili değil**, normal renkte; yeşil şerit ve ✓ yeterli.

## Team ve şifre sıfırlama (kullanıcı isteği)

- Yeni kişi eklerken şifre **iki kez** yazılır.
- Team listesinde düğme yok: kişiye dokununca **kart** açılır (Edit / Reset password, Deactivate ya da Activate).
- Manager kişiyi **düzenler** (ad, rol). Kendi bilgilerini Team'den değil hesap sayfasından değiştirir
  (kendi Manager rolünü elinden almasın diye).
- Manager **şifreyi keyfine göre değiştiremez**: yalnızca kişi "Forgot password?" dediyse yeni şifre yazabilir
  (sunucu da reddeder: 409 `user.noResetRequest`). Yeni kişi eklerken şifreyi Manager belirler (iki kez).
- **Şifremi unuttum:** giriş ekranında kişi adını yazar → Team'de o kişinin yanında "Reset password"
  görünür. Hesapta hiçbir şey değişmez (başkası adına istek zararsız); cevap her zaman aynıdır (hangi
  adların var olduğu belli olmaz).
- Manager'ın verdiği şifre **geçicidir**: kişi girişte önce kendi şifresini seçmek zorundadır; o zamana
  kadar sunucu yalnızca `/api/auth` uçlarına cevap verir (403 `auth.mustChangePassword`).
- Kişinin rolü değişirse açık oturumu kapanır (yeni rolle tekrar giriş yapar).

## Prompt'ta olmayan, eklenmeyen şeyler (ihtiyaç olursa sorun)

- Görev geçmişi raporu / dışa aktarma (History ekranı ve araması var)
- Bildirim (push)
- Siparişte miktar/aciliyet alanı

## Android (APK)

- React Native 0.76, yalnızca Android. Play Store değil, **APK** ile dağıtılır.
- Sunucu adresi uygulamanın içinde sabit: `https://ynzserver.duckdns.org/dailytasks` (`mobile/src/api.js`).
  Adres değişirse yeni APK gerekir. Dosya adı `dist/DailyTasks.apk`.
- Şu an HTTP'ye izin verilir (`usesCleartextTraffic`) çünkü sunucu yerel ağda. Canlıda
  **HTTPS** kullanılmalı.
- APK, `mobile/android/app/dailytasks-release.keystore` anahtarıyla imzalanır. Bu dosya ve
  `mobile/android/keystore.properties` **git'e girmez** — **yedekleyin**: kaybolursa telefondaki
  uygulama güncellenemez, kaldırılıp yeniden kurulması gerekir.
