# Soy Ağacı Yönetim Sistemi

ASP.NET Core MVC (.NET 8) + Entity Framework Core + MySQL tabanlı bir soy ağacı / aile ilişkileri yönetim uygulaması.

Mimari kararlar ve geliştirme kuralları için bkz. [CLAUDE.md](./CLAUDE.md).

## Bu sürümde neler var (Faz 1-6)

- **Kişi yönetimi**: ekleme, düzenleme, silme (soft delete), listeleme, arama, sayfalama
- **Aile ilişkileri**: anne/baba (self-referencing FK), eş (`SpouseRelationship`), kardeş/torun/yeğen otomatik hesaplama
- **Döngü koruması**: bir kişinin kendi soyundan birini anne/baba olarak seçmesi engellenir
- **Fotoğraf yönetimi**: kişi başına çoklu fotoğraf, MIME/imza doğrulamalı güvenli yükleme, ana fotoğraf seçimi
- **TC Kimlik No**: opsiyonel, benzersiz, detay sayfasında maskelenerek gösterilir
- **AJAX kişi arama**: anne/baba/eş seçiminde canlı arama (`/api/person/search`)
- **Soy ağacı görselleştirmesi (D3.js)**: `/FamilyTree/{id}` — merkez kişiyi baz alan, zoom/pan destekli
  görsel ağaç. Kart tıklanınca o kişi merkez olur, ↗ ikonu kişi detayına götürür. Artımlı genişletme
  butonları: Dede/Nine, Torun, Yeğen, Amca/Dayı/Hala/Teyze, Kuzen
  (`/api/familytree/{id}`, `/grandparents`, `/grandchildren`, `/nephews`, `/aunts-uncles`, `/cousins`)
- **Genişletilmiş akrabalık hesaplama**: Kişi detay sayfasında büyükanne/büyükbaba (Dede/Nine, taraf
  etiketiyle), amca, dayı, hala, teyze ve kuzenler otomatik hesaplanıp listelenir. Amca/dayı/hala/teyze
  ayrımı için Person modelinde opsiyonel bir `Cinsiyet` alanı bulunur (bkz. `CLAUDE.md` Bölüm 5)
- **Kullanıcı sistemi ve roller (ASP.NET Core Identity)**: `Admin` / `Editor` / `Viewer` rolleri.
  Admin kullanıcı yönetir, kişi ekler/düzenler/siler; Editor kişi ekler/düzenler ama silemez;
  Viewer yalnızca görüntüler. Uygulamanın tamamı kimlik doğrulaması ister (kişisel veri içerdiği için).
- **Audit log**: kişi/eş/fotoğraf/kullanıcı üzerindeki işlemler ile giriş/çıkışlar `AuditLogs`
  tablosuna kaydedilir (kullanıcı, işlem, tarih, IP, varlık); TC Kimlik No veya şifre asla loglanmaz.
  Admin-only `/AuditLog` sayfasından incelenebilir.
- **Veri dışa aktarma**: Kişiler sayfasından mevcut arama filtresine göre CSV dışa aktarma.

Yedekleme (mysqldump tabanlı) henüz eklenmedi — bkz. `CLAUDE.md` Faz 6.

## Gereksinimler

- .NET 8 SDK
- MySQL 8.0+
- `dotnet-ef` global aracı: `dotnet tool install --global dotnet-ef`

## Kurulum

1. MySQL'de veritabanı ve kullanıcı oluşturun:

   ```sql
   CREATE DATABASE FamilyTreeDb CHARACTER SET utf8mb4 COLLATE utf8mb4_turkish_ci;
   CREATE USER 'familytree_user'@'localhost' IDENTIFIED BY 'DEĞİŞTİRİN';
   GRANT ALL PRIVILEGES ON FamilyTreeDb.* TO 'familytree_user'@'localhost';
   FLUSH PRIVILEGES;
   ```

2. Bağlantı dizesini ve ilk Admin hesabını **User Secrets** ile tanımlayın (gerçek şifreler
   `appsettings.json`'a yazılmamalıdır ve repoya asla commit edilmemelidir):

   ```bash
   cd FamilyTree
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
     "Server=localhost;Port=3306;Database=FamilyTreeDb;User=familytree_user;Password=DEĞİŞTİRİN;CharSet=utf8mb4;"
   dotnet user-secrets set "Seed:AdminEmail" "admin@ornek.com"
   dotnet user-secrets set "Seed:AdminPassword" "GÜÇLÜ_BİR_ŞİFRE_BELİRLEYİN"
   ```

   `Seed:AdminEmail` / `Seed:AdminPassword` yalnızca veritabanında **hiç kullanıcı yokken**
   uygulama ilk kez başlatıldığında bir Admin hesabı oluşturmak için okunur; bu değerler
   ayarlanmazsa uygulama loglarda bir uyarı basar ve seed işlemini atlar (kullanıcıyı elle
   oluşturmanız gerekir).

3. Migration'ı uygulayın:

   ```bash
   dotnet ef database update
   ```

4. Uygulamayı çalıştırın:

   ```bash
   dotnet run
   ```

   Varsayılan adres: `http://localhost:5171`. İlk açılışta `Seed:AdminEmail` ile giriş
   yapıp `/Users` sayfasından Editor/Viewer hesapları oluşturabilirsiniz.

## Roller

| Rol    | Kişi görüntüleme | Kişi ekleme/düzenleme | Kişi silme | Kullanıcı yönetimi | Audit log |
|--------|:---:|:---:|:---:|:---:|:---:|
| Admin  | ✓ | ✓ | ✓ | ✓ | ✓ |
| Editor | ✓ | ✓ | ✗ | ✗ | ✗ |
| Viewer | ✓ | ✗ | ✗ | ✗ | ✗ |

## Proje yapısı

```text
FamilyTree/
├── Controllers/     # Person, PersonApi, FamilyTree, FamilyTreeApi, Account, Users, AuditLog, Home
├── Models/           # Person, PersonPhoto, SpouseRelationship, ApplicationUser, AuditLog, Gender
├── Data/              # ApplicationDbContext (IdentityDbContext<ApplicationUser>)
├── Services/         # IPersonService / IPhotoService / IFamilyTreeService / IAuditLogService
├── ViewModels/
├── Views/
├── Migrations/
└── wwwroot/uploads/  # Yüklenen fotoğraflar (git'e dahil değil)
```
