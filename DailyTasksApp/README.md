# Daily Tasks

Bakım ekibi için günlük görev ve basit sipariş takibi. CFI App'ten tamamen bağımsız.
Kararlar: [docs/DECISIONS.md](docs/DECISIONS.md)

```
DailyTasksApp/
  backend/   ASP.NET Core 10 Web API + EF Core + PostgreSQL, entegrasyon testleri
  web/       React + Vite + JavaScript + Tailwind
  mobile/    React Native (Android APK)
  docker-compose.dev.yml   kendi PostgreSQL container'ı (port 5435)
```

## Çalıştırma (geliştirme)

```powershell
# 1) Veritabanı (bir kez; sonra Docker açıldıkça kendisi başlar)
docker compose -f docker-compose.dev.yml up -d

# 2) API  -> http://localhost:5278   (migration'lar ve ilk Manager otomatik)
cd backend
dotnet run --project src/DailyTasks.Api --launch-profile http

# 3) Web  -> http://localhost:5174
cd web
npm install
npm run dev
```

İlk giriş: **Manager / Manager123!** → *Team* sekmesinden Engineer'ları ekleyin.

## Telefon (APK)

1. API'yi ağa açık başlatın:
   `dotnet run --project src/DailyTasks.Api --launch-profile lan`
   (Windows Güvenlik Duvarı izin sorarsa **Özel ağ** için izin verin.)
2. Bilgisayarın yerel IP'sini bulun: `ipconfig` → IPv4 (örn. `192.168.1.20`).
3. APK'yı telefona kopyalayıp kurun (bilinmeyen kaynaklara izin vermek gerekir):
   `dist/DailyTasks-1.0.apk`
4. Giriş ekranında sunucu adresi: `http://192.168.1.20:5278`

APK'yı yeniden derlemek:

```powershell
cd mobile
npm install
cd android
.\gradlew.bat assembleRelease
```

## Testler

```powershell
cd backend
dotnet test          # Docker açık olmalı (Testcontainers ayrı bir PostgreSQL açar)
cd ../web
npm run check:i18n   # 4 dilde aynı anahtarlar var mı
```

## Tek sunucu kurulumu (ileride)

`web` derlenip (`npm run build`) çıktısı `backend/src/DailyTasks.Api/wwwroot/` içine
kopyalanırsa API hem arayüzü hem `/api`'yi aynı adresten sunar. Canlıda `Jwt:Key`,
`ConnectionStrings:Default`, `Bootstrap:*` ortam değişkeni / gizli ayar olarak verilmeli
ve HTTPS kullanılmalı.
