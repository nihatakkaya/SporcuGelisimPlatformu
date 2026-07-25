# Gereksinimler

Sistem sporcu merkezli motivasyon, oturum ve geri bildirim yönetimi sağlar. MVP; Blazor Web App Interactive Server, ASP.NET Core Identity cookie authentication, EF Core ve SQL Server LocalDB ile modüler monolith olarak geliştirilir.

Roller: Admin, Athlete, Coach, Parent. Rol adları `RoleNames` sabitlerinden kullanılmalıdır.

Kritik kurallar:
- Coach ve Parent işlemlerinde `AthleteRelation` üzerinden aktif ilişki kontrolü yapılır.
- UI buton gizleme güvenlik değildir; servis katmanı object-level authorization uygular.
- Entity anahtarları Guid, tarih alanları UTC `DateTimeOffset` olmalıdır.
- Entity sınıfları doğrudan form modeli olarak kullanılmaz.
- Secret değerleri repository içinde tutulmaz.
- Giriş ekranı Türkçe olmalı, kullanıcı adı veya e-posta ile giriş yapılmasını desteklemelidir.
- Development admin kullanıcısı yalnızca `SeedAdmin:Email` ve `SeedAdmin:Password` user-secrets değerleri üzerinden seed edilir.
