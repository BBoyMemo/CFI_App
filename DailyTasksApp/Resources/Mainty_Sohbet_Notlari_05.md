# Mainty — Sohbet Notları (Bölüm 5)

## 0. Bu dosyanın amacı

Bu dosya `Mainty_Sohbet_Notlari_01.md` – `04.md` dosyalarının **devamıdır**, onların
yerine geçmez. Yeni bir chat açıldığında proje içindeki tüm
`Mainty_Sohbet_Notlari_XX.md` dosyaları sırayla okunmalı.

Bu bölüm şunları kapsıyor: (1) kullanıcının yüklediği gerçek CFI kağıt formlarının
analizi, (2) BRC sertifikasyonu ile ilgili yapılan araştırma, (3) operatör
prototipine eklenen **Holiday Request modülü**.

Güncel prototip dosyası: `mainty_operator_prototype.html` (bu chat'te 1410 satıra
çıktı, Holiday modülü dahil).

---

## 1. Reddedilen görev — Site Plan (hatırlatma)

Fabrika yerleşim planı (SVG/HTML diyagram) iki denemede de kullanıcı tarafından
reddedildi ("boş ver" dendi). **Tekrar başlatılmadan önce mutlaka önce neyin yanlış
olduğu sorulmalı.** Site coğrafyası bilgileri ayrı bir transcript'te saklı, gerekirse
oradan geri çağrılabilir.

---

## 2. Kullanıcının yüklediği gerçek kağıt formlar (fotoğraf analizi)

Kullanıcı 3 fotoğraf yükledi: CFI'nin gerçek kullandığı iki form.

### 2.1 Factory Equipment Fault Reporting Log (Section 4.7.2, Issue Date 03/01/2019,
Issue No 002, Authorised by J. Woods) — 2 sayfa

**Sayfa 1 — Operatör/Süpervizör doldurur:**
- Date / Time / Area
- Equipment Needing Attention
- Fault Details
- Reported By / Position, Reported To / Position

**Sayfa 1 devamı — Engineering Section (Site Engineer veya Process Technician doldurur):**
- Able to Repair? Yes/No + Reason *(daha önce app planında yoktu)*
- Contractor Required? Yes/No + Contractor Used *(app'te yoktu — soru: dropdown mu
  serbest metin mi olacak, henüz cevaplanmadı)*
