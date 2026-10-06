# Mainty — Sohbet Notları (Bölüm 4)

## 0. Bu dosyanın amacı

Bu dosya `Mainty_Sohbet_Notlari_01.md`, `02.md` ve `03.md` dosyalarının **devamıdır**,
onların yerine geçmez. Yeni bir chat açıldığında proje içindeki tüm
`Mainty_Sohbet_Notlari_XX.md` dosyaları sırayla okunmalı.

Bu bölüm, operatör arayüzü için hazırlanan **HTML prototip** (`mainty_operator_prototype.html`)
üzerinde yapılan tasarım kararlarını özetler.

---

## 1. ÖNEMLİ — Bu prototipin statüsü

**Kullanıcı açıkça belirtti:** HTML prototipteki eleman yerleşimi (header, menü, buton
konumları, dil seçici vb.) şu an için **genel bir görsel referans**tır — kesin/final
UI spesifikasyonu değildir.

- Aşağı yukarı böyle görünecek, elementlerin yerleşimi bu şekilde olacak.
- Ancak **gerçek uygulamada** bu farklı olabilir — buton konumları, metinler,
  detaylar değişebilir.
- Bu prototip, konuşulan iş akışlarını (breakdown raporlama, clock in/out, dil seçimi)
  görsel olarak deneyimlemek için yapıldı, birebir uygulanacak final tasarım değil.

**Sonraki AI/geliştirici bunu unutmamalı:** prototipteki pixel-level yerleşim kararları
zorunlu değil, konsept/akış olarak referans alınmalı.

---

## 2. Prototipte varılan yerleşim kararları (referans amaçlı)

Uzun bir deneme-yanılma sürecinden sonra (birden fazla revizyon), şu anki durum:

### Ana sayfa (home) header'ı
- Sol üstte hamburger menü ikonu.
- Ortada "Hi, Deniz" / "Operator" başlığı.
- Sağ üstte **sadece metin** olarak Clock durumu gösteriliyor: **"Clocked In"** veya
  **"Clocked Out"** — bu metin tıklanabilir değil, sadece bilgi amaçlı.
- Onun yanında **dil seçici** (tek buton, örn. "🇬🇧 EN ▾", tıklanınca açılıp diğer
  dilleri gösteriyor: PL, BG, ES).
- Bu iki öğe (durum metni + dil seçici) `floating-controls` olarak konumlandırıldı,
  panel/menü açılsa bile üstte kalacak şekilde (yüksek z-index).

### Menü (hamburger ile açılan yan panel)
- Panelin kendi header'ında ("Deniz Aksoy / Operator" bilgisinin yanında) **gerçek,
  tıklanabilir bir Clock In/Out butonu** var — asıl aksiyon burada gerçekleşiyor.
- Butona basınca yeşil (Clock In durumu, "clocked out" iken buton "Clock In" yazıyor)
  veya sarı (Clock Out durumu, "clocked in" iken buton "Clock Out" yazıyor) renk alıyor.
- Ana sayfadaki durum metniyle senkron çalışıyor — menüden basınca ikisi de güncelleniyor.
- Panelde ayrıca: aylık saat özeti, takvim, overtime bildirme formu var.

### Breakdown raporlama akışı
- Ana sayfada büyük "Report Breakdown" kartı → Line seç → parça seç (ikonlu) →
  açıklama + fotoğraf + önem derecesi → gönder.
- Gönderdikten sonra "Havuzda" durumu, birkaç saniye sonra otomatik "mühendis üstlendi"
  durumuna geçiyor (simülasyon).

### Renk paleti
- Kahverengi, yeşil, beyaz, sarı tonları (açık/sıcak tema).
- Sarı: ana aksiyon renkleri (submit, clock in butonu vb.)
- Yeşil: bağlı/tamamlandı/olumlu durumlar
- Kahverengi: başlıklar, kritik uyarılar

### Dil desteği
- İngilizce (varsayılan), Lehçe, Bulgarca, İspanyolca — tüm arayüz metinleri,
  parça isimleri, takvim, overtime seçenekleri dile göre değişiyor.

---

## 3. Yerleşim kararlarının geçmişi (neden bu şekle geldi)

Bu konuda birkaç tur yanlış anlaşılma yaşandı, gelecekte tekrar karışmaması için not:

1. İlk halde Clock In/Out butonu ana header'daydı.
2. Kullanıcı "sadece ana sayfada metin olsun, buton olmasın" dedi.
3. Buton menü paneline taşındı, ama önce panelin *içine* (gövdesine) konuldu — kullanıcı
   bunu istemedi, "ana header'da olsun" dedi.
4. Buton tekrar ana header'a taşındı, ama panel açıkken header'ın panelin **arkasında**
   kalıp kesilmesi sorun oldu.
5. Son olarak kullanıcı netleştirdi: buton **menünün kendi header'ında** (Deniz Aksoy
   bilgisinin yanında) olacak; ana sayfa header'ında ise sadece **durum metni**
   ("Clocked In"/"Clocked Out") görünecek, buton değil.

Bu son karar uygulandı ve kullanıcı onayladı.

---

## 4. Sırada ne var

- Mühendis tarafının ekran akışı hâlâ tam detaylandırılmadı (havuzdan işi aldığında
  ne görüyor, adım adım).
- Fabrika planı (görsel) hâlâ gönderilmedi.
- Standart tamir formu fotoğrafı hâlâ gönderilmedi.
- BRC Food Certificate uyumluluğu detayları netleşmedi.
- İmza akışı netleşmesi gereken nokta: tek seferlik kayıtlı imza mı, her kapanışta
  yeniden mi atılacak (bkz. Bölüm 3).

---

*Bu dosya, konuşmanın devamı sırasında yeni bölümler ile genişletilecektir.*
