# Gereksinimler

Sistem sporcu merkezli motivasyon, oturum ve geri bildirim yönetimi sağlar. MVP; Blazor Web App Interactive Server, ASP.NET Core Identity cookie authentication, EF Core ve SQL Server LocalDB ile modüler monolith olarak geliştirilir.

Roller: Admin, Athlete, Coach, Parent. Rol adları `RoleNames` sabitlerinden kullanılmalıdır.

Kritik kurallar:
- Üst menü ve hesap bağlantıları, Blazor Server circuit hatasına düşmeden sayfalar arasında geçiş yapmalı; yeniden bağlantı ekranı kullanıcıya Türkçe gösterilmelidir.
- Mobil görünümde üst menüdeki üç çizgi butonu, mevcut tasarımı değiştirmeden menü bağlantılarını açıp kapatabilmelidir.
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
- Pasife alınan sporcu, antrenör veya ebeveyn hesapları sporcu atama listelerinde gösterilmemeli ve klasik form POST ile de yeni ilişkiye atanamamalıdır.
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
- Admin, tek kullanıcı yönetimi ekranında tüm rolleri filtreleyebilmeli; seçilen kullanıcıyı aktif/pasif yapabilmeli, e-posta adresini düzeltebilmeli, e-posta onay kodu gönderebilmeli ve geçici şifre gönderebilmelidir.
- Admin ve Antrenör ekranlarında sporcu ilişkilerinin aktif/pasif geçmişi; ilişki türü, başlangıç ve bitiş tarihleriyle görüntülenebilmelidir.
- Üst menü ve ana sayfa, okunmamış geri bildirimleri ve bekleyen kelime isteklerini küçük yeşil bildirim rozetleriyle göstermelidir.
- Counter ve Weather gibi örnek scaffold sayfaları uygulama rotalarından kaldırılmalı; kullanılmayan Identity harici giriş, passkey, 2FA ve kişisel veri silme ekranları Türkçe kontrollü bilgilendirme ekranı olarak kalmalıdır.
- Admin kullanıcı yönetimi ekranındaki rol filtreleri listeyi gerçekten yeniden yüklemeli; üst menüde ayrı Sporcular, Antrenörler ve Ebeveynler bağlantıları yerine Admin için tek Kullanıcılar bağlantısı kullanılmalıdır.
- Admin geri bildirim ekranında kullanıcı arama/listesi bulunmalı; seçilen kullanıcıya ait geri bildirimler sporcu bazlı akordiyon olarak görüntülenmelidir.
- Ebeveyn geri bildirim ekranında mesajlar sporcu bazlı akordiyon olarak görüntülenmeli ve sayfaya girildiğinde ebeveyne gelen okunmamış geri bildirimler okunmuş sayılmalıdır.

## Sporcuya özel oturumlar ve kelime geçmişi (18.09.2026)

- Antrenörün Sporcularım profilinden ayrı Atanmış Kelimeler ve Oturumlar ekranları açılır. Aktif sorumlu antrenör (ve yönetici) sporcuya toplu kelime ekleyebilir/kaldırabilir; arama, seçim sayısı ve seçimden çıkarma desteklenir.
- Bir görüşme bir oturumdur. Oluşturma tarihi UTC olarak saklanır; oturum tarihi Türkiye gününe göre otomatik atanır ve sonradan düzenlenebilir. Liste sporcuya özel, oluşturulma sırasındaki oturum numarasına göre azalan sıralıdır. Tarihi değiştirmek son oturumun kimliğini değiştirmez.
- Özel antrenör notu ve paylaşılan not ayrı saklanır (en fazla 10.000 karakter). Sporcu yalnızca kendi oturumlarını ve paylaşılan notu okuyabilir; özel not veritabanı sorgusunun DTO projeksiyonunda dışlanır. Ebeveyn oturum detaylarına erişemez. Sporcu oturum veya kelime yönetemez.
- Başlangıç/bitiş kelimeleri adlarıyla birlikte snapshot olarak saklanır. Ekleme/çıkarma olayları sporcu, oturum (varsa), kelime, metin, işlem, kaynak, yapan kullanıcı ve zaman bilgilerini korur. Kelime adı değişse veya kelime pasifleştirilse geçmiş değişmez.
- Kelime değişiklikleri yalnızca en son oluşturulan oturumda yapılır. Önceki oturumların tarih ve notları düzenlenebilir; kelime geçmişleri sabittir. Sonraki oturum güncel atanmış kelimelerle başlar.
- Atanmış Kelimeler ve kelime havuzu ekranlarından yapılan değişiklikler en son oturuma assigned_words, oturumdan yapılanlar session kaynağıyla yazılır. Oturum yoksa değişiklik yine saklanır; ilk oturum güncel kelimeleri devralır.
- Toplu değişiklik ve geçmiş kayıtları tek serializable transaction içinde kaydedilir. Yinelenen ekleme/kaldırma isteği durum değişmediyse yeni olay oluşturmaz. Kaldırıp yeniden ekleme iki gerçek olaydır ve ikisi de gösterilir.
- Eski veriler silinmez. Önceden oluşturulmuş oturumların bilinmeyen başlangıç geçmişi uydurulmaz; mevcut SessionWords kayıtları gösterilir ve geçmişin eksik olabileceği belirtilir. Eski son oturumda yeni işlem yapılırken o andaki kelimeler başlangıç referansı olur.
- Blazor Server, Identity cookie ve mevcut EF Core mimarisi korunur; API, JWT veya mikroservis eklenmez. Yetkilendirme sunucu servislerinde aktif antrenör ilişkisi ve sporcu sahipliği ile doğrulanır.

