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
| 7 | Engineer hangi görevleri görür | **Açık görevler:** sadece kendisine atananlar. **Tamamlanan görevler:** hepsi (kullanıcı kararı, History sekmesinde). Manager her şeyi görür. | Evet |
| 8 | Manager sipariş silebilir mi | **Evet.** | Evet |
| 9 | Engineer hangi siparişleri görür | **Hepsini** (aynı parçayı iki kez istememek için). | Evet |
| 10 | Devir (carry-over) nasıl tetiklenir | **İstek anında**: görev listesi her okunduğunda tamamlanmamış, tarihi geçmiş görevler bugüne taşınır. Zamanlayıcı yok, sunucu kapalı kalsa bile doğru çalışır. | Evet |
| 11 | "Gün sonu" hangi saat dilimi | **Europe/London** (fabrika saati; yaz saati dahil). | Evet (`Site:TimeZone`) |
| 12 | Manager görevi sonradan düzenleyebilir mi | **Evet** (başlık, açıklama, tarih, öncelik, vardiya, atananlar). **Tamamlanmış** görev düzenlenemez (kayıttır), sadece silinebilir. | Evet |
| 13 | Barındırma | Henüz karar yok. Şu an geliştirme bilgisayarında çalışıyor. API, derlenmiş web uygulamasını kendisi de sunabilir (tek sunucu kurulumu). | — |
| 14 | Diller | **EN** (varsayılan), **PL**, **BG**, **ES**. Çeviriler web ve mobilde ortak dosyalardan gelir. | Evet |

## Diğer kurallar (prompt'tan)

- Roller: yalnızca **Manager** ve **Engineer**. Kayıt ekranı yok; kullanıcıyı Manager ekler (ad + şifre + rol).
- Çalışan listesinde **sadece ad** var.
- Görev: başlık (zorunlu), açıklama (isteğe bağlı), tarih, öncelik (Low / Medium / High),
  vardiya (Morning / Afternoon), en az bir Engineer. Tarih geçmişte olamaz.
- Bir görevi atanan Engineer'lardan **biri** tamamlar; yorum ve fotoğraf isteğe bağlı.
  İki kişi aynı anda tamamlarsa yalnızca biri kaydedilir (diğeri "zaten tamamlandı" görür).
- Tamamlanan görevde **kim + tarih + saat** görünür (kullanıcı isteği).
- **History** sekmesi (Tasks ile Orders arasında, kullanıcı isteği): tüm tamamlanan görevler,
  en yeni üstte, tamamlandığı güne göre gruplu, sayfalı. Salt okunur. Herkes hepsini görür.
- **History araması** (kullanıcı isteği): başlıkta veya açıklamada geçen ifade (büyük/küçük
  harf duyarsız) ve/veya **tamamlanma günü** (fabrika saatiyle). İkisi birlikte de kullanılabilir.
  Tamamlama yorumu aramaya dahil değil.
- Devreden görevde "**↪ 6 Oct'tan**" etiketi görünür (ilk planlanan tarih). Manager tarihi
  elle değiştirirse bu etiket sıfırlanır.
- Sipariş: açıklama (zorunlu) + fotoğraf (isteğe bağlı). Durum: **New** → **Ordered**; yalnızca Manager değiştirir.
- Yetki reddi **403**, geçersiz/eksik oturum **401**. İstemci sadece 401'de çıkış yapar.

## İlk giriş

Veritabanı boşken API, yapılandırmadaki `Bootstrap:ManagerName` / `Bootstrap:ManagerPassword`
ile **bir** Manager hesabı oluşturur (geliştirmede: `Manager` / `Manager123!`). Bu değerler
sadece kullanıcı tablosu boşken kullanılır. **Canlıya geçmeden önce şifreyi değiştirin**
(şu an şifre değiştirme ekranı yok — prompt'ta olmadığı için eklenmedi).

## Kayıtlar silinmez (kullanıcı isteği)

- **Tamamlanan görev silinemez ve değiştirilemez** (Manager dahil); History'nin parçasıdır.
- **Sipariş verilmiş (Ordered) sipariş silinemez.** Yalnızca hâlâ "New" olan bir talebi Manager silebilir.

## Arama (kullanıcı isteği)

- **History:** başlık, açıklama, tamamlama yorumu, **tamamlayan kişinin adı** veya **atanan kişilerin
  adları** + tamamlanma günü (takvimden).
- **Siparişler:** açıklama (parça adı vb.), **isteyen** veya **sipariş veren** kişinin adı + gün
  (istendiği ya da sipariş verildiği gün). Arama New ve Ordered'ın **ikisinde birden** arar ve her
  sonucun durumunu gösterir ("bu parça ısmarlandı mı?" sorusunun tek cevabı olsun diye).

## Görev fotoğrafları (kullanıcı isteği)

- Manager görev oluştururken/düzenlerken **en fazla 5 fotoğraf** ekler (kamera veya galeriden toplu).
  Tamamlanan görevde fotoğraflar değişmez. Tamamlama fotoğrafı (Engineer'ın) ayrıca durur.
- Fotoğrafları görevi görebilen herkes görür (açık görev: atananlar + Manager; tamamlanan: herkes).

## Otomatik çeviri (kullanıcı isteği, DeepL API Free)

- Görev başlığı/açıklaması, tamamlama yorumu ve sipariş açıklaması kayıttan sonra arka planda
  EN/PL/BG/ES'ye çevrilip saklanır; herkes kendi dilinde okur, 🌐 ile aslını görür. Adlar çevrilmez.
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
- Tamamlanan görevlerin başlıkları **üstü çizili değil**, normal renkte; yeşil şerit ve ✓ yeterli.

## Prompt'ta olmayan, eklenmeyen şeyler (ihtiyaç olursa sorun)

- Kullanıcı silme / Manager'ın başkasının şifresini sıfırlaması
- Görev geçmişi raporu / dışa aktarma (History ekranı ve araması var)
- Bildirim (push)
- Siparişte miktar/aciliyet alanı

## Android (APK)

- React Native 0.76, yalnızca Android. Play Store değil, **APK** ile dağıtılır.
- Sunucu adresi giriş ekranında bir kez yazılır (barındırma belli olmadığı için).
- Şu an HTTP'ye izin verilir (`usesCleartextTraffic`) çünkü sunucu yerel ağda. Canlıda
  **HTTPS** kullanılmalı.
- APK, `mobile/android/app/dailytasks-release.keystore` anahtarıyla imzalanır. Bu dosya ve
  `mobile/android/keystore.properties` **git'e girmez** — **yedekleyin**: kaybolursa telefondaki
  uygulama güncellenemez, kaldırılıp yeniden kurulması gerekir.
