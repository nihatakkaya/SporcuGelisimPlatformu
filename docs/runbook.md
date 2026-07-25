# Runbook

```powershell
dotnet tool restore
dotnet restore
dotnet build
dotnet test
dotnet ef migrations add InitialCreate --project src/SporcuGelisim.Infrastructure --startup-project src/SporcuGelisim.Web
dotnet ef database update --project src/SporcuGelisim.Infrastructure --startup-project src/SporcuGelisim.Web
dotnet run --project src/SporcuGelisim.Web
```

Admin seed:

```powershell
cd src/SporcuGelisim.Web
dotnet user-secrets set "SeedAdmin:Email" "<admin-email>"
dotnet user-secrets set "SeedAdmin:Password" "<strong-password>"
```
