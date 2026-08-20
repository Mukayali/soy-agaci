# Soy Ağacı Yönetim Sistemi

ASP.NET Core MVC (.NET 8) + Entity Framework Core + MySQL tabanlı bir soy ağacı / aile ilişkileri yönetim uygulaması.

Mimari kararlar ve geliştirme kuralları için bkz. [CLAUDE.md](./CLAUDE.md).

## Bu sürümde neler var (Faz 1-4)

- **Kişi yönetimi**: ekleme, düzenleme, silme (soft delete), listeleme, arama, sayfalama
- **Aile ilişkileri**: anne/baba (self-referencing FK), eş (`SpouseRelationship`), kardeş/torun/yeğen otomatik hesaplama
- **Döngü koruması**: bir kişinin kendi soyundan birini anne/baba olarak seçmesi engellenir
- **Fotoğraf yönetimi**: kişi başına çoklu fotoğraf, MIME/imza doğrulamalı güvenli yükleme, ana fotoğraf seçimi
- **TC Kimlik No**: opsiyonel, benzersiz, detay sayfasında maskelenerek gösterilir
- **AJAX kişi arama**: anne/baba/eş seçiminde canlı arama (`/api/person/search`)
- **Soy ağacı görselleştirmesi (D3.js)**: `/FamilyTree/{id}` — merkez kişiyi baz alan, zoom/pan destekli
  görsel ağaç. Kart tıklanınca o kişi merkez olur, ↗ ikonu kişi detayına götürür. "+ Dede ve Nineleri
  Göster / Torunları Göster / Yeğenleri Göster" butonlarıyla artımlı genişletme yapılır
  (`/api/familytree/{id}`, `/grandparents`, `/grandchildren`, `/nephews`)

Kullanıcı/rol yönetimi henüz eklenmedi — bkz. `CLAUDE.md` Faz 6.

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

2. Bağlantı dizesini **User Secrets** ile tanımlayın (gerçek şifreler `appsettings.json`'a yazılmamalıdır):

   ```bash
   cd FamilyTree
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
     "Server=localhost;Port=3306;Database=FamilyTreeDb;User=familytree_user;Password=DEĞİŞTİRİN;CharSet=utf8mb4;"
   ```

3. Migration'ı uygulayın:

   ```bash
   dotnet ef database update
   ```

4. Uygulamayı çalıştırın:

   ```bash
   dotnet run
   ```

   Varsayılan adres: `http://localhost:5171`

## Proje yapısı

```text
FamilyTree/
├── Controllers/     # Person, PersonApi, Home
├── Models/           # Person, PersonPhoto, SpouseRelationship
├── Data/              # ApplicationDbContext
├── Services/         # IPersonService / IPhotoService (iş mantığı)
├── ViewModels/
├── Views/
├── Migrations/
└── wwwroot/uploads/  # Yüklenen fotoğraflar (git'e dahil değil)
```
