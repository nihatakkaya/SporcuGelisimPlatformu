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
- Giriş ekranında `Rol bazlı` ve `Güvenli` tanıtım kutuları gösterilmemelidir.
- Giriş ekranındaki okul logosu görünür boyutta olmalı ve yüklenen resmi kullanmalıdır.
- Sayfa açılışında başlıklara odak verildiğinde kullanıcıya siyah odak çerçevesi gösterilmemelidir.
- Development admin kullanıcısı yalnızca `SeedAdmin:Email` ve `SeedAdmin:Password` user-secrets değerleri üzerinden seed edilir.
- Giriş sonrası ana sayfa, veri bulunmadığında bile rol bazlı hızlı işlemler ve boş durum açıklaması göstermelidir.
- Başarılı giriş sonrası kullanıcı, önceki yasaklı `returnUrl` değerine değil rolüne uygun ana sayfaya yönlendirilmelidir.
- Kullanıcıya gösterilen Identity, doğrulama ve iş kuralı hataları Türkçe olmalıdır.
- Kayıt oluşturma ekranı Admin ve Antrenör rollerine açık olmalıdır; Admin, e-posta ve şifre belirleyerek Admin, Antrenör, Ebeveyn veya Sporcu hesabı oluşturabilmelidir.
- Kayıt oluşturma ekranı Antrenör rolüne de açık olmalı; Antrenör yalnızca Sporcu ve Ebeveyn hesabı oluşturabilmeli, Antrenör veya Admin hesabı oluşturamamalıdır.
- Antrenör tarafından oluşturulan sporcu hesabı, oluşturma anında ilgili antrenörün alt birimine aktif ilişki olarak bağlanmalıdır.
- Yeni oluşturulan hesapların e-posta adresi 6 haneli onay kodu ile doğrulanmalı; e-posta onaylanmadan kullanıcı giriş yapamamalıdır.
- SMTP ayarları secret içermeden yapılandırılmalı; şifre gibi gizli değerler repository içinde tutulmamalıdır.
- SMTP ayarı eksik veya hatalıysa kullanıcıya e-posta gönderildi mesajı verilmemeli; Türkçe hata mesajı gösterilmelidir.
- Sporcu hesabı oluşturulduğunda sporcu, kendi hesabıyla giriş yaparak TC kimlik no, telefon no, 2. telefon numarası (ebeveyn no), adres, spor branşı, doğum tarihi, sporcu notu ve görsel bilgilerini profil ekranında tamamlamalıdır.
- Antrenör kelime havuzunda kendi kelimelerini ekleyebilmeli, düzenleyebilmeli, pasife alabilmeli ve erişebildiği sporculara kelime atayabilmelidir.
- Antrenör, `Sporcularım` ekranında kendisine bağlı sporcuları listeleyebilmeli; sporcu seçildiğinde branş, iletişim, ebeveyn telefonu, adres, doğum tarihi, yaş, profil notu, oturumlar, geri bildirimler ve atanmış kelimeleri görüntüleyebilmelidir.
- Admin paneli ve hesap yönetimi ekranları Türkçe, görsel olarak tutarlı ve hızlı işlem odaklı olmalıdır.
- Admin, sporcuları antrenör veya ebeveyn hesaplarına aktif ilişki olarak atayabilmelidir.
- Sporcu, yalnızca kendisinden sorumlu antrenöre geri bildirim gönderebilmelidir; antrenör sadece kendisine gelen geri bildirimleri görmelidir.
- Sporcu, kendisine atanmış kelime havuzunu görebilmeli ve yeni kelime önerisini sorumlu antrenöre istek olarak gönderebilmelidir. Antrenör isteği onaylarsa kelime antrenör havuzuna eklenmeli ve sporcuya atanmalıdır.
- Üst menüde oturum e-postası yerine normal yazı ağırlığında `Hesabım` bağlantısı gösterilmelidir.
- `Profilim` ve `Hesabım` ayrı menü öğeleri olarak gösterilmemeli; hesap bilgileri, role göre profil bilgileri ve şifre değiştirme işlemi `Hesabım` ekranında birleşmelidir.
- Şifre değiştirme formu `Hesabım` ekranında butonla açılmalı; kullanıcı ayrı bir şifre sayfasına yönlendirilmemelidir.
- Üst menüde `Sporcular` bağlantısı yalnızca Admin ve Antrenör rollerinde bulunmalı; Sporcu ve Ebeveyn rollerinde gizlenmelidir.
- Ebeveyn üst menüsünde yalnızca `Sporcularım` ekranı gösterilmelidir.
- Ana sayfadaki sporcu, antrenör, ebeveyn ve oturum sayıları yalnızca Admin rolüne gösterilmelidir.
- Ana sayfada `Sistem Durumu` ve kurulum özeti paneli gösterilmemelidir; rol bazlı hızlı işlemler korunmalıdır.
- Ana sayfadaki hesap kartında `Aktif bölüm` metni yerine kullanıcının profil fotoğrafı veya baş harfleri ile ad soyadı gösterilmelidir.
- `Sporcular` ve `Sporcularım` listelerinde ad, branş veya telefon ile arama yapılabilmelidir.
- Sporcu adına basıldığında sporcunun profil, iletişim ve branş bilgileri detay sayfasında görüntülenmelidir; oturum, kelime ve geri bildirim detayları yalnızca yetkili ilişki üzerinden gösterilmelidir.
- Admin, branşları ekleyebilmenin yanında düzenleyebilmeli ve güvenli şekilde pasife alarak silebilmelidir.
- Sporcu atama ekranında sporcu ve hesap seçimi aramalı, açılır liste biçiminde olmalı; yazılan harflere göre filtreleme yapılmalıdır.
- Sporcu atama ekranında Sporcu, Antrenör ve Ebeveyn panellerinin her birinde yazılı arama alanı ve `Ara` butonu bulunmalıdır.
- Sporcu-antrenör ve sporcu-ebeveyn ilişki atama işlemi Blazor Server bağlantısına bağlı kalmayan klasik form POST akışıyla çalışmalıdır.
- Sporcu profil güncelleme formu, görsel yüklemede Blazor Server bağlantısına bağımlı kalmamak için yetkili klasik multipart POST akışıyla kaydedilmelidir.
- Sporcu profilinde TC kimlik, spor branşı, telefon no, 2. telefon numarası (ebeveyn no), doğum tarihi ve adres zorunlu olmalıdır; TC kimlik 11 rakam, telefonlar `05` ile başlayan 11 hane ve `0555 555 55 55` formatında saklanmalıdır.
- Sporcu geri bildirimi, antrenör ve mesaj seçimini Blazor Server event akışına bağlı kalmadan klasik form POST ile göndermelidir.
- Antrenör kendi profilinde telefon numarasını ve profil fotoğrafını güncelleyebilmelidir; sporcu kendi sorumlu antrenörünün telefon numarasını ve profil fotoğrafını görebilmelidir.
- Antrenör ve Ebeveyn, ortak sporcu ilişkisi üzerinden birbirine özel geri bildirim gönderebilmelidir; bu mesajlar sporcu hesabına gösterilmemelidir.
- Antrenör kelime havuzu ve sporcu takip ekranları, Blazor Server event sırasında kullanıcı bilgisi düşse bile antrenörün kendi rolü ve aktif ilişkileri üzerinden çalışmalıdır.
- Sporcu kelime havuzu ekranı, sporcuya atanmış kelimeleri ve kelime isteklerini doğrudan oturumdaki sporcu profili üzerinden yüklemeli; antrenör ilişkisi yoksa çökmeden boş durum göstermelidir.
- Ebeveyn hesabı, kendisine atanmış sporcuları ve bu sporcuların temel gelişim detaylarını ayrı bir `Sporcularım` ekranında görüntüleyebilmelidir.
- Antrenör, genel `Sporcular` ekranından seçtiği sporcuyu kendi alt birimine klasik form POST akışıyla ekleyebilmelidir.
- Antrenör, `Sporcularım` ekranında kendi altındaki sporcuyu alt biriminden çıkarabilmeli ve bu işlem eski geri bildirimleri veya kelime atamalarını silmemelidir.
- Antrenör, kendi altındaki sporcuyu aktif bir ebeveyn hesabı ile eşleştirebilmelidir.
- `Hesabım` ekranında kullanıcı adı ve e-posta aynı değer olarak yinelenmemeli; hesap bilgileri bölümünde ad soyad ve mevcut e-posta ayrı gösterilmelidir.
- Kullanıcı `Hesabım` ekranından yeni e-posta adresine 6 haneli onay kodu göndererek e-posta adresini değiştirebilmelidir.
- E-posta onay ekranında yanlış girilmiş e-posta adresine erişemeyen kullanıcı, mevcut e-posta ve şifresini doğrulayıp yeni e-posta adresine onay kodu gönderebilmelidir.
- E-posta onay kodu gelmeyen kullanıcı, onay ekranından aynı e-posta için yeni 6 haneli kod isteyebilmelidir.
- Giriş ekranında bağımsız `E-posta onayı` bağlantısı gösterilmemeli; e-posta onayı yalnızca onay bekleyen kullanıcı giriş yaptığında veya kayıt sonrası yönlendirme ile açılmalıdır.
- `Şifremi unuttum` ekranı tamamen Türkçe olmalı; kullanıcı e-posta adresini girdiğinde sisteme kayıtlıysa yeni geçici şifre bu adrese gönderilmeli ve hesap şifresi bu geçici şifre olarak güncellenmelidir.
- Admin, sporcular listesine ek olarak antrenör ve ebeveyn hesaplarını alfabetik arama listesiyle görüntüleyebilmeli; seçilen hesabın temel bilgileri ve aktif bağlı sporcuları gösterilmelidir.
- Admin menüsünde Antrenörler ve Ebeveynler sayfaları arasında geçiş yapıldığında aynı bileşen yeniden yüklenmeli ve önceki liste türü ekranda kalmamalıdır.
- Antrenör geri bildirim ekranında mesajlar sporcu bazlı akordiyon olarak gruplanmalı; okunmamış gelen mesajı olan sporcularda ana tema yeşiliyle dikkat çekici rozet gösterilmeli ve grup açıldığında bu mesajlar görüldü sayılmalıdır.
- Giriş ekranında dış arka plan ile sol görsel panel arasında aynı fotoğrafın hizasız tekrarından kaynaklanan görsel kayma olmamalıdır.
