# Varsayımlar

- Feedback varsayılan görünürlüğü: feedback yazarı, feedback verilen sporcu ve Admin görebilir. Başka Coach veya Parent aynı sporcuya bağlı olsa bile diğer yazarların feedbacklerini varsayılan olarak göremez.
- MVP ekranları servis katmanını kullanacak şekilde sade tutuldu; ileri düzey inline edit/tree drag-drop deneyimi sonraki iterasyona bırakıldı.
- Development admin seed için `SeedAdmin:Email` ve `SeedAdmin:Password` yoksa seed güvenli şekilde atlanır.
- Profil fotoğrafları SQL Server içinde binary olarak saklanmaz; uygulama altındaki `uploads/profiles` klasörüne yazılır.
- Üretim ortamı için connection string environment variable veya secret store üzerinden sağlanmalıdır.
