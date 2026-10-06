# Mainty — Sohbet Notları (Bölüm 3)

## 0. Bu dosyanın amacı

Bu dosya `Mainty_Sohbet_Notlari_01.md` ve `Mainty_Sohbet_Notlari_02.md` dosyalarının
**devamıdır**, onların yerine geçmez. Yeni bir chat açıldığında proje içindeki tüm
`Mainty_Sohbet_Notlari_XX.md` dosyaları (01, 02, 03...) sırayla okunmalı.

Diğer ilgili dosyalar:
- `Mainty_Project_Handoff.md` — eski teknik handoff
- `CFIA_Mainty_Future_Vision_and_Requirements.md` — genel gelecek vizyonu (başlıkta "CFIA" var ama doğru kısaltma **CFI**'dir)
- `Mainty_Sohbet_Notlari_01.md` — Bölüm 1
- `Mainty_Sohbet_Notlari_02.md` — Bölüm 2

---

## 1. HATIRLATMA — Bekleyen görev: HTML prototip

Kullanıcı, konuştuğumuz ekranların (operatör ana ekranı, breakdown raporlama akışı vb.)
**gerçek tıklanabilir bir HTML prototipini** görmek istiyor — sadece konuşmak değil,
buton düzenini, akışı görsel olarak deneyebilmek istiyor.

**Bu şu an (sesli modda) yapılamıyor.** Kullanıcıya, metin tabanlı Claude sohbetine
geçtiğinde bunun yapılabileceği söylendi. **Bir sonraki metin sohbetinde bu hatırlatılmalı
ve HTML prototip hazırlanmalı** (buton, ekran akışı — henüz veritabanı/backend değil,
sadece görsel/etkileşimli mockup).

---

## 2. Breakdown iş durumu — operatörün görebildikleri (sınırlı görünürlük)

Rapor gönderildikten sonra operatörün göreceği bilgiler kasıtlı olarak **sınırlı**
tutulacak:

- Operatör, kendi bildirdiği arızayı **hangi mühendisin üstlendiğini** görebilir.
- Eğer henüz kimse üstlenmediyse, bunu da görebilir (yani "beklemede" durumu görünür).
- Operatör iş durumunun aşamalarını görebilir ama **sadece günlük/shift bazında** —
  yani sadece o günkü/kendi shift'indeki durumu görür, geçmiş günlerin/başka shift'lerin
  detayına ihtiyacı yok ve gösterilmeyecek.
- **Kim daha fazla/detaylı görür:** Production Manager ve Maintenance Manager gibi
  yöneticiler, tüm makinelerin durumunu (örn. parça bekleyenler dahil) sürekli ve
  kapsamlı şekilde görebilir. Bu, operatörün sınırlı/günlük görünürlüğünden farklıdır.

---

## 3. "Parça bekleniyor" senaryosu — özel bir kapanış/bildirim mantığı

Bu, üzerinde özellikle durulan bir akış:

- Mühendis bir arızayı incelerken sebep olarak **"parça bekleniyor"** durumunu
  seçtiğinde, iş kaydı (work order/report) **aslında kapanmış** sayılır (yani neden
  kapandığı net şekilde belirtilmiş olur: "şu parça bozuk, parça bekleniyor").
- Ama operatöre bununla ilgili **bir kerelik bir bildirim** gider: "parça bekleniyor"
  şeklinde. Operatör bunu görür ama bu **sürekli takip edilen/tekrar tekrar
  görüntülenen bir durum değildir** — bir kere bildirim olarak gelir, sonra operatör
  bunu tekrar tekrar görmez.
- **Production Manager ve Maintenance Manager** ise bu tür "parça bekleyen" tüm
  makineleri/işleri **sürekli ve kapsamlı** şekilde görebilir — onlar için bu geçici
  bir bildirim değil, takip edilmesi gereken kalıcı bir liste/görünüm olacak.

---

## 4. Standart tamir formu — HATIRLATMA (bekleyen içerik)

- Kullanıcının işyerinde kullanılan **standart bir tamir formu** var (muhtemelen
  mevcut Mainty/maintenance sürecinde de kullanılıyor).
- Kullanıcı bunu **daha sonra fotoğraf çekerek gösterecek** — şu an işte değil.
- **Bu hatırlatılmalı**, form içeriği geldiğinde work order / breakdown raporu
  alanlarının bu standart forma uygun olacak şekilde tasarlanması gerekecek.

---

## 5. BRC Food Certificate uyumluluğu — ÖNEMLİ, unutulmamalı

- Kullanıcının işyeri her yıl **BRC Food Certificate** denetiminden geçiyor.
- Bu denetimde bir **puanlama/derecelendirme sistemi** var (örn. "A" veya "AA" gibi
  seviyeler — kullanıcı tam terminolojiyi netleştirmedi, ama bir skorlama olduğu
  kesin).
- **Uygulamanın bu denetim standardına uygun olması gerekiyor.** Bu, maintenance
  kayıtlarının (work order geçmişi, tamir formları, imza/onay süreçleri gibi)
  denetlenebilir/izlenebilir olması gerektiği anlamına geliyor.
- **Bu madde özellikle "unutma, dosyalara yaz" diye vurgulandı** — ileride hem
  form tasarımı hem de raporlama/audit trail özellikleri BRC gereksinimleriyle
  karşılaştırılıp kontrol edilmeli.

---

## 6. İmza akışı — work order'ın kapanışı

Mühendisin tamiri tamamlamasından sonraki son adım netleşti:

1. Mühendis tamiri yapar (örn. teknisyen/mühendis gelip arızayı giderir).
2. Mühendis işin tamamlandığını sisteme işaretler/onaylar, gerekli alanları doldurur.
3. Bu noktada **operatöre imza için bildirim gider** — "tamir tamamlandı, onaylamak
   için imzala" gibi.
4. Operatör telefon ekranında **parmağıyla bir kere imza atar**.
5. İmza atıldığı anda **work order / breakdown raporu kapanmış olur** — imza,
   kapanışın son adımı ve şartıdır.

**İmza ile ilgili detaylar:**
- Operatörün **her seferinde yeniden imza atmasına gerek yok** — imza bir kere
  alınıp sistemde saklanabilir (yorumdan çıkarılan mantık: imza bir nevi kayıtlı
  bir "onay imzası" gibi davranabilir, her work order kapanışında parmakla
  aynı hareketi tekrar tekrar yapması istenmiyor gibi anlaşıldı — ancak bu nokta
  **tam netleşmedi**, teknik olarak her kapanışta gerçekten yeni bir imza atılıp
  atılmayacağı mı yoksa kayıtlı imzanın mı kullanılacağı ileride netleştirilmeli).
- Operatör isterse imzasını **değiştirebilir** (yeni bir imza kaydedebilir).

> **Not (netleştirilmesi gereken nokta):** Kullanıcının anlatımında hem "iki defa
> imza atmasın, bir kere attığında kayıtlı olsun" hem de "tamir tamamlandığında
> operatöre gidip imzalıyor, imza attığında rapor kapanıyor" ifadeleri birlikte
> geçti. Bu iki fikir birbiriyle biraz gerilimli olabilir (her kapanışta imza
> atılması mı gerekiyor, yoksa tek seferlik kayıtlı imza yeterli mi). **Bir sonraki
> konuşmada bu netleştirilmeli:** İmza her work order kapanışında yeniden mi
> atılacak (hızlıca, önceden kayıtlı imza görselinin üstünden onay gibi), yoksa
> gerçekten tek seferlik bir "dijital imza kaydı" oluşturulup bir daha hiç
> istenmeyecek mi?

---

## 7. Açık kalan / henüz konuşulmamış noktalar

- HTML prototip — bir sonraki metin sohbetinde yapılacak (bkz. Bölüm 1 burada).
- Standart tamir formunun içeriği — kullanıcı fotoğraf gönderecek.
- BRC Food Certificate'ın tam puanlama/seviye sistemi ve bunun uygulamaya
  tam olarak nasıl yansıyacağı netleşmedi.
- İmza mantığı: tek seferlik kayıtlı imza mı, yoksa her kapanışta yeniden atılan
  imza mı — netleştirilmeli (bkz. Bölüm 6 notu).
- Mühendis tarafının tam ekran akışı (havuzdan işi aldığında ne görüyor, nasıl
  ilerliyor) hâlâ detaylı olarak konuşulmadı — bu Bölüm 2'den beri bekleyen konu,
  kısmen bu bölümde (parça bekleme, kapanış, imza) değinildi ama tam ekran ekran
  akış hâlâ netleşmedi.
- Fabrika planı (görsel) — kullanıcı hâlâ göndermedi.

---

*Bu dosya, konuşmanın devamı sırasında yeni bölümler (Bölüm 4, ...) ile
genişletilecektir. Her bölüm ayrı bir dosya olarak projeye eklenecek.*
