# CFI App

County Food Ingredients (Dennis Road, Widnes, UK) için fabrika bakım + operasyon platformu.
Sıfırdan yazılıyor. Eski `Mainty` projesi sadece **referans**, kod olarak taşınmıyor.

> **Bu bir ÜRETİM uygulaması, MVP değil.** Gerçek çalışanlar, maaşı etkileyen mesai kayıtları,
> BRC denetimine giren bakım kayıtları. Mock veri yok, simülasyon yok, yer tutucu ekran yok.
> Test / yetki testi / sayfalama / doğrulama / hata durumu **fazın parçasıdır**, sonraya bırakılmaz.
> Tam liste: `docs/ROADMAP.md` → "Üretim standardı — Definition of Done".

## Stack

- **Backend:** .NET 10, ASP.NET Core Web API, EF Core 10, Npgsql
- **DB:** PostgreSQL — `Host=localhost;Port=5434;Database=cfiAppDb;Username=dev;Password=dev1234`
  (İleride SQL Server'a geçiş mümkün → PostgreSQL'e özel tip/raw SQL kullanma, UTC + `DateTimeOffset`)
- **Auth:** JWT access token + rotating refresh token, permission bazlı authorization
- **Web:** React + Vite + **JavaScript** (TypeScript değil) + **Tailwind** (kendi token'larımızla, hazır bileşen kütüphanesi yok) — `web/`
- **Mobil:** React Native, JavaScript (`mobile/`, Faz 11'de başlar)
- **Repo:** tek repo (monorepo) — `backend/` + `web/` + `mobile/`
- **Diller:** EN (varsayılan), PL, BG, ES

## Önce oku

1. `docs/ROADMAP.md` — fazlar, ne yapıldı ne kaldı, hangi fazda hangi model
2. `docs/DECISIONS.md` — kesinleşmiş tüm iş kuralları ve tasarım kararları

Orijinal müşteri dokümanları ve HTML prototipler **depoda değil** — County Food
Ingredients'ın iç belgeleri, GitHub'a girmiyorlar. İçlerinden çıkan her karar yukarıdaki iki
dosyada özetli. Bir ayrıntı eksikse kullanıcıya sor; olmayan bir klasörü arama.

## Kurallar

- **Varsayım yapma, sor.** Kararı kullanıcı verir.
- Adım adım ilerle: tek seferde 1–3 dosya. Toplu 10 dosyalık değişiklik yapma.
- İlgisiz dosyalara dokunma. Ne değiştiğini kısaca açıkla.
- Kod, değişken adı, yorum ve UI metinleri **İngilizce**. Dokümanlar ve sohbet **Türkçe**.
- **UI ilkesi:** sadelik. Az buton, az geçiş, az yazı, çok ikon. Yeterli bilgi, gereksiz karmaşa yok.
- **Auth ilkesi:** yetki reddi = **403**, sadece geçersiz/eksik token = **401**.
  Frontend yalnızca 401'de refresh dener / logout eder.
- `X-UserId`, `HttpContext.Items["CurrentUser"]`, `RequireRoles<T>()` **asla yazılmaz** —
  tek kaynak `HttpContext.User` claims.
- Yetkilendirme rol string'i ile değil, **permission** ile yapılır.
- Foto/dosya `IFileStorage` arkasında (lokal disk → ileride S3/Azure).

## Model politikası (token verimliliği)

- **Opus:** mimari, veri modeli, güvenlik, durum makinesi, offline sync, zor debug, kod review
- **Sonnet:** var olan desenin tekrarı, CRUD, form/liste ekranı, DTO, test, migration, çeviri
- **Haiku:** yeniden adlandırma, format, tek satırlık düzeltme

Yöntem: faz başında Opus tasarlar + **ilk referans dosyayı** yazar → `/model sonnet` ile geçilir →
Sonnet aynı deseni çoğaltır → faz sonunda Opus 1 kez gözden geçirir.
Her faz için yeni oturum / `/clear`.

## Renk paleti (prototiplerden)

`#6B4A2E` kahve (başlık/kritik) · `#4E7A3B` yeşil (olumlu/tamamlandı) · `#E7A93A` sarı (ana aksiyon) · `#F5EFE2` krem zemin