### Kompakt profil özeti ve kayıt branşı

- Sporcularım profilinde oturumlar tek tek listelenmez. Yalnızca toplam sayı, son oluşturulan oturumun tarihi ve sporcunun Oturumlar sayfasına bağlantı gösterilir. Oturum sayısı arttığında özetin yapısı değişmez; profil sorgusu tüm oturumları yüklemez.
- Kayıt ekranında Sporcu rolü için Branş alanı yıldızla işaretlenir ve zorunludur. Diğer roller için isteğe bağlıdır. Yalnızca aktif branşlar seçilebilir.
- Sunucu, sporcu kaydında boş/Guid.Empty, bulunamayan veya pasif branşı hesap oluşturmadan önce reddeder. Seçilen branş yeni sporcu profilinin PrimaryBranchId alanına kaydedilir. Branşsız kayıt hata metni: “Sporcu kaydı oluşturmak için branş seçmelisiniz.”
- Antrenörün yalnızca izin verilen rollerde kayıt oluşturabilmesi korunur; istemciden gönderilen yetkisiz rol değiştirilmeden reddedilir. Mevcut sporcu kayıtları bu değişiklikle değiştirilmez.

### Geçmiş oturumların düzeltilmesi ve silinmesi (önceki sabit geçmiş kuralının yerine geçer)

- Yetkili antrenör tüm oturumları görüntüleme modunda açar; Düzenle ile tarih, özel/paylaşılan notlar ve kelime işlemlerini değiştirir. Değişiklikleri Kaydet tek işlemde uygular; Vazgeç hiçbir veri yazmaz. Düzenleme sırasında diğer oturumlara geçiş engellenir.
- Silme için ekranda “Bu oturumu silmek istediğinize emin misiniz?” onayı gösterilir. Aktif sorumlu antrenör yalnızca kendi oluşturduğu oturumu silebilir; yönetici yetkisi korunur. Silinen oturumlar normal sorgulardan, not ve kelime geçmişi görünümünden çıkarılır; kalıcı ID ve silinmiş kayıt denetim için saklanır.
- Geçmiş düzeltildiğinde/silindiğinde o oturumun başlangıcından ileriye, değişmeyen oturum numarası sırasıyla yeniden hesaplanır. Tarih değişikliği görüşmelerin oluşturulma sırasını değiştirmez. Sonraki oturumların olay kimlikleri, kendi ekleme/çıkarma niyetleri, kaynakları, notları ve tarihleri korunur. Etkisiz hale gelen sonraki olaylar da saklanır.
- Türetilmiş başlangıç/bitiş snapshotları ve güncel atamalar aynı serializable transaction içinde güncellenir. Başarısızlıkta tüm işlem geri alınır. Güncel atama eşitlemesi yeni kelime geçmişi olayı üretmez.
- Aynı kayıt isteğinin tekrarlanması, arada veri değişmişse revision kontrolüyle reddedilir; yinelenen yeni işlemler tekilleştirilir. Silinmiş oturum numaraları yeniden kullanılmaz; ilişkiler Guid oturum ID'siyle devam eder.
- Eksik geçmişli eski kayıtların bilinen SessionWords seçimleri ekleme olaylarına dönüştürülerek korunur. Başlangıç referansı varsa önceki sonuçtan, yoksa sonraki bilinen başlangıçtan veya güncel atamalardan bilinen işlemler geri alınarak çıkarılır; bağımsız mevcut atamalar korunur. Yeniden hesaplanan kayıtların başlangıç referansı bundan sonraki düzeltmeler için saklanır. Henüz dönüştürülmemiş eski kayıtlarda eksik geçmiş uyarısı gösterilir. Yalnızca not/tarih değişikliği kelime geçmişini dönüştürmez. Şema değişikliği gerekmez; mevcut soft-delete, olay ve snapshot tabloları kullanılır.
## Uygulama başlangıcı ve veritabanı erişimi

- Kestrel, veritabanı migration ve seed işlemlerini beklemeden HTTP/HTTPS portlarını dinlemeye başlar.
- Migration ve seed işlemleri sunucu başladıktan sonra arka planda yürütülür. LocalDB geçici olarak erişilemiyorsa hata loglanır; web sunucusunun başlatılması engellenmez.
## Atanmış kelimeler etkileşim görünümü

- Yeni kelime seçenekleri fareyle üzerine gelindiğinde ve klavyeyle odaklandığında yeşil ekleme geri bildirimi verir; seçilen durum aynı yeşil dilde kalır.
- Güncel atanmış kelimeler fareyle üzerine gelindiğinde veya kaldırma düğmesi odaklandığında kırmızı kaldırma geri bildirimi verir. Hareket azaltma tercihi olan kullanıcılar için animasyon uygulanmaz.
