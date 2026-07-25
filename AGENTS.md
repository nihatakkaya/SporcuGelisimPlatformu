# AGENTS

- Build: `dotnet build`
- Test: `dotnet test`
- Migration: `dotnet ef migrations add <Name> --project src/SporcuGelisim.Infrastructure --startup-project src/SporcuGelisim.Web`
- UI dili Türkçe, C# isimleri İngilizce olmalıdır.
- API, JWT ve mikroservis kullanılmaz.
- Yetkilendirme testleri zorunludur; yalnızca `[Authorize]` yeterli değildir.
- Secretlar repositoryye yazılmaz.
- Her değişiklikten sonra restore/build/test çalıştırılır.
- Gereksinim değişiklikleri `docs/requirements.md` içine işlenir.
