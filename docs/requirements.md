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
- Giriş sonrası panel, veri bulunmadığında bile rol bazlı hızlı işlemler ve boş durum açıklaması göstermelidir.
- Kullanıcıya gösterilen Identity, doğrulama ve iş kuralı hataları Türkçe olmalıdır.
- Kayıt oluşturma ekranı yalnızca Admin rolüne açık olmalı; Admin, e-posta ve şifre belirleyerek Admin, Antrenör, Ebeveyn veya Sporcu hesabı oluşturabilmelidir.
- Sporcu hesabı oluşturulduğunda sporcu, kendi hesabıyla giriş yaparak TC kimlik no, telefon no, 2. telefon numarası, ebeveyn no, adres, spor branşı, doğum tarihi, sporcu notu ve görsel bilgilerini profil ekranında tamamlamalıdır.
- Antrenör kelime havuzunda kendi kelimelerini ekleyebilmeli, düzenleyebilmeli, pasife alabilmeli ve erişebildiği sporculara kelime atayabilmelidir.
