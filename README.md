# Sporcu Gelişim Platformu

Blazor Web App Interactive Server ile geliştirilen sporcu motivasyon, oturum ve geri bildirim yönetim sistemi.

## Teknolojiler

.NET 10, ASP.NET Core Identity cookie authentication, EF Core SQL Server, SQL Server LocalDB, FluentValidation, Serilog, Bootstrap ve xUnit.

## Çalıştırma

Visual Studio ile `SporcuGelisimPlatformu.sln` dosyasını açın, startup project olarak `SporcuGelisim.Web` seçin.

```powershell
dotnet tool restore
dotnet restore
dotnet build
dotnet test
dotnet ef database update --project src/SporcuGelisim.Infrastructure --startup-project src/SporcuGelisim.Web
dotnet run --project src/SporcuGelisim.Web
```

## Admin Seed

Admin parolası kodda tutulmaz. Development için:

```powershell
cd src/SporcuGelisim.Web
dotnet user-secrets set "SeedAdmin:Email" "admin@example.local"
dotnet user-secrets set "SeedAdmin:Password" "ChangeMe-12345!"
```

Bu değerler yoksa admin seed atlanır ve log yazılır.

## Solution Yapısı

- `src/SporcuGelisim.Domain`
- `src/SporcuGelisim.Application`
- `src/SporcuGelisim.Infrastructure`
- `src/SporcuGelisim.Web`
- `tests/SporcuGelisim.UnitTests`
- `tests/SporcuGelisim.IntegrationTests`

## Bilinen Sınırlamalar

MVP ekranları sade tutuldu. Gelişmiş tree editing, kapsamlı admin kullanıcı ekranları ve dosya cleanup job sonraki iterasyonda genişletilmelidir.

## Production Notları

Connection string ve seed secretları environment/secret store üzerinden verilmeli, HTTPS zorunlu kalmalı, gerçek e-posta sağlayıcısı ve saklama/imha süreçleri yapılandırılmalıdır.
