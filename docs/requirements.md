# Gereksinimler

Sistem sporcu merkezli motivasyon, oturum ve geri bildirim yönetimi sağlar. MVP; Blazor Web App Interactive Server, ASP.NET Core Identity cookie authentication, EF Core ve SQL Server LocalDB ile modüler monolith olarak geliştirilir.

Roller: Admin, Athlete, Coach, Parent. Rol adları `RoleNames` sabitlerinden kullanılmalıdır.

Kritik kurallar:
- Üst menü ve hesap bağlantıları, Blazor Server circuit hatasına düşmeden sayfalar arasında geçiş yapmalı; yeniden bağlantı ekranı kullanıcıya Türkçe gösterilmelidir.
- Coach ve Parent işlemlerinde `AthleteRelation` üzerinden aktif ilişki kontrolü yapılır.
- UI buton gizleme güvenlik değildir; servis katmanı object-level authorization uygular.
- Entity anahtarları Guid, tarih alanları UTC `DateTimeOffset` olmalıdır.
- Entity sınıfları doğrudan form modeli olarak kullanılmaz.
- Secret değerleri repository içinde tutulmaz.
- Giriş ekranı Türkçe olmalı, kullanıcı adı veya e-posta ile giriş yapılmasını desteklemelidir.
- Development admin kullanıcısı yalnızca `SeedAdmin:Email` ve `SeedAdmin:Password` user-secrets değerleri üzerinden seed edilir.
- Giriş sonrası ana sayfa, veri bulunmadığında bile rol bazlı hızlı işlemler ve boş durum açıklaması göstermelidir.
- Başarılı giriş sonrası kullanıcı, önceki yasaklı `returnUrl` değerine değil rolüne uygun ana sayfaya yönlendirilmelidir.
- Kullanıcıya gösterilen Identity, doğrulama ve iş kuralı hataları Türkçe olmalıdır.
- Kayıt oluşturma ekranı yalnızca Admin rolüne açık olmalı; Admin, e-posta ve şifre belirleyerek Admin, Antrenör, Ebeveyn veya Sporcu hesabı oluşturabilmelidir.
- Sporcu hesabı oluşturulduğunda sporcu, kendi hesabıyla giriş yaparak TC kimlik no, telefon no, 2. telefon numarası (ebeveyn no), adres, spor branşı, doğum tarihi, sporcu notu ve görsel bilgilerini profil ekranında tamamlamalıdır.
- Antrenör kelime havuzunda kendi kelimelerini ekleyebilmeli, düzenleyebilmeli, pasife alabilmeli ve erişebildiği sporculara kelime atayabilmelidir.
- Antrenör, `Sporcularım` ekranında kendisine bağlı sporcuları listeleyebilmeli; sporcu seçildiğinde branş, iletişim, ebeveyn telefonu, adres, doğum tarihi, yaş, profil notu, oturumlar, geri bildirimler ve atanmış kelimeleri görüntüleyebilmelidir.
- Admin paneli ve hesap yönetimi ekranları Türkçe, görsel olarak tutarlı ve hızlı işlem odaklı olmalıdır.
- Admin, sporcuları antrenör veya ebeveyn hesaplarına aktif ilişki olarak atayabilmelidir.
- Sporcu, yalnızca kendisinden sorumlu antrenöre geri bildirim gönderebilmelidir; antrenör sadece kendisine gelen geri bildirimleri görmelidir.
- Sporcu, kendisine atanmış kelime havuzunu görebilmeli ve yeni kelime önerisini sorumlu antrenöre istek olarak gönderebilmelidir. Antrenör isteği onaylarsa kelime antrenör havuzuna eklenmeli ve sporcuya atanmalıdır.
- Üst menüde oturum e-postası yerine normal yazı ağırlığında `Hesabım` bağlantısı gösterilmelidir.
- Üst menüde `Sporcular` bağlantısı yalnızca Admin ve Antrenör rollerinde bulunmalı; Sporcu ve Ebeveyn rollerinde gizlenmelidir.
- Ebeveyn üst menüsünde yalnızca `Sporcularım` ekranı gösterilmelidir.
- Ana sayfadaki sporcu, antrenör, ebeveyn ve oturum sayıları yalnızca Admin rolüne gösterilmelidir.
- Ana sayfada `Sistem Durumu` ve kurulum özeti paneli gösterilmemelidir; rol bazlı hızlı işlemler korunmalıdır.
- `Sporcular` ve `Sporcularım` listelerinde ad, branş veya telefon ile arama yapılabilmelidir.
- Sporcu adına basıldığında sporcunun profil, iletişim ve branş bilgileri detay sayfasında görüntülenmelidir; oturum, kelime ve geri bildirim detayları yalnızca yetkili ilişki üzerinden gösterilmelidir.
- Admin, branşları ekleyebilmenin yanında düzenleyebilmeli ve güvenli şekilde pasife alarak silebilmelidir.
- Sporcu atama ekranında sporcu ve hesap seçimi aramalı, açılır liste biçiminde olmalı; yazılan harflere göre filtreleme yapılmalıdır.
- Sporcu-antrenör ve sporcu-ebeveyn ilişki atama işlemi Blazor Server bağlantısına bağlı kalmayan klasik form POST akışıyla çalışmalıdır.
- Sporcu profil güncelleme formu, görsel yüklemede Blazor Server bağlantısına bağımlı kalmamak için yetkili klasik multipart POST akışıyla kaydedilmelidir.
- Sporcu profilinde TC kimlik, spor branşı, telefon no, 2. telefon numarası (ebeveyn no), doğum tarihi ve adres zorunlu olmalıdır; TC kimlik 11 rakam, telefonlar `05` ile başlayan 11 hane ve `0555 555 55 55` formatında saklanmalıdır.
- Sporcu geri bildirimi, antrenör ve mesaj seçimini Blazor Server event akışına bağlı kalmadan klasik form POST ile göndermelidir.
- Antrenör kelime havuzu ve sporcu takip ekranları, Blazor Server event sırasında kullanıcı bilgisi düşse bile antrenörün kendi rolü ve aktif ilişkileri üzerinden çalışmalıdır.
- Sporcu kelime havuzu ekranı, sporcuya atanmış kelimeleri ve kelime isteklerini doğrudan oturumdaki sporcu profili üzerinden yüklemeli; antrenör ilişkisi yoksa çökmeden boş durum göstermelidir.
- Ebeveyn hesabı, kendisine atanmış sporcuları ve bu sporcuların temel gelişim detaylarını ayrı bir `Sporcularım` ekranında görüntüleyebilmelidir.
- Antrenör, genel `Sporcular` ekranından seçtiği sporcuyu kendi alt birimine klasik form POST akışıyla ekleyebilmelidir.
- Antrenör, `Sporcularım` ekranında kendi altındaki sporcuyu alt biriminden çıkarabilmeli ve bu işlem eski geri bildirimleri veya kelime atamalarını silmemelidir.
- Antrenör, kendi altındaki sporcuyu aktif bir ebeveyn hesabı ile eşleştirebilmelidir.
