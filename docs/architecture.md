# Mimari

Katmanlar:
- `Domain`: Entity, enum ve temel sabitler. Başka projeye referans vermez.
- `Application`: DTO, request modelleri, servis arayüzleri, exception ve validasyonlar.
- `Infrastructure`: EF Core DbContext, Identity user, SQL Server mapping, seed, audit, file storage ve servis implementasyonları.
- `Web`: Blazor UI, Identity sayfaları, DI ve authentication/authorization startup.

Authentication cookie tabanlı ASP.NET Core Identity ile yapılır. Athlete self-registration sırasında kullanıcıya Athlete rolü atanır ve `AthleteProfile` oluşturulur.

Authorization yaklaşımı rol policyleri ve servis seviyesinde object-level kontrolün birleşimidir. Coach/Parent için `AthleteRelation` aktif ilişki şartı aranır.

DbContext Blazor circuit boyunca saklanmamalıdır. Infrastructure `AddDbContextFactory` ve scoped DbContext kayıtlarını içerir; servisler kısa ömürlü scope içinde çalışır.

Fotoğraf depolama dosya sistemi metadata + güvenli dosya adı yaklaşımıyla yapılır. Content-Type, uzantı, imza ve boyut kontrolü uygulanır.