- Corrective Action Required or Taken
- Root Cause *(Corrective Action'dan ayrı, kendi alanı var)*
- Are all tools and parts accounted for? Yes/No

**Sayfa 2 — Tamir & hijyen & imza:**
- **"Intervention into process stream post deodorisation?" Yes/No** — **yeni bir
  hijyen kapısı**, daha önce hiç konuşulmamıştı. Eğer Yes ise ATP Swab Result
  (Pass/Fail) zorunlu; Fail olursa alan resanitize edilip 2. bir ATP swab ile
  tekrar test ediliyor.
  *(Açık soru: bu kontrol her tamirde mi soruluyor, yoksa sadece deodorisation
  sonrası belirli hat/ekipmanlarda mı geçerli? — henüz cevaplanmadı.)*
- Maliyet takibi: Parts Required, Price of Parts, Labour Hrs, Labour Cost/Hr,
  PO Number, Repair Date, Repaired By *(app'te hiç yoktu, yeni eklenmesi gerekiyor)*
- **Çift imza kapısı:**
  - **Production Management Sign Off**: Area/Item Clean & Tidy? Yes/No,
    Released back into service? Yes/No, Date, Sign Name, Position, Print Name
  - **QA Sign Off (REQUIRED IF WORK IS INTRUSIVE)** — aynı alanlar, ama **sadece
    işin "intrusive" olması durumunda** zorunlu.
  - ⚠️ **Önemli düzeltme:** Daha önceki varsayım QA'nın "geçti/kaldı" tarzı bir test
    adımı olduğu yönündeydi — **yanlıştı**. QA'nın gerçek rolü, alanın temiz/düzenli
    olduğunu ve servise geri verilebileceğini onaylayan bir **imza/sign-off**
    adımı, sadece intrusive işlerde devreye giriyor.
    *(Açık soru: "intrusive work" tam olarak ne demek — ürünle temas eden yüzeye
    dokunmak mı, daha geniş bir tanım mı? — henüz cevaplanmadı.)*

### 2.2 Holiday Request Form

Alanlar: Name, Department, Start Date, End Date, Duration (working days),
Date form completed, Signed by Employee, Signed by Manager.

→ Bu form, bu chat oturumunda **prototipe UI olarak işlendi** (bkz. Bölüm 4).

### 2.3 Fault Log ile ilgili henüz cevaplanmamış sorular
1. "Post-deodorisation" ATP swab kontrolü tüm tamirlerde mi soruluyor, yoksa
   sadece belirli hat/ekipmanlarda mı?
2. "Intrusive work" tanımı nedir — QA imzasını zorunlu kılan sınır ne?
3. "Contractor Used" alanı dropdown/liste mi olacak, serbest metin mi?

*(Fault Log modülünün kendisi henüz app'e/prototipe işlenmedi — sadece analiz
yapıldı, yukarıdaki 3 soru netleşince tasarıma geçilecek.)*

---

## 3. BRC Sertifikasyon Araştırması — Sonuç

Kullanıcı, kağıt formların yerine dijital/app tabanlı bir sistem kullanmanın BRC
(BRCGS Global Standard for Food Safety) denetimi açısından ayrı bir onay/sertifika
gerektirip gerektirmediğini sordu. Web araştırması yapıldı.

**Sonuç: Hayır, ayrı bir onay gerekmiyor.** BRC belirli bir yazılım veya "kağıt
zorunlu" şartı koymuyor; dijital/paperless sistemler sektörde zaten yaygın ve kabul
görüyor. Denetim sırasında önemli olan üç şey:

1. **İzlenebilirlik** — kim, ne zaman, ne yaptı; root cause ve corrective action
   net ve aranabilir kayıtlı olmalı.
2. **Onay/imza akışı** — Production Management ve (gerekirse) QA onayının kim
   tarafından ne zaman verildiği dijital ortamda da açıkça görünmeli.
3. **Denetçiye anında gösterilebilirlik** — denetçi geldiğinde belirli bir
   tarih/olay için kayıt saniyeler içinde çağrılabilmeli.

Yani planlanan çift imza akışı (Production + koşullu QA) zaten bu mantığa uygun;
kağıda basıp saklamaya gerek yok, ama fallback olarak istenirse tamamlanmış
formlar kağıda da basılabilir.

---

## 4. Bu oturumda yapılan geliştirme — Holiday Request Modülü

Kullanıcının talebi (sesli mesajdan): operatör tarafında yeni bir menü butonu,
form doldurup gönderme, yönetici onayı bekleyen/onaylanmış/reddedilmiş durumu
görebilme, eski taleplerin de listede durması. Yönetici tarafındaki takvim görünümü
**ayrı, daha sonraki bir aşama** olarak bırakıldı — kullanıcı kesin bir UX
istemiyor, "sen daha iyi/standart olanı bul" dedi.

### 4.1 Eklenenler (mainty_operator_prototype.html)

- **Yeni menü öğesi**: Side panel'de Calendar / Shifts / Notifications altına
  "Holiday" eklendi (güneş ikonlu SVG), `goto('holidayScreen')` ile açılıyor.
- **Yeni ekran (`holidayScreen`)**:
  - **Form**: Start Date + End Date (`<input type="date">`). İkisi de girilince
    otomatik olarak **iş günü sayısı** hesaplanıp sarı bir "duration" kutusunda
    gösteriliyor (hafta sonları hariç, `calcWorkingDays()` fonksiyonu ile).
    Submit butonu ancak geçerli bir tarih aralığı girilince aktifleşiyor.
  - **Gönderince**: talep listeye **"Pending"** (sarı badge) olarak düşüyor,
    ~4.5 saniye sonra demo amaçlı otomatik **"Approved"** (yeşil badge) durumuna
    geçiyor — gerçek uygulamada bu geçiş yöneticinin onay aksiyonuyla olacak.
  - **Geçmiş liste**: iki mock eski kayıt da var — biri "Approved" (yeşil), biri
    "Rejected" (kırmızı) — tüm durum tiplerinin nasıl göründüğünü göstermek için.
    Her kayıtta tarih aralığı, iş günü sayısı, ve "Requested [tarih]" bilgisi var.
- **4 dilde tam çeviri** (EN/PL/BG/ES) — form etiketleri, durum metinleri
  ("Pending"/"Approved"/"Rejected"), "X working day(s)" gibi çoğul/tekil
  metinler dahil. Dil değiştirilince form ve liste otomatik güncelleniyor.
- Test edildi: JS syntax kontrolü, ID benzersizliği, tüm `data-i18n` anahtarlarının
  4 dilde de eşit sette (82 key) tanımlı olduğu, iş günü hesaplama mantığı
  (örn. Salı–Cuma = 4 gün, tam hafta = 5 gün) doğrulandı.

### 4.2 Bilinçli olarak yapılmayanlar / ertelenenler
- **Yönetici tarafı takvim görünümü** — kullanıcının bahsettiği "renkli takvim,
  tarihe basınca kim izinli görünsün" fikri şu an tasarlanmadı. Kullanıcı standart/
  daha iyi bir UX'e açık olduğunu belirtti — bu aşamaya gelince önerilecek.
  Ham fikir: tıklanabilir takvim günleri, seçilen günde o gün izinli olan kişilerin
  listesi.
- Name/Department alanları forma **eklenmedi** — bunlar zaten giriş yapmış
  operatörün profilinden bilinen bilgiler olduğu için tekrar sorulmuyor (kağıt
  formda vardı ama dijitalde gereksiz). Bu bir tasarım kararı olarak not düşüldü,
  istenirse değiştirilebilir.
- "Signed by Employee" / "Signed by Manager" alanları ayrı imza kutusu olarak
  eklenmedi — dijital karşılığı: **Submit** aksiyonunun kendisi operatörün
  imzası, yöneticinin **Approve/Reject** aksiyonu da onun imzası sayılıyor.

---

## 5. Ertelenen konu — "Fold'luklar"

Kullanıcı sesli mesajda "fold'lukları daha sonra konuşuruz, şimdi değil" dedi.
Bu terim ne anlama geliyor **netleşmedi** — muhtemelen daha önce konuşulan
bekleyen bir konuya (belki PPM/planlı bakım ile ilgili) atıfta bulunuyor olabilir.
**Kullanıcı kendisi geri dönene kadar bu konu açılmamalı**; döndüğünde ne demek
istediği netleştirilmeli.

---

## 6. Sırada ne var (güncel liste)

1. Fault Reporting Log modülünün tasarımı — Bölüm 2.3'teki 3 soru netleşmeden
   başlanmamalı.
2. Yönetici tarafı Holiday takvim görünümü (Bölüm 4.2).
3. "Fold'luklar" konusu — kullanıcı kendisi açtığında.
4. Mühendis tarafının ekran akışı hâlâ detaylandırılmadı (Bölüm 4, eski not,
   hâlâ geçerli).
5. FLT Driver rolü detayları hâlâ konuşulmadı (çok eski bir açık madde, hâlâ
   gündemde).
6. İmza akışı: tek seferlik kayıtlı dijital imza mı, her kapanışta yeniden mi
   atılacak — Fault Log'un imza kısmı tasarlanırken bu da netleşmeli.

---

*Bu dosya, konuşmanın devamı sırasında yeni bölümler ile genişletilecektir.*
