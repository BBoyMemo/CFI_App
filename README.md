# CFI App

County Food Ingredients (Dennis Road, Widnes, UK) fabrikası için bakım ve operasyon platformu.

**Bu bir üretim uygulamasıdır, MVP değil.** Gerçek çalışanlarla, maaşı etkileyen mesai
kayıtlarıyla ve BRC denetimine giren bakım kayıtlarıyla çalışacak.

| | |
|---|---|
| Plan | [`docs/ROADMAP.md`](docs/ROADMAP.md) |
| Kararlar / iş kuralları | [`docs/DECISIONS.md`](docs/DECISIONS.md) |
| Geliştirme kuralları | [`CLAUDE.md`](CLAUDE.md) |

## Gereksinimler

- .NET SDK 10.0
- Node.js 22+
- Docker (PostgreSQL ve entegrasyon testleri için)

## Geliştirme ortamı

Veritabanı Docker'daki `local-dev-db` container'ında, **5434** portunda çalışıyor.

```bash
# Veritabanını oluştur (bir kez)
docker exec local-dev-db psql -U dev -d postgres -c 'CREATE DATABASE "cfiAppDb" OWNER dev;'
```

### Backend

```bash
cd backend
dotnet run --project src/CfiApp.Api      # http://localhost:5140
```

Doğrulama:

```bash
curl http://localhost:5140/health/live    # Healthy
curl http://localhost:5140/health/ready   # veritabanı dahil
curl http://localhost:5140/api/v1/system/info
```

Swagger yalnızca Development ortamında: <http://localhost:5140/swagger>

### Web

```bash
cd web
cp .env.example .env
npm install
npm run dev                               # http://localhost:5173
```

### Testler

```bash
cd backend
dotnet test CfiApp.slnx                   # Testcontainers ile gerçek PostgreSQL başlatır
```

```bash
cd web
npm run lint
npm run check:i18n                        # 4 dilde eksik çeviri anahtarı var mı
```

## Telefondan / başka cihazdan açmak (aynı ağ)

Dev sunucusu ağdaki her arayüzü dinliyor ve `/api`'yi backend'e proxy'liyor — üretimdeki
nginx ile aynı şekil. Bu yüzden hiçbir cihaza IP adresi gömmek gerekmiyor ve istekler
tarayıcı açısından aynı origin'den gittiği için CORS devreye girmiyor.

1. Backend'i ve `npm run dev`'i çalıştır.
2. Vite'ın yazdığı **Network** adresini kullan (ör. `http://192.168.0.2:5173`).
3. Telefon aynı wifi'da olmalı.

Sadece **5173** portu ağa açık; API `localhost`'ta kalıyor ve yalnızca proxy üzerinden
erişiliyor.

> `http://` üzerinden açıldığında tarayıcı "secure context" saymaz, bu yüzden
> `crypto.randomUUID()` tanımsızdır. `web/src/api/uuid.js` bunu karşılıyor — yoksa
> uygulama telefonda her istekte hata verirdi.


## Deploy (Faz 10)

Docker Compose ile: API + PostgreSQL + nginx (TLS terminasyonu, statik web dosyaları).

```bash
cp .env.example .env
# .env içinde POSTGRES_PASSWORD, JWT_SIGNING_KEY (openssl rand -base64 64), WEB_ORIGIN doldurulmalı
docker compose up -d --build
```

Migration'lar container başlarken **otomatik uygulanmaz** (`Database__ApplyMigrationsOnStartup=false`,
vardiya ortasında tablo kilitlenmesin diye). API imajı yalnızca derlenmiş DLL'leri içerir, `dotnet ef`
SDK aracını değil — bu yüzden migration ayrı, .NET SDK'nın bulunduğu bir yerden (CI adımı veya
geliştirici makinesi) production connection string'iyle çalıştırılır:

```bash
cd backend
dotnet ef database update -p src/CfiApp.Infrastructure -s src/CfiApp.Api \
  --connection "Host=<prod-host>;Port=5432;Database=cfiAppDb;Username=...;Password=..."
```

Sunucuya SDK kurmadan tek dosyalık bir çalıştırılabilir yeterli olsun istenirse, EF Core'un
`dotnet ef migrations bundle` özelliği bir sonraki adım olarak değerlendirilebilir.

**Doğrulandı:** `backend/src/CfiApp.Api/Dockerfile` gerçekten build edilip çalıştırıldı — non-root
kullanıcı, container healthcheck, Production'da Swagger kapalı (`404`), PostgreSQL bağlantısı sağlıklı.

**Henüz eksik / kullanıcı girdisi gerekiyor:** gerçek domain adı, TLS sertifikası (`nginx/certs/`),
sunucunun nerede duracağı (ROADMAP Faz 10 açık sorusu), web build'inin nginx'e bağlanması (Faz 4/9
sonrası).

## Yapı

```
backend/
  src/CfiApp.Domain          entity, enum, domain kuralları — dış bağımlılık yok
  src/CfiApp.Application     servis, DTO, doğrulama
  src/CfiApp.Infrastructure  EF Core, DbContext, migration
  src/CfiApp.Api             controller, auth, middleware, Program.cs
  tests/CfiApp.Tests         entegrasyon testleri (gerçek PostgreSQL container)
web/                         React + Vite + Tailwind (JavaScript)
mobile/                      React Native (Faz 11)
docs/                        plan, kararlar, veri modeli
```

Orijinal müşteri dokümanları ve prototipler depoda tutulmuyor (müşterinin iç belgeleri);
içlerinden çıkan kararlar `docs/DECISIONS.md` içinde.

## Sabit kurallar

- Yetki reddi **403**, geçersiz/eksik token **401**. Frontend yalnızca 401'de logout eder.
- Tüm zamanlar veritabanında **UTC**; gösterimde `Europe/London`.
- Her liste endpoint'i sayfalı. Audit kaydı append-only, silinmez.
- Secret'lar repoda değil — `.env` ve `appsettings.Production.json` git dışı.
