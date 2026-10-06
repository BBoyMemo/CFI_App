# Mainty — Sohbet Notları (Bölüm 2)

## 0. Bu dosyanın amacı ve nasıl kullanılacağı

Bu dosya, `Mainty_Sohbet_Notlari_01.md` dosyasının **devamıdır**, onun yerine geçmez.
Numaralandırma bu dosyada 1'den yeniden başlıyor (Bölüm 2 kendi iç numaralandırmasıyla),
ama konu olarak Bölüm 1'in kaldığı yerden devam eder.

**Önemli çalışma prensibi:** Kullanıcı bu ve önceki dosyayı kendisi projeye yüklüyor.
Yeni bir chat açıldığında, proje içindeki tüm `Mainty_Sohbet_Notlari_XX.md` dosyaları
(01, 02, 03...) sırayla okunmalı — bunlar birbirinin devamıdır, biri diğerinin
yerine geçmez. Her yeni konuşma bölümü ayrı bir dosya olarak eklenecek, eskiler
tekrar yazılmayacak/tekrarlanmayacak.

Diğer ilgili dosyalar (hepsi birlikte okunmalı):
- `Mainty_Project_Handoff.md` — eski teknik handoff (JWT migration vs.)
- `CFIA_Mainty_Future_Vision_and_Requirements.md` — genel gelecek vizyonu (not: bu dosyanın başlığında "CFIA" var ama doğru kısaltma **CFI**'dir, bkz. Bölüm 1 isimlendirme notu)
- `Mainty_Sohbet_Notlari_01.md` — Bölüm 1

---

## 1. Clock In / Clock Out — karar verilen mantık

- Hem **otomatik** hem **manuel** olacak, ikisi birlikte:
  - Otomatik: konum tabanlı (GPS). Kullanıcı fabrikanın konumuna belirli bir yarıçap
    içine girince (örn. 500 metre, kesin sayı henüz karara bağlanmadı, daha az da olabilir)
    sistem otomatik Clock In yapar.
  - Manuel: otomatik algılama olmazsa (örn. bağlantı sorunu, GPS sorunu) kullanıcı
    elle butona basarak Clock In / Clock Out yapabilir.
- **Teknoloji notu:** GPS/konum kullanımı telefonlarda yerleşik ve bedavadır, ücretli
  bir harita/konum API'sine gerek yok. Backend'de sadece kullanıcının konumu ile
  fabrikanın sabit koordinatı arasında basit bir mesafe hesaplaması yapılacak.
  Proje için **her şeyin bedava teknolojilerle** yapılması gerektiği tekrar vurgulandı
  (kullanıcı limitli bütçeyle çalışıyor).
- **Bağlantı kopması senaryosu:** Konum sinyali kısa süreliğine kesilip kısa sürede
  tekrar aynı yerde yakalanırsa, bu gerçek bir çıkış (Clock Out) sayılmamalı — aynı
  oturumun devamı gibi düşünülmeli. Yani küçük/geçici kopmalar için bir tolerans
  mantığı olmalı. (Kesin süre/parametre henüz belirlenmedi, teknik detay olarak
  ileride konuşulacak.)
- **Tek zaman damgası kararı:** Clock In / Clock Out **tek bir gerçek zaman kaydı**
  olacak (yani "planlanan shift saati" ile "gerçek giriş/çıkış saati" ayrımı fikri
  tartışıldı ama kullanıcı bunu sadeleştirdi — bkz. Bölüm 2).

---

## 2. Overtime (Fazla Mesai) — ayrı bir kavram/modül

- İlk düşünülen "planlanan shift saati vs. gerçek clock in saati" ayrımı yerine,
  kullanıcı daha basit bir çözüme karar verdi:
  - Clock In / Clock Out **tek gerçek saat** olarak kalır.
  - Eğer çalışan fazladan çalıştıysa (overtime), bunu **kendisi ayrıca beyan eder**
    uygulama üzerinden (örn. "yarım saat overtime'ım var", "bir saat overtime'ım var").
  - Bu beyan, gerçek Clock In/Out saatleriyle karşılaştırılıp kontrol edilebilir
    (yani kimse gerçek dışı overtime beyan edemez, çünkü gerçek giriş/çıkış zaten kayıtlı).
- **Nereye konumlanacak:** Overtime, Clock In/Out ile aynı genel modülün
  (Attendance) içinde ama **ayrı bir ekran/pencere** olacak. Örn. uygulamada
  overtime bildirmek için ayrı bir form/pencere açılır.
- Çalışan kendi geçmiş saatlerini görebilecek: ne kadar çalışmış, ne kadar overtime
  yapmış — bu **aylık** olacak (maaş ödemesi aylık olduğu için).
- **Yeni eklenen fikir — "kim ne kadar saat çalışmış" takibi:** Bu, ayrı bir alt
  başlık/modül olarak eklenebilir (tam olarak hangi modüle gireceği henüz netleşmedi,
  muhtemelen yine Attendance/Overtime alanının bir parçası). Amaç: genel olarak
  kimin ne kadar çalıştığının takip edilebilmesi.
- **Menajer görünürlüğü:** Her menajer **sadece kendi sorumlu olduğu bölgedeki/
  departmandaki çalışanların** Clock In/Out ve çalışma saatlerini görebilir —
  başka bölgelerin/departmanların çalışanlarını göremez. (Bu, Bölüm 1'deki genel
  "her manager sadece kendi bölgesini görür" prensibiyle tutarlı.)

---

## 3. Operatör ana ekranı — genel tasarım fikri

Kullanıcının hayal ettiği akış (henüz taslak, ama net bir yön var):

- Uygulama açıldığında **direkt breakdown (arıza) raporlama ekranı** karşısına
  çıkmalı — bu, en basit ve en hızlı erişilebilir ana işlev olacak. Operatör
  hiçbir menüye girmeden direkt arıza bildirebilmeli.
- **Clock In / Clock Out ve çalışma saatleri** ise ana ekranda değil, sol üstteki
  **üç çizgili (hamburger) menüden** açılan bir yan panelde olacak:
  - Orada bağlantı durumunu görebilecek (örn. yeşil ışık = online/bağlı).
  - Clock In / Clock Out butonunu görecek, şu an clock-in yapmış mı görebilecek.
  - O ayki toplam çalışma saatini direkt görecek (varsayılan görünüm).
  - Takvimden geçmiş aylara/tarihlere bakarak geçmiş çalışma saatlerini de
    inceleyebilecek.
  - Overtime bilgilerini de burada görecek/girebilecek.
- **Tasarım prensibi (tekrar vurgulandı):** Arayüz olabildiğince **sade** olmalı,
  gereksiz karmaşa istenmiyor. Bu tüm proje için geçerli genel bir ilke.

---

## 4. Breakdown (Arıza) Raporlama akışı — operatör tarafı

Bu, kullanıcının üzerinde en çok durduğu, adım adım netleştirdiği akış:

### 4.1 Makine/parça seçimi

- QR kod veya barkod okutma gibi yöntemler düşünülmüş ama kullanıcı bunun yerine
  **basit seçim listesi** istiyor:
  1. Önce **Line (Hat)** seçilir — örn. "Line 1". Hatlar admin panelinden
     menajer tarafından tanımlanacak (bkz. Bölüm 1, Admin Panel CRUD).
  2. Hat seçildikten sonra, o hattın **parçaları** ikonlarla listelenir — örn.
     conveyor (konveyör), sieve/elek (Türkçe transkriptte "Sheemer" olarak geçti,
     muhtemelen "sifter/sieve" — elek benzeri bir ekipman) gibi.
  3. Operatör, görsel/ikon üzerinden ilgili parçayı seçer.
- **İkon kaynağı sorunu ve çözümü:**
  - Genel/bilinen makine parçaları için **hazır bir ikon kütüphanesi** olacak
    (örn. 30-40 kadar genel endüstriyel ikon: konveyör, motor, pompa, tank,
    elek vb.). Menajer yeni bir parça eklerken bu hazır listeden seçer.
  - Eğer parça bu genel kategorilere uymuyorsa (çok özel/bilinmeyen bir parça),
    menajer **kendi çektiği gerçek bir fotoğrafı** ikon yerine yükleyebilir.
    Yani ikon zorunlu değil — gerçek fotoğraf da ikon görevi görebilir.
  - Tam animasyon/görsel hat şeması gibi fikirler de konuşuldu ama şimdilik
    **çok fazla iş** olacağı için ertelendi, basit ikon yaklaşımı tercih edildi.

### 4.2 Rapor detayları

- Serbest metin alanı: operatör sorunu yazarak tarif edebilir.
- Fotoğraf ekleme: **sınırsız sayıda** fotoğraf yükleyebilir (dosya boyutu gibi
  teknik limitler ileride ayrıca konuşulacak).
- **Önem derecesi / severity:** Arızanın üretimi **tamamen durdurup durdurmadığı**
  işaretlenecek (örn. kritik/tam durdurma vs. küçük sorun). Kesin seçenek isimleri
  ve sayısı henüz netleşmedi.
- Gönder butonu ile rapor gönderilir.

### 4.3 Rapor gönderildikten sonra — atama mantığı

- Rapor **direkt belirli bir mühendise atanmaz** — bir **havuza (pool)** düşer.
- **Boşta olan (müsait) herhangi bir mühendis** bu raporu havuzdan görüp
  üstlenebilir/alabilir.
- **Önemli:** Operatörler hangi mühendisin boşta/müsait olduğunu **göremez** —
  bu bilgi operatöre gösterilmez, sadece mühendisler kendi müsaitlik durumlarını
  bilir/sistem bilir.
- **Alternatif atama yolu:** Menajer isterse raporu belirli bir mühendise
  **direkt yönlendirebilir** (havuza bırakmak yerine manuel atama yapabilir).

---

## 5. Açık kalan / henüz konuşulmamış noktalar

- Mühendis tarafı: rapor havuzdan alındıktan sonra mühendisin ekranında ne
  göreceği, işi nasıl üstleneceği, adım adım süreç — **henüz konuşulmadı**,
  sıradaki konu bu.
- Konum tabanlı Clock In/Out için kesin yarıçap değeri (500m mi, daha az mı).
- Bağlantı kopması toleransı için kesin süre/parametre (teknik detay, ileride).
- "Kim ne kadar saat çalışmış" takibinin tam olarak hangi modül/ekran altında
  yer alacağı netleşmedi.
- Breakdown önem derecesi (severity) seçeneklerinin tam listesi netleşmedi
  (şu an sadece "üretimi tamamen durduruyor mu, küçük sorun mu" ayrımı var).
- Fabrika planı (görsel) — kullanıcı hâlâ göndermedi, hatırlatılmalı.

---

## 6. Sırada ne var

Kullanıcının kendi ifadesiyle bir sonraki adım:

> Mühendis havuzdan işi aldığında ekranında ne görecek, ne yapacak — adım adım,
> ekran ekran devam edilecek.

---

*Bu dosya, konuşmanın devamı sırasında yeni bölümler (Bölüm 3, 4, ...) ile
genişletilecektir. Her bölüm ayrı bir dosya olarak projeye eklenecek.*
