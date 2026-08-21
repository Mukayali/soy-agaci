# Soy Ağacı Yönetim Sistemi

ASP.NET Core MVC (.NET 8) + Entity Framework Core + MySQL tabanlı bir soy ağacı / aile ilişkileri yönetim uygulaması.

Mimari kararlar ve geliştirme kuralları için bkz. [CLAUDE.md](./CLAUDE.md).

## Bu sürümde neler var (Faz 1-6)

- **Kişi yönetimi**: ekleme, düzenleme, silme (soft delete), listeleme, arama, sayfalama
- **Aile ilişkileri**: anne/baba (self-referencing FK), eş (`SpouseRelationship`), kardeş/torun/yeğen otomatik hesaplama
- **Döngü koruması**: bir kişinin kendi soyundan birini anne/baba olarak seçmesi engellenir
- **Fotoğraf yönetimi**: kişi başına çoklu fotoğraf, MIME/imza doğrulamalı güvenli yükleme, ana fotoğraf seçimi
- **Fotoğraf galerisi** (`/Photos`, ana sayfadaki "Toplam Fotoğraf" kartından erişilir): tüm
  fotoğrafları kişiye göre gruplayarak listeler; kişi seçmeden yüklenen fotoğraflar ayrı bir
  "İlişkilendirilmemiş Fotoğraflar" bölümünde gösterilir ve sonradan bir kişiye atanabilir
  (Admin/Editor). Her fotoğraf buradan da silinebilir.
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
- **CSV toplu kişi içe aktarma**: Kişiler sayfasındaki "CSV İçe Aktar" paneli (Admin/Editor)
  `TCKimlikNo, Ad, Soyad, Cinsiyet, DogumTarihi, OlumTarihi, AnneTC, BabaTC` sütunlarından
  toplu kişi ekler; `AnneTC`/`BabaTC` dosyadaki diğer satırlara veya veritabanında zaten
  kayıtlı kişilere TC üzerinden otomatik bağlanır. "Örnek Şablon İndir" ile hazır bir
  başlangıç dosyası indirilebilir. Hatalı/eksik satırlar tüm içe aktarmayı durdurmaz —
  atlanır veya ilgili alan boş bırakılır, ayrıntılı bir uyarı listesiyle bildirilir
  (bkz. `CLAUDE.md` Bölüm 51.2).
- **Yedekleme (mysqldump tabanlı)**: Admin-only `/Backup` sayfasından anlık `.sql` yedeği indirilebilir;
  düzenli/otomatik yedekleme için `scripts/backup.sh` betiği cron ile zamanlanabilir (bkz. aşağıda).
- **GEDCOM içe/dışa aktarma**: `/Gedcom` sayfasından standart GEDCOM 5.5.1 formatında dışa aktarma
  (tüm kullanıcılar) ve içe aktarma (Admin/Editor). Diğer soy ağacı programlarıyla (Ancestry,
  MyHeritage, Gramps vb.) veri alışverişi sağlar; TC Kimlik No dışa aktarılmaz, belirsiz tarihler
  (`ABT 1950` gibi) yanlış kesinlik oluşturmamak için açıklama alanına not olarak düşülür.
- **PDF / PNG / SVG soy ağacı dışa aktarma**: `/FamilyTree/{id}` sayfasındaki "PDF İndir",
  "PNG İndir" ve "SVG İndir" butonları, ekranda yüklü tüm soy ağacını (tüm genişletmeler
  dahil) ilgili formatta indirir. Tamamen istemci tarafında çalışır (sunucuda headless
  tarayıcı gerektirmez); Türkçe karakterlerin (ş, ğ, ı, İ) doğru görünmesi için PDF/PNG
  tarayıcının kendi font motoruyla rasterleştirilir, SVG ise doğrudan vektör olarak (en
  sadık sonuç, Illustrator/Inkscape'te düzenlenebilir) indirilir — bkz. `CLAUDE.md`
  Bölüm 51.1'de bu tasarımın gerekçesi (jsPDF'in özel font gömme hatası).
- **Sülale (aile grubu) ve doğum yeri**: `/Sulale` sayfasından sülaleler (Admin/Editor ekler ve
  düzenler, Admin siler) yönetilir. Kişi ekleme/düzenleme formunda Sülale bir pulldown menü olarak
  seçilir ve isteğe bağlı bir "Doğum Yeri" alanı bulunur. `/Person?sulaleId=X` ile bir sülalenin tüm
  üyeleri listelenebilir. `/FamilyTree/Sulale/{id}` tek bir kişiyi merkez almadan, sülalenin **tüm**
  üyelerini tek bir ağaçta gösterir — nesil derinliği, sülale içi kan bağlarından oluşan DAG üzerinde
  topolojik sıralama (Kahn algoritması) ile hesaplanır ve evli çiftler (yalnızca aralarında kayıtlı bir
  `SpouseRelationship` varsa) sabit nokta yinelemesiyle aynı görsel satıra hizalanır — bkz. `CLAUDE.md`
  Bölüm 51.3.

Faz 1-6'nın tamamı tamamlandı — kalan işler artık CLAUDE.md Bölüm 50'deki gelecek özellikleri
(aile olayları, gelişmiş raporlama, harita entegrasyonu vb.) kapsıyor.

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

| Rol    | Kişi görüntüleme | Kişi ekleme/düzenleme | Kişi silme | Kullanıcı yönetimi | Audit log | Yedekleme |
|--------|:---:|:---:|:---:|:---:|:---:|:---:|
| Admin  | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Editor | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ |
| Viewer | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ |

## Yedekleme

İki yol vardır:

1. **Admin panelinden anlık indirme**: `/Backup` sayfasında "Yedek Al ve İndir" butonu, sunucuda
   `mysqldump` çalıştırıp sonucu doğrudan `.sql` dosyası olarak tarayıcıya indirir. Sunucuda
   `mysqldump` komutunun PATH'te bulunması gerekir.

2. **Zamanlanmış (cron) yedekleme**: `scripts/backup.sh` betiği `mysqldump` ile yedek alır,
   `gzip` ile sıkıştırır ve `RETENTION_DAYS` (varsayılan 14 gün) süresinden eski yedekleri
   otomatik siler.

   ```bash
   DB_PASSWORD='...' ./scripts/backup.sh
   ```

   Cron ile örnek zamanlama (`crontab -e`):

   ```cron
   0 3 * * * DB_PASSWORD='...' /opt/soyagaci/scripts/backup.sh >> /var/log/soyagaci-backup.log 2>&1
   ```

   Yedekler varsayılan olarak proje kökündeki `backups/` klasörüne yazılır (bu klasör git'e
   dahil değildir, gerçek kişisel veri içerdiğinden asla commit edilmemelidir).

## Proje yapısı

```text
FamilyTree/
├── Controllers/     # Person, PersonApi, FamilyTree, FamilyTreeApi, Account, Users, AuditLog, Backup, Gedcom, Photos, Home
├── Models/           # Person, PersonPhoto, SpouseRelationship, ApplicationUser, AuditLog, Gender
├── Data/              # ApplicationDbContext (IdentityDbContext<ApplicationUser>)
├── Services/         # IPersonService / IPhotoService / IFamilyTreeService / IAuditLogService / IBackupService / IGedcomService
├── ViewModels/
├── Views/
├── Migrations/
└── wwwroot/uploads/  # Yüklenen fotoğraflar (git'e dahil değil)

scripts/
└── backup.sh          # mysqldump tabanlı, cron ile zamanlanabilir yedekleme betiği
```
