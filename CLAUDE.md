# CLAUDE.md

## 1. Proje Tanımı

Bu proje, kişilerin ve aralarındaki aile/akrabalık ilişkilerinin kayıt altına alınabildiği, web tabanlı bir **Soy Ağacı Yönetim Sistemi** geliştirmeyi amaçlamaktadır.

Uygulama C# ve ASP.NET Core kullanılarak geliştirilecektir.

Temel amaçlar:

* Kişi kaydı oluşturmak
* Kişilerin temel ve ayrıntılı bilgilerini saklamak
* Anne-baba ilişkilerini tanımlamak
* Eş ilişkilerini tanımlamak
* Çocuk, kardeş, torun, yeğen vb. akrabalıkları otomatik olarak hesaplamak
* Bir kişinin birden fazla fotoğrafını saklamak
* Kişiler hakkında ayrıntılı açıklamalar/tarihçe eklemek
* Kişiler arasında görsel bir soy ağacı oluşturmak
* Soy ağacında kişi kartlarına tıklayarak kişi detaylarına ulaşmak
* Büyük ailelerde soy ağacının kolay gezilebilmesini sağlamak
* Gelecekte kullanıcı, yetkilendirme, veri dışa aktarma ve gelişmiş raporlama özelliklerinin eklenebilmesine uygun mimari oluşturmak

---

# 2. Teknoloji Yığını

Aşağıdaki teknoloji ve mimari tercih edilmelidir:

* C#
* ASP.NET Core MVC
* .NET 8 veya güncel LTS sürüm
* Entity Framework Core
* **MySQL** (8.0 veya üzeri)
* **Pomelo.EntityFrameworkCore.MySql** (EF Core için resmi olmayan ama en yaygın ve stabil MySQL provider'ı)
* ASP.NET Core Identity
* Razor Views
* Bootstrap 5
* JavaScript
* AJAX / Fetch API
* HTML5
* CSS3

> Not: Microsoft, EF Core için resmi bir MySQL provider'ı sağlamaz. Bu nedenle proje `Pomelo.EntityFrameworkCore.MySql` NuGet paketini kullanmalıdır. Alternatif olarak Oracle'ın `MySql.EntityFrameworkCore` paketi de değerlendirilebilir, ancak Pomelo topluluk desteği ve EF Core sürüm uyumluluğu açısından tercih edilmelidir.

Soy ağacı görselleştirmesi için mümkün olduğunca modern ve bakımı kolay bir JavaScript kütüphanesi kullanılmalıdır.

Öncelikli seçenek:

* D3.js

Alternatif olarak:

* Cytoscape.js
* React Flow benzeri bir yapı
* Özel SVG tabanlı çözüm

kullanılabilir.

Ancak gereksiz bağımlılık oluşturulmamalıdır.

---

# 3. Genel Mimari

Proje MVC mimarisine uygun olarak katmanlı ve sürdürülebilir şekilde tasarlanmalıdır.

Önerilen yapı:

```text
FamilyTree/
│
├── Controllers/
│
├── Models/
│
├── Data/
│
├── Services/
│
├── Repositories/
│
├── ViewModels/
│
├── Views/
│   ├── Person/
│   ├── FamilyTree/
│   ├── Account/
│   └── Shared/
│
├── wwwroot/
│   ├── css/
│   ├── js/
│   ├── images/
│   └── uploads/
│
├── Migrations/
│
├── Areas/
│   └── Admin/
│
├── Program.cs
├── appsettings.json
└── CLAUDE.md
```

Kod mümkün olduğunca SOLID prensiplerine uygun yazılmalıdır.

Controller içerisinde iş mantığı biriktirilmemelidir.

İş mantıkları Service katmanında tutulmalıdır.

---

# 4. Ana Modüller

Uygulama aşağıdaki ana modüllerden oluşmalıdır.

## 4.1. Kişi Yönetimi

Kişi ekleme, düzenleme, silme ve görüntüleme işlemleri.

## 4.2. Fotoğraf Yönetimi

Bir kişiye birden fazla fotoğraf eklenebilmelidir.

## 4.3. Aile İlişkileri

Anne, baba, eş, çocuk ve diğer ilişkiler.

## 4.4. Soy Ağacı

Kişiler arasındaki ilişkilerin görsel olarak gösterilmesi.

## 4.5. Kişi Detay Sayfası

Kişinin tüm bilgilerinin, fotoğraflarının ve akrabalarının gösterilmesi.

## 4.6. Arama

Ad, soyad ve mümkünse TC Kimlik Numarası üzerinden arama.

## 4.7. Kullanıcı ve Yetkilendirme

İleride birden fazla kullanıcının sistemi kullanabilmesine uygun yapı.

---

# 5. Kişi Veri Modeli

Temel kişi modeli aşağıdaki alanları içermelidir.

```csharp
public class Person
{
    public int Id { get; set; }

    public string? TcKimlikNo { get; set; }

    public string Ad { get; set; }

    public string Soyad { get; set; }

    public DateTime? DogumTarihi { get; set; }

    public DateTime? OlumTarihi { get; set; }

    public string? Aciklama { get; set; }

    public int? AnneId { get; set; }

    public int? BabaId { get; set; }

    public Person? Anne { get; set; }

    public Person? Baba { get; set; }

    public ICollection<Person> AnneCocuklari { get; set; }

    public ICollection<Person> BabaCocuklari { get; set; }

    public ICollection<PersonPhoto> Photos { get; set; }

    public ICollection<SpouseRelationship> Spouses { get; set; }
}
```

Ancak gerçek uygulamada ilişkiler Entity Framework Core açısından dikkatli tasarlanmalıdır.

**MySQL özel notu:** MySQL'de self-referencing foreign key'ler için `ON DELETE` davranışı EF Core tarafından varsayılan olarak `CASCADE` önerebilir. Person tablosunda `AnneId` ve `BabaId` gibi self-referencing FK'lerde MySQL çoklu cascade path hatası (`Cannot add or update a child row: a foreign key constraint fails`) verebileceğinden, bu ilişkiler `DeleteBehavior.Restrict` veya `DeleteBehavior.NoAction` olarak yapılandırılmalıdır:

```csharp
modelBuilder.Entity<Person>()
    .HasOne(p => p.Anne)
    .WithMany(p => p.AnneCocuklari)
    .HasForeignKey(p => p.AnneId)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<Person>()
    .HasOne(p => p.Baba)
    .WithMany(p => p.BabaCocuklari)
    .HasForeignKey(p => p.BabaId)
    .OnDelete(DeleteBehavior.Restrict);
```

**Cinsiyet alanı (Faz 5 için eklendi):** Amca/Dayı/Hala/Teyze gibi cinsiyete bağlı akrabalık
etiketlerinin doğru hesaplanabilmesi için Person modeline opsiyonel bir `Cinsiyet` alanı
(`Erkek` / `Kadin` enum, nullable) eklenmiştir. Anne/Baba alanları zaten doğası gereği
cinsiyet taşıdığından (Anne=kadın, Baba=erkek), Dede/Nine etiketleri bu alan olmadan da
doğru hesaplanabilir; Cinsiyet alanı yalnızca bir kişinin **kendi** kardeşlerinin
amca/hala mı yoksa dayı/teyze mi olduğunu ayırt etmek için gereklidir.

---

# 6. TC Kimlik Numarası

TC Kimlik Numarası hassas kişisel veri olduğundan:

* Veritabanında mümkünse şifrelenmiş veya güvenli biçimde saklanmalıdır.
* Log dosyalarına yazılmamalıdır.
* Hata mesajlarında gösterilmemelidir.
* Gereksiz API response'larında gönderilmemelidir.
* Arama sonuçlarında tam TC Kimlik No gösterilmemelidir.
* Yetkisiz kullanıcılar tarafından görüntülenmemelidir.

TC Kimlik No için benzersizlik kontrolü uygulanmalıdır.

MySQL'de unique index tanımı:

```sql
ALTER TABLE Persons
ADD UNIQUE INDEX UX_Persons_TcKimlikNo (TcKimlikNo);
```

veya Fluent API ile:

```csharp
modelBuilder.Entity<Person>()
    .HasIndex(p => p.TcKimlikNo)
    .IsUnique();
```

> Not: MySQL'de unique index NULL değerlere izin verir ve birden fazla NULL kaydına müsaade eder (SQL Server'daki `WHERE` filtreli unique index davranışına benzer şekilde), bu nedenle TC Kimlik No opsiyonel tutulsa dahi unique kısıtlama sorunsuz çalışır.

TC Kimlik Numarası opsiyonel olarak tasarlanabilir.

---

# 7. Fotoğraf Modeli

Bir kişinin birden fazla fotoğrafı olabilir.

Bu nedenle fotoğraflar Person tablosunda tutulmamalıdır.

Ayrı tablo kullanılmalıdır.

Örnek:

```csharp
public class PersonPhoto
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public string FileName { get; set; }

    public string FilePath { get; set; }

    public string? Description { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }

    public Person Person { get; set; }
}
```

Bir kişinin:

* 1
* 10
* 100

fotoğrafı olabilir.

Sistem buna engel olmamalıdır.

---

# 8. Fotoğraf Yükleme

Fotoğraf yükleme sistemi:

* JPG
* JPEG
* PNG
* WEBP

formatlarını desteklemelidir.

Dosya boyutu kontrol edilmelidir.

Dosya uzantısına güvenilmemeli, MIME type ve dosya içeriği kontrol edilmelidir.

Dosyalar mümkünse benzersiz GUID isimleriyle kaydedilmelidir.

Örneğin:

```text
/uploads/persons/
    8f9e2a1c-....jpg
    3c1f7d91-....jpg
```

Kullanıcının yüklediği dosya adı doğrudan kullanılmamalıdır.

Fotoğraf dosya yolu/adı veritabanında `VARCHAR(255)` veya `VARCHAR(512)` olarak saklanmalıdır; GUID'in kendisi ayrı bir kolon olarak tutulmayacaksa dosya adının içine gömülmesi yeterlidir.

---

# 9. Anne-Baba İlişkisi

Her kişi için:

```text
Anne
Baba
```

alanları bulunmalıdır.

Örneğin:

```text
Ahmet
 ├── Anne: Ayşe
 └── Baba: Mehmet
```

Anne ve baba Person tablosundaki başka bir kişi olacaktır.

Foreign Key kullanılmalıdır.

```text
Person.AnneId -> Person.Id
Person.BabaId -> Person.Id
```

Self-referencing relationship kullanılacaktır. MySQL'de bu tür self-referencing FK'lerde cascade delete zincirleri döngüsel hataya yol açabileceğinden bkz. Bölüm 5 — `DeleteBehavior.Restrict` kullanılmalıdır.

---

# 10. Kardeş Hesaplama

Kardeş ilişkisi veritabanında ayrıca saklanmamalıdır.

Aynı anne veya babaya sahip kişiler kardeş olarak hesaplanmalıdır.

Örneğin:

```text
Ali
Anne = Ayşe
Baba = Mehmet

Veli
Anne = Ayşe
Baba = Mehmet
```

Bu durumda:

```text
Ali <-> Veli
```

kardeştir.

Sistemin kardeş ilişkisini otomatik hesaplaması tercih edilmelidir.

---

# 11. Çocuk Hesaplama

Bir kişinin çocukları:

```text
AnneId == Person.Id
```

veya

```text
BabaId == Person.Id
```

olan kişilerden bulunmalıdır.

Örneğin:

```text
Mehmet
   |
   +---- Ali
   +---- Veli
   +---- Ayşe
```

---

# 12. Torun Hesaplama

Torun ilişkisi doğrudan veritabanında tutulmamalıdır.

Çocukların çocukları bulunarak hesaplanmalıdır.

Örneğin:

```text
Dede
 |
 +-- Baba
      |
      +-- Çocuk
```

Burada Dede'nin torunu Çocuk'tur.

---

# 13. Yeğen Hesaplama

Yeğen ilişkisi:

```text
Kişinin kardeşlerinin çocukları
```

şeklinde hesaplanmalıdır.

Örneğin:

```text
        Mehmet
        /    \
      Ali    Veli
             |
            Ayşe
```

Ali açısından Ayşe:

```text
Yeğen
```

olarak gösterilmelidir.

---

# 14. Eş İlişkisi

Eş ilişkisi anne-baba alanlarından ayrı tutulmalıdır.

Bunun için ayrı bir tablo kullanılmalıdır.

Örnek:

```csharp
public class SpouseRelationship
{
    public int Id { get; set; }

    public int Person1Id { get; set; }

    public int Person2Id { get; set; }

    public DateTime? MarriageDate { get; set; }

    public DateTime? DivorceDate { get; set; }

    public Person Person1 { get; set; }

    public Person Person2 { get; set; }
}
```

Böylece:

* evlilik tarihi
* boşanma tarihi
* birden fazla evlilik

desteklenebilir.

`Person1Id` ve `Person2Id` için de MySQL'de aynı Person tablosuna çift FK tanımlanırken `DeleteBehavior.Restrict` kullanılmalı, aksi halde çoklu cascade path hatası oluşabilir.

---

# 15. Gelecekte Eklenebilecek İlişkiler

Mimari ileride aşağıdaki ilişkileri destekleyebilecek şekilde tasarlanmalıdır:

* Anne
* Baba
* Eş
* Çocuk
* Kardeş
* Dede
* Nine
* Torun
* Amca
* Dayı
* Hala
* Teyze
* Yeğen
* Kuzen

Ancak mümkün olduğunca bu ilişkiler veritabanında tekrar tekrar tutulmamalıdır.

Anne/baba ve eş gibi temel ilişkilerden türetilen akrabalıklar Service katmanında hesaplanmalıdır.

---

# 16. Soy Ağacı Sayfası

Ana görsel sayfa:

```text
                 Dede ───── Nine
                   │
          ┌────────┴────────┐
          │                 │
        Baba ──────────── Anne
          │
      ┌───┴────┐
      │        │
     Ali      Veli
      │
     ┌┴┐
    Ayşe Mehmet
```

şeklinde görsel bir yapı oluşturmalıdır.

Ancak gerçek uygulamada:

* Düğümler
* Bağlantılar
* Yakınlık ilişkileri
* Eşler
* Çocuklar

dinamik olarak oluşturulmalıdır.

---

# 17. Soy Ağacı Kullanıcı Deneyimi

Soy ağacı ekranında:

* Yakınlaştırma
* Uzaklaştırma
* Sürükleme
* Ortala
* Tam ekran
* Kişi seçme
* Kişi detayına gitme
* Üst nesilleri gösterme
* Alt nesilleri gösterme

özellikleri bulunmalıdır.

Özellikle büyük ailelerde tüm ağacın tek seferde yüklenmesi yerine seçilen kişi merkez alınmalıdır.

Örneğin:

```text
Seçilen kişi
   ↓
Anne
Baba
Eş
Kardeşler
Çocuklar
```

ve kullanıcı isterse:

```text
+ Dede ve Nineleri Göster
+ Torunları Göster
+ Yeğenleri Göster
```

şeklinde genişletebilmelidir.

---

# 18. Kişi Kartı

Soy ağacındaki her kişi bir kart şeklinde gösterilmelidir.

Örnek:

```text
┌─────────────────────┐
│       FOTOĞRAF      │
│                     │
│     Ahmet Yılmaz    │
│     1945 - 2020     │
│                     │
│     👨 Baba         │
└─────────────────────┘
```

Karta tıklanınca:

```text
/Person/Details/123
```

adresindeki kişi detay sayfasına gidilmelidir.

---

# 19. Hayatta Olan / Vefat Eden Kişiler

Kişi kartlarında doğum ve ölüm tarihleri gösterilmelidir.

Örneğin:

```text
Ahmet Yılmaz
1945 - 2020
```

Hayatta olan kişi:

```text
Ali Yılmaz
1975 -
```

şeklinde gösterilebilir.

Ölüm tarihi girilmiş kişiler görsel olarak farklılaştırılabilir.

Ancak renk seçimi erişilebilirlik kurallarına uygun olmalıdır.

---

# 20. Kişi Detay Sayfası

Kişi detay sayfası aşağıdaki bölümlerden oluşmalıdır.

## Temel Bilgiler

```text
Ad
Soyad
TC Kimlik No
Doğum Tarihi
Ölüm Tarihi
```

## Fotoğraflar

Fotoğraf galerisi.

## Açıklama

Kişinin biyografisi, yaşam öyküsü veya aile içerisindeki bilgiler.

## Anne-Baba

```text
Anne
Baba
```

## Eş

Eş/eşler.

## Çocuklar

Çocuk listesi.

## Kardeşler

Kardeş listesi.

## Soy Ağacı

Kişiyi merkez alan küçük soy ağacı.

---

# 21. Kişi Ekleme Formu

Form:

```text
Ad
Soyad
TC Kimlik No
Doğum Tarihi
Ölüm Tarihi

Anne
[Birey Ara...]

Baba
[Birey Ara...]

Fotoğraflar
[Dosya Seç]

Açıklama
[.................................]

[Kaydet]
```

Anne ve baba alanlarında bütün kişileri dropdown olarak yüklemek yerine AJAX tabanlı arama kullanılmalıdır.

Örneğin:

```text
Anne: [ Ayşe Yıl... ]

Sonuçlar:

Ayşe Yılmaz
Ayşe Kaya
Ayşe Demir
```

---

# 22. Kişi Arama

Kişi arama sistemi aşağıdaki alanlarda çalışmalıdır:

```text
Ad
Soyad
Ad + Soyad
TC Kimlik No
```

Örneğin:

```text
"Mehmet Yılmaz"
```

arama sonucunda:

```text
Mehmet Yılmaz
1942 - 2019

Mehmet Yılmaz
1971 -
```

gibi sonuçlar gösterilebilir.

MySQL'de büyük/küçük harf duyarlılığı varsayılan collation'a (`utf8mb4_general_ci` veya `utf8mb4_unicode_ci`) bağlıdır ve genellikle case-insensitive'dir; Türkçe karakterler (İ, ı, ğ, ş, ç, ö, ü) için arama yaparken collation'ın Türkçe karakterleri doğru sıraladığından ve eşleştirdiğinden emin olunmalıdır. Gerekirse `utf8mb4_turkish_ci` collation'ı değerlendirilmelidir.

---

# 23. Veri Doğrulama

Server-side validation mutlaka uygulanmalıdır.

Örnek:

* Ad boş bırakılamaz.
* Soyad boş bırakılamaz.
* Doğum tarihi ölüm tarihinden sonra olamaz.
* Ölüm tarihi gelecekte olamaz.
* Anne ve baba aynı kişi olamaz.
* Kişi kendisinin annesi/babası olamaz.
* TC Kimlik No formatı doğrulanmalıdır.
* Aynı TC Kimlik No ikinci kez eklenmemelidir.

---

# 24. Döngü Kontrolü

Aile ilişkilerinde sonsuz döngü oluşturulmasına izin verilmemelidir.

Örneğin:

```text
Ali -> Baba = Veli
Veli -> Baba = Ali
```

gibi hatalı ilişki engellenmelidir.

Aynı şekilde:

```text
Ali -> Anne = Ayşe
Ayşe -> Anne = Ali
```

gibi ilişkiler engellenmelidir.

Relationship validation Service katmanında yapılmalıdır.

---

# 25. Veritabanı

**MySQL** kullanılmalıdır (8.0 veya üzeri önerilir; JSON kolon tipi, CTE — `WITH RECURSIVE` — ve window function desteği için).

Temel tablolar:

```text
Persons
PersonPhotos
SpouseRelationships
Users
```

İleride:

```text
Notes
Documents
Places
Events
PersonEvents
AuditLogs
```

tabloları eklenebilir.

**Karakter seti ve collation:** Türkçe karakter desteği ve emoji uyumluluğu için veritabanı ve tüm tablolar `utf8mb4` karakter setiyle, `utf8mb4_turkish_ci` veya `utf8mb4_unicode_ci` collation ile oluşturulmalıdır.

```sql
CREATE DATABASE FamilyTreeDb
CHARACTER SET utf8mb4
COLLATE utf8mb4_turkish_ci;
```

**Depolama motoru:** Tüm tablolar `InnoDB` storage engine kullanmalıdır (foreign key ve transaction desteği için varsayılan ve zorunludur).

---

# 26. Önerilen Person Tablosu

```text
Persons
--------------------------------
Id
TcKimlikNo
Ad
Soyad
DogumTarihi
OlumTarihi
Aciklama
AnneId
BabaId
CreatedAt
UpdatedAt
IsDeleted
```

Soft delete kullanılmalıdır.

Gerçek kişi kayıtları doğrudan fiziksel olarak silinmemelidir.

MySQL DDL örneği:

```sql
CREATE TABLE Persons (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    TcKimlikNo VARCHAR(11) NULL,
    Ad VARCHAR(100) NOT NULL,
    Soyad VARCHAR(100) NOT NULL,
    DogumTarihi DATE NULL,
    OlumTarihi DATE NULL,
    Aciklama TEXT NULL,
    AnneId INT NULL,
    BabaId INT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT FK_Persons_Anne FOREIGN KEY (AnneId) REFERENCES Persons(Id) ON DELETE RESTRICT,
    CONSTRAINT FK_Persons_Baba FOREIGN KEY (BabaId) REFERENCES Persons(Id) ON DELETE RESTRICT,
    UNIQUE INDEX UX_Persons_TcKimlikNo (TcKimlikNo),
    INDEX IX_Persons_AnneId (AnneId),
    INDEX IX_Persons_BabaId (BabaId),
    INDEX IX_Persons_AdSoyad (Ad, Soyad)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;
```

> Not: `AUTO_INCREMENT` MySQL'de SQL Server'daki `IDENTITY(1,1)` karşılığıdır ve EF Core Pomelo provider'ı tarafından otomatik yapılandırılır (`int Id` alanı için ek konfigürasyon gerekmez).

---

# 27. Audit Log

İleride kullanıcıların yaptığı işlemlerin izlenebilmesi için:

```text
AuditLogs
```

tablosu oluşturulmalıdır.

Örneğin:

```text
Kullanıcı
İşlem
Tarih
IP
Entity
EntityId
```

bilgileri tutulabilir.

Ancak TC Kimlik No gibi hassas veriler loglara yazılmamalıdır.

---

# 28. Güvenlik

Uygulama kişisel veri içerdiği için güvenlik öncelikli olmalıdır.

Mutlaka:

* Authentication
* Authorization
* HTTPS
* CSRF protection
* XSS protection
* SQL Injection protection
* Input validation
* File upload validation
* Rate limiting
* Secure password hashing

uygulanmalıdır.

Entity Framework Core parametreli sorguları kullanılmalıdır.

Raw SQL mümkün olduğunca kullanılmamalıdır. Zorunlu durumlarda (örn. `WITH RECURSIVE` gerektiren atalar/alt soy sorguları) mutlaka `FromSqlInterpolated` veya parametreli `FromSqlRaw` kullanılmalı, string concatenation ile SQL oluşturulmamalıdır.

**Bağlantı dizesi güvenliği:** MySQL bağlantı bilgileri (kullanıcı adı, şifre, sunucu) `appsettings.json` içinde düz metin olarak tutulmamalı; production ortamında `appsettings.Development.json`, User Secrets, environment variables veya bir secret manager (Azure Key Vault, AWS Secrets Manager vb.) kullanılmalıdır.

---

# 29. ASP.NET Identity

Kullanıcı yönetimi için ASP.NET Core Identity kullanılmalıdır.

ASP.NET Core Identity, MySQL ile Pomelo EF Core provider'ı üzerinden sorunsuz çalışır; `IdentityDbContext` MySQL tablolarına migrate edilirken varsayılan `nvarchar(450)` tipi kolonlar MySQL'de `varchar(191)` gibi index uzunluk sınırlarına takılabilir (InnoDB + `utf8mb4` ile index anahtar uzunluğu limiti 767 byte / 191 karakter). Pomelo provider bunu otomatik ayarlar; manuel migration düzenlemesi gerekirse `MySqlServerVersion` sürüm bilgisinin `Program.cs`'de doğru tanımlandığından emin olunmalıdır.

Roller:

```text
Admin
Editor
Viewer
```

olarak tasarlanabilir.

Admin:

* Kullanıcı yönetir
* Kişi ekler
* Kişi siler
* İlişkileri düzenler

Editor:

* Kişi ekler
* Düzenler
* Fotoğraf ekler

Viewer:

* Sadece görüntüleme yapar.

---

# 30. API / AJAX

Kişi arama ve soy ağacı verilerinin alınması için API endpointleri oluşturulabilir.

Örnek:

```text
GET /api/person/search?q=mehmet
GET /api/person/123
GET /api/familytree/123
GET /api/person/123/children
GET /api/person/123/siblings
GET /api/person/123/ancestors
GET /api/person/123/descendants
```

Soy ağacı API'sinin mümkün olduğunca sade bir JSON yapısı döndürmesi gerekir.

Örnek:

```json
{
  "nodes": [
    {
      "id": 1,
      "name": "Ahmet Yılmaz",
      "birthDate": "1945-05-12",
      "deathDate": "2020-03-10",
      "photo": "/uploads/persons/abc.jpg"
    }
  ],
  "links": [
    {
      "source": 1,
      "target": 2,
      "relationship": "child"
    }
  ]
}
```

---

# 31. Soy Ağacı Algoritması

Soy ağacı oluşturulurken seçilen kişi merkez kabul edilmelidir.

Örneğin:

```text
SelectedPerson = 100
```

İlk aşamada:

```text
Anne
Baba
Eş
Kardeş
Çocuk
```

getirilmelidir.

Kullanıcı genişletme yaptığında:

```text
Anne'nin anne ve babası
Baba'nın anne ve babası
Çocukların çocukları
```

getirilebilir.

Böylece büyük ailelerde gereksiz veri yüklenmesi önlenir.

**Atalar/alt soy için CTE:** Çok nesilli atalar (ancestors) veya alt soy (descendants) sorguları için MySQL 8.0+'ın `WITH RECURSIVE` desteği kullanılabilir. Bu tür sorgular EF Core LINQ ile doğrudan ifade edilemeyeceğinden, Service katmanında parametreli raw SQL (`FromSqlInterpolated`) ile yazılmalı ve sonuç kümesi projection ile sınırlandırılmalıdır. Örnek:

```sql
WITH RECURSIVE Ancestors AS (
    SELECT Id, AnneId, BabaId, 1 AS Seviye
    FROM Persons
    WHERE Id = @personId

    UNION ALL

    SELECT p.Id, p.AnneId, p.BabaId, a.Seviye + 1
    FROM Persons p
    INNER JOIN Ancestors a ON p.Id = a.AnneId OR p.Id = a.BabaId
    WHERE a.Seviye < 10
)
SELECT * FROM Ancestors;
```

Sonsuz döngüye karşı `Seviye` (derinlik) sınırı mutlaka konulmalıdır.

---

# 32. Performans

Büyük ailelerde performans önemlidir.

Şunlara dikkat edilmelidir:

* Lazy loading dikkatli kullanılmalıdır.
* N+1 sorgularından kaçınılmalıdır.
* Gerekli alanlar projection ile alınmalıdır.
* AsNoTracking() uygun sorgularda kullanılmalıdır.
* Arama alanlarına index eklenmelidir.
* TC Kimlik No indexlenmelidir.
* AnneId ve BabaId indexlenmelidir.
* Sayfalama uygulanmalıdır.
* Fotoğraflar optimize edilmelidir.
* MySQL tarafında yavaş sorgular için `EXPLAIN` ile sorgu planı kontrol edilmelidir; gerekirse `slow_query_log` aktifleştirilerek üretim ortamında yavaş sorgular izlenmelidir.
* Bağlantı havuzu (connection pooling) için `Pooling=true` bağlantı dizesinde etkin olmalı ve `MinPoolSize`/`MaxPoolSize` değerleri uygulama yüküne göre ayarlanmalıdır.

---

# 33. Fotoğraf Optimizasyonu

Yüksek çözünürlüklü fotoğraflar doğrudan tarayıcıya gönderilmemelidir.

Thumbnail oluşturulması tercih edilmelidir.

Örneğin:

```text
original/
thumbnail/
medium/
```

klasörleri kullanılabilir.

Galeride thumbnail gösterilir.

Fotoğraf açıldığında büyük versiyon gösterilir.

---

# 34. Arayüz Tasarımı

Arayüz modern, sade ve responsive olmalıdır.

Bootstrap 5 kullanılabilir.

Ana menü:

```text
Ana Sayfa
Kişiler
Soy Ağacı
Arama
Yönetim
```

şeklinde olabilir.

Mobil cihazlarda da kullanılabilmelidir.

---

# 35. Ana Sayfa

Ana sayfada:

```text
Toplam Kişi
Toplam Aile
Toplam Fotoğraf
Son Eklenen Kişiler
```

gösterilebilir.

Örneğin:

```text
┌──────────────┐
│  1.245 Kişi  │
└──────────────┘

┌──────────────┐
│  3.482 Foto  │
└──────────────┘
```

---

# 36. Kişiler Sayfası

Tablo:

```text
Fotoğraf | Ad Soyad | Doğum | Ölüm | Anne | Baba | İşlemler
```

İşlemler:

```text
Detay
Düzenle
Soy Ağacını Gör
```

şeklinde olmalıdır.

---

# 37. Silme İşlemi

Kişi silmeden önce:

```text
Bu kişiye bağlı aile ilişkileri bulunmaktadır.
Silmek istediğinizden emin misiniz?
```

uyarısı gösterilmelidir.

Soft delete kullanılmalıdır.

Bir kişinin başka kişilerin:

```text
Anne
Baba
Eş
```

ilişkilerinde kullanılıp kullanılmadığı kontrol edilmelidir.

---

# 38. Kodlama Kuralları

C# naming convention:

```text
PascalCase
```

kullanılmalıdır.

Örneğin:

```csharp
GetPersonAsync()
GetFamilyTreeAsync()
CreatePersonAsync()
UpdatePersonAsync()
```

Asenkron metotlarda:

```text
Async
```

suffix kullanılmalıdır.

---

# 39. Controller Kuralları

Controller mümkün olduğunca ince tutulmalıdır.

YANLIŞ:

```csharp
public IActionResult Details(int id)
{
    // 200 satır iş mantığı
}
```

DOĞRU:

```csharp
public async Task<IActionResult> Details(int id)
{
    var person = await _personService.GetDetailsAsync(id);

    return View(person);
}
```

---

# 40. Service Katmanı

Örnek:

```csharp
public interface IPersonService
{
    Task<Person?> GetByIdAsync(int id);

    Task<PersonDetailViewModel?> GetDetailsAsync(int id);

    Task<int> CreateAsync(PersonCreateViewModel model);

    Task UpdateAsync(PersonEditViewModel model);

    Task DeleteAsync(int id);

    Task<List<Person>> SearchAsync(string query);

    Task<FamilyTreeViewModel> GetFamilyTreeAsync(int personId);
}
```

---

# 41. ViewModel Kullanımı

Entity modelleri doğrudan View'a gönderilmemelidir.

Örneğin:

```text
PersonCreateViewModel
PersonEditViewModel
PersonDetailViewModel
FamilyTreeViewModel
PersonSearchViewModel
```

kullanılmalıdır.

---

# 42. Hata Yönetimi

Global exception handling kullanılmalıdır.

Kullanıcıya:

```text
Bir hata oluştu.
Lütfen daha sonra tekrar deneyiniz.
```

gibi güvenli mesaj gösterilmelidir.

Database exception detayları kullanıcıya gösterilmemelidir. Özellikle MySQL bağlantı dizesi, sunucu adı veya hata kodları (`MySqlException`) doğrudan kullanıcıya yansıtılmamalıdır.

Development ortamında detaylı hata kullanılabilir.

---

# 43. Logging

ASP.NET Core ILogger kullanılmalıdır.

Loglarda:

* Kullanıcı
* İşlem
* Tarih
* Hata

gibi bilgiler tutulabilir.

Ancak:

* TC Kimlik No
* Şifre
* Hassas kişisel bilgiler
* MySQL bağlantı dizesi / kimlik bilgileri

loglanmamalıdır.

---

# 44. Test

Önemli servisler için Unit Test yazılmalıdır.

Özellikle:

```text
Kardeş hesaplama
Çocuk hesaplama
Torun hesaplama
Yeğen hesaplama
Atalar
Alt soy
Döngü kontrolü
```

test edilmelidir.

Örnek:

```text
Dede
 └── Baba
      └── Çocuk
```

verildiğinde:

```text
Dede -> Torun = Çocuk
```

doğru şekilde bulunmalıdır.

Unit testlerde gerçek MySQL veritabanına bağımlı olunmamalı; EF Core InMemory provider veya SQLite InMemory kullanılabilir. Entegrasyon testleri için ise gerçek MySQL davranışını doğrulamak amacıyla Testcontainers (MySQL container) tercih edilmelidir.

---

# 45. İlk MVP

İlk sürümde aşağıdaki özellikler tamamlanmalıdır:

### Faz 1

* [ ] ASP.NET Core projesi
* [ ] MySQL bağlantısı (Pomelo.EntityFrameworkCore.MySql)
* [ ] Entity Framework Core
* [ ] Person modeli
* [ ] Migration
* [ ] Kişi ekleme
* [ ] Kişi düzenleme
* [ ] Kişi silme
* [ ] Kişi listeleme
* [ ] Kişi detay sayfası

### Faz 2

* [ ] Anne ilişkisi
* [ ] Baba ilişkisi
* [ ] Çocuk hesaplama
* [ ] Kardeş hesaplama
* [ ] Eş ilişkisi

### Faz 3

* [ ] Fotoğraf yükleme
* [ ] Çoklu fotoğraf
* [ ] Fotoğraf galerisi
* [ ] Ana fotoğraf

### Faz 4

* [x] Soy ağacı görselleştirmesi (D3.js)
* [x] Zoom
* [x] Pan
* [x] Kişi seçme
* [x] Kişi detayına geçiş

### Faz 5

* [x] Torun
* [x] Yeğen
* [x] Dede
* [x] Nine
* [x] Amca
* [x] Dayı
* [x] Hala
* [x] Teyze
* [x] Kuzen

> Not: Amca/Dayı/Hala/Teyze ayrımı kişinin cinsiyetine bağlı olduğundan, Person modeline
> opsiyonel bir `Cinsiyet` alanı eklendi (bkz. Bölüm 5). Cinsiyeti girilmemiş
> ebeveyn kardeşleri bu dört listede görünmez; Kuzenler listesi ise cinsiyetten
> bağımsız olarak hesaplanır.

### Faz 6

* [x] Kullanıcı sistemi (ASP.NET Core Identity)
* [x] Roller (Admin / Editor / Viewer)
* [x] Yetkilendirme (global `RequireAuthenticatedUser` fallback policy + rol bazlı `[Authorize(Roles=...)]`)
* [x] Audit log (AuditLogs tablosu, Admin-only görüntüleme sayfası)
* [x] Yedekleme (mysqldump tabanlı — hem Admin panelinden anlık indirme hem `scripts/backup.sh` ile cron)
* [x] Veri dışa aktarma (Kişiler listesi CSV olarak dışa aktarılabiliyor)

> Not: İlk Admin hesabı `Seed:AdminEmail` / `Seed:AdminPassword` yapılandırma değerleri
> (User Secrets veya ortam değişkeni ile) üzerinden, uygulama ilk kez ve hiç kullanıcı
> yokken başlatıldığında otomatik oluşturulur. Bu değerler koda veya appsettings.json'a
> **asla** yazılmamalıdır (bkz. README "Kurulum").

---

# 46. Claude Code Çalışma Kuralları

Claude Code bu projede kod üretirken:

1. Önce mevcut proje yapısını incele.
2. Var olan kodu gereksiz yere değiştirme.
3. Yeni özellik eklemeden önce mevcut mimariye uy.
4. Aynı işlev için tekrar kod yazma.
5. Service katmanı kullan.
6. Entity Framework Core ilişkilerini açıkça tanımla (self-referencing FK'lerde `DeleteBehavior.Restrict` kullanmayı unutma).
7. Migration oluşturmadan önce model değişikliklerini kontrol et.
8. Güvenlik açıklarına karşı kodu kontrol et.
9. Kullanıcıdan gelen hiçbir veriye doğrudan güvenme.
10. Dosya yükleme işlemlerinde güvenlik kontrolü yap.
11. TC Kimlik No gibi hassas bilgileri gereksiz şekilde UI/API/log içerisinde gösterme.
12. Kodun tamamını değiştirmek yerine mümkün olan en küçük değişikliği yap.
13. Her önemli değişiklikten sonra build/test çalıştır.
14. Hata oluşursa önce hatanın kaynağını analiz et.
15. Çalışan özellikleri bozacak refactoring yapma.
16. MySQL'e özgü kısıtlamaları göz önünde bulundur (index anahtar uzunluğu, collation, `utf8mb4`, cascade path kısıtları).

---

# 47. Claude Code Görev Formatı

Yeni bir özellik istendiğinde şu sırayla ilerle:

```text
1. Proje yapısını incele
2. İlgili modelleri bul
3. İlgili service'i bul
4. İlgili controller'ı bul
5. İlgili ViewModel'i bul
6. Gerekli değişiklikleri planla
7. Kod değişikliklerini yap
8. Migration gerekiyorsa oluştur
9. Build çalıştır
10. Test çalıştır
11. Hataları düzelt
12. Yapılan değişiklikleri özetle
```

---

# 48. Veritabanı İlişki Şeması

Temel ilişki:

```text
                    ┌──────────────┐
                    │    Person    │
                    └──────┬───────┘
                           │
            ┌──────────────┼──────────────┐
            │              │              │
            ▼              ▼              ▼
         AnneId          BabaId       PersonPhotos
            │              │
            └──────┬───────┘
                   │
                   ▼
                Person
```

Eş ilişkisi:

```text
Person
   │
   │
   ▼
SpouseRelationship
   │
   ▼
Person
```

---

# 49. Önemli Tasarım Kararı

Akrabalık ilişkilerinin tamamını veritabanında ayrı ayrı saklama.

Örneğin:

```text
Kardeş
Torun
Yeğen
Amca
Dayı
Hala
Teyze
Kuzen
```

gibi ilişkileri mümkün olduğunca temel ilişkilerden türet.

Temel veri:

```text
Anne
Baba
Eş
```

üzerinden aile ağacını hesapla.

Bu yaklaşım veri tutarlılığını önemli ölçüde artırır.

---

# 50. Gelecekteki Özellikler

Mimari aşağıdaki özelliklerin eklenmesine uygun olmalıdır:

* Aile olayları
* Evlilikler
* Boşanmalar
* Askerlik bilgileri
* Eğitim bilgileri
* Meslek bilgileri
* Yaşadığı şehirler
* Göç bilgileri
* Mezarlık bilgileri
* Belge/fotoğraf arşivi
* Ses kayıtları
* Video kayıtları
* Aile hikâyeleri
* Aile kronolojisi
* Harita üzerinde yaşam yerleri
* ~~GEDCOM import/export~~ (uygulandı, bkz. Bölüm 51)
* ~~PDF soy ağacı dışa aktarma~~ (uygulandı, bkz. Bölüm 51.1 — `/FamilyTree` sayfasında "PDF İndir")
* ~~PNG/SVG soy ağacı dışa aktarma~~ (uygulandı, bkz. Bölüm 51.1 — `/FamilyTree` sayfasında "PNG İndir" / "SVG İndir")
* ~~Excel/CSV dışa aktarma~~ (CSV kısmı uygulandı — `PersonController.ExportCsv`; Excel formatı henüz yok)
* Gelişmiş arama
* Aile bazlı kullanıcı yetkilendirmesi

---

# 51. GEDCOM Desteği

**Durum: Uygulandı.** `/Gedcom` sayfası GEDCOM 5.5.1 formatında içe/dışa aktarma sağlar.

* **Dışa aktarma** (`IGedcomService.ExportAsync`, tüm authenticated kullanıcılar): tüm kişiler
  INDI, anne/baba+çocuk grupları ile eş ilişkileri FAM kayıtları olarak yazılır. Anne/Baba
  alanlarının kendisi zaten cinsiyet taşıdığı için HUSB/WIFE ataması doğrudan buradan yapılır;
  eşleşen bir anne/baba çifti yoksa (yalnızca eş ilişkisi varsa) `Cinsiyet` alanına bakılır.
  TC Kimlik No **hiçbir zaman** dışa aktarılmaz.
* **İçe aktarma** (`IGedcomService.ImportAsync`, Admin/Editor): yüklenen `.ged` dosyası
  ayrıştırılır, her INDI yeni bir `Person`, her FAM kaydı anne/baba ataması ve (varsa) bir
  `SpouseRelationship` oluşturur. Tüm işlem tek bir DB transaction'ı içinde yapılır — bir hata
  oluşursa hiçbir kayıt eklenmez. Yalnızca tam gün/ay/yıl içeren tarihler (`12 MAR 1950`)
  kesin tarih olarak kaydedilir; belirsiz tarihler (`ABT 1950`, yalnızca yıl vb.) yanlış
  kesinlik oluşturmamak için ham metin olarak kişinin Açıklama alanına not düşülür. Dosya
  yükleme güvenliği için: uzantı (`.ged`), boyut limiti (10 MB) ve içerik kontrolü (ilk satır
  `0 HEAD` ile başlamalı) uygulanır.

Bu tasarım Bölüm 49'daki "temel ilişkilerden türet" prensibiyle uyumludur: GEDCOM'un
Event/Place/Source gibi ek kavramları şu an desteklenmiyor (Person açıklama alanına not
olarak düşülüyor), ancak veri modeli bunların ileride ayrı tablolar olarak eklenmesine
engel olmayacak şekilde tasarlanmıştır.

---

# 51.1. PDF / PNG / SVG Soy Ağacı Dışa Aktarma

**Durum: Uygulandı.** `/FamilyTree/{id}` sayfasındaki "PDF İndir", "PNG İndir" ve
"SVG İndir" butonları, o an ekranda yüklü olan tüm soy ağacını (kullanıcının açtığı tüm
genişletmeler dahil, mevcut zoom/pan durumundan bağımsız olarak tam içerik) sırasıyla
`.pdf`, `.png` ve `.svg` dosyası olarak indirir. İşlem **tamamen istemci tarafında**
(`familytree.js`) gerçekleşir — sunucuda ayrıca bir headless tarayıcı veya render motoru
çalıştırılmaz, bu da CLAUDE.md'nin "gereksiz bağımlılık oluşturulmamalı" ilkesine uygundur.

Üç format ortak bir `buildExportSvg()` fonksiyonunu paylaşır (bağımsız bir `<svg>` içine
başlık/tarih ile birlikte tam içeriği kopyalar, fotoğrafları base64 gömer). `exportSvgFile()`
bu SVG'yi doğrudan dosya olarak indirir (en sadık/vektörel sonuç, Illustrator/Inkscape'te
düzenlenebilir). `exportPng()` ve `exportPdf()` ise aynı SVG'yi `svgToCanvas()` ile
rasterize edip sırasıyla PNG/JPEG olarak indirir (bkz. aşağıdaki font bulgusu — PDF için
JPEG kullanılır, PNG dışa aktarma kendi başına bu soruna takılmaz çünkü jsPDF hiç devreye
girmez).

**Teknik yaklaşım ve önemli bir bulgu:** İlk denemede D3 SVG'sini doğrudan `svg2pdf.js` ile
jsPDF'e gömülü bir TrueType font (Noto Sans) kullanarak vektör metin olarak aktarmayı
denedik. Bu yaklaşım **Türkçe karakterlerde (İ, ş, Ş, ğ, ı) sessizce yanlış glif üretti**
(ör. "İsmail" → "0smail", "Ağacı" → "Aac1") — jsPDF v2.5.2'nin özel TTF font gömme
mekanizmasında bu belirli Unicode aralığıyla ilgili bir hata olduğu doğrulandı (hem tam font
hem alt kümelenmiş font ile aynı hata tekrarlandı, yani sorun font subsetting değil jsPDF'in
kendisiydi). Bu nedenle mimari değiştirildi:

1. Mevcut D3 SVG içeriği (linksLayer + nodesLayer) klonlanıp bağımsız bir `<svg>` içine, tam
   içerik sınırlarını kapsayacak şekilde (mevcut ekran zoom/pan'inden bağımsız) yerleştirilir.
   Kişi fotoğrafları (`<image href="/uploads/...">`) base64 `data:` URI'lerine önceden
   gömülür — aksi halde SVG bir `blob:` URL üzerinden `<img>` ile rasterize edilirken iç içe
   ağ isteklerinin zamanlaması tarayıcıda güvenilir şekilde beklenmeyip fotoğraflar boş
   kalabiliyordu (bu da test sırasında bulunup düzeltilen ikinci bir hataydı).
2. Bu SVG, tarayıcının **kendi** (doğru) font render motoruyla bir `<canvas>`'a çizilir
   (2x çözünürlük, baskı kalitesi için).
3. Canvas, `image/jpeg` (kalite 0.95) olarak dışa aktarılıp jsPDF'e `addImage()` ile tek bir
   resim olarak gömülür. **PNG değil JPEG kullanılmalı:** aynı canvas içeriği ham PNG olarak
   ~70 KB iken, jsPDF'in PNG kodlayıcısından geçince ~4 MB'a şişiyor (jsPDF'e özgü bir
   verimsizlik); JPEG ile nihai PDF ~80 KB civarında kalıyor.

Bu üç noktadan biri atlanırsa (jsPDF metin embedding'e geri dönülürse, fotoğraf inlining
kaldırılırsa — bu PNG/SVG dışa aktarmayı da etkiler, sadece PDF'e özgü değildir — ya da
PDF'de PNG formatına geri dönülürse) sırasıyla: Türkçe karakter bozulması, kayıp fotoğraflar
veya aşırı büyük dosya boyutu problemleri geri gelir — bu üçü de gerçek tarayıcıda
(Playwright ile ekran görüntüsü/indirme yakalama + `pdftoppm` ile PDF'i görsel olarak
render ederek) doğrulanmış, tesadüfi değil tekrar üretilebilir bulgulardır.

Kütüphane: yalnızca `jspdf` (UMD, yerel barındırılan `wwwroot/lib/jspdf/jspdf.umd.min.js`);
`svg2pdf.js` ve özel font dosyaları artık kullanılmadığından depoda tutulmaz.

---

# 51.2. CSV Toplu Kişi İçe Aktarma

**Durum: Uygulandı.** `/Person` (Kişiler) sayfasındaki "CSV İçe Aktar" paneli (Admin/Editor),
`TCKimlikNo, Ad, Soyad, Cinsiyet, DogumTarihi, OlumTarihi, AnneTC, BabaTC` sütunlarını içeren
bir CSV dosyasından toplu kişi ekler. Aynı sayfadaki "Örnek Şablon İndir" bağlantısı
(`PersonController.CsvImportTemplate`) doğru sütun sırasını ve gerçek olmayan (açıkça
tekrarlı basamaklı) örnek TC Kimlik No'lar içeren bir başlangıç dosyası sağlar.

**Anne/Baba eşleştirme mantığı:** `AnneTC`/`BabaTC` sütunları, dosyadaki başka bir satırın
`TCKimlikNo`'suna **veya** veritabanında zaten kayıtlı bir kişinin TC'sine eşleşerek
ilişkiyi otomatik kurar (`CsvImportService.ImportAsync`, iki geçişli: önce tüm kişiler
oluşturulur ve TC→Id eşlemesi çıkarılır, sonra bu eşlemeyle anne/baba bağlanır). Bu, GEDCOM
içe aktarmadaki xref eşleştirme mantığının TC Kimlik No üzerinden kurulmuş halidir.

**Veri kalitesi kuralları** (satır bazında, tek bir hatalı satır tüm içe aktarmayı
başarısız kılmaz — satır atlanır veya ilgili alan boş bırakılır, bir uyarı eklenir):

* Ad veya Soyad boşsa satır atlanır.
* TC formatı geçersizse (11 haneli değil) TC'siz içe aktarılır.
* Aynı TC dosya içinde birden fazla geçiyorsa ikinci ve sonraki satırlar atlanır.
* TC veritabanında zaten kayıtlıysa yeni kişi oluşturulmaz (mevcut kayıt yine de
  anne/baba referansı olarak kullanılabilir) — bu, "upsert" değil "var olanı çakıştırma"
  davranışıdır; CSV import her zaman **yeni** kayıt oluşturur, mevcut kişileri güncellemez.
* Doğum/ölüm tarihi gelecekte olamaz, ölüm doğumdan önce olamaz, çözümlenemeyen tarihler
  boş bırakılır (GEDCOM içe aktarmadaki "yanlış kesinlik oluşturma" prensibiyle aynı).
* Kişi kendi annesi/babası olarak referans verilemez.
* Tek seferde en fazla 5000 satır (dosya boyutu limiti: 5 MB, yalnızca `.csv` uzantısı).

Tüm işlem tek bir DB transaction'ı içindedir; beklenmeyen bir hata oluşursa hiçbir kayıt
eklenmez. On adet uç durumla (mükerrer TC, eksik alan, geçersiz tarih/cinsiyet, kendine
referans, var olan kişiye çapraz referans vb.) gerçek tarayıcıda ve curl ile test edilmiş,
her biri beklenen davranışı (satır eklendi / atlandı / alan boş bırakıldı) doğru şekilde
üretmiştir.

---

# 52. Öncelikli Geliştirme Prensibi

Öncelik:

```text
Doğru veri modeli
        ↓
Doğru ilişkiler
        ↓
Güvenli API
        ↓
Kişi yönetimi
        ↓
Soy ağacı algoritması
        ↓
Görsel soy ağacı
        ↓
Gelişmiş özellikler
```

olmalıdır.

Görsel tasarıma başlamadan önce veri modelinin doğru oluşturulması kritik öneme sahiptir.

---

# 53. Claude Code'dan Beklenen Çıktı

Claude Code herhangi bir geliştirme yaptığında cevabın sonunda aşağıdaki formatta kısa bir özet vermelidir:

```text
Yapılanlar:
- ...
- ...
- ...

Değiştirilen dosyalar:
- ...
- ...

Migration:
- Gerekli / Gerekli değil

Test:
- Build: Başarılı / Başarısız
- Test: Başarılı / Başarısız

Sonraki önerilen adım:
- ...
```

---

# 54. Bağlantı Yapılandırması (appsettings.json)

`Program.cs` içinde DbContext kaydı:

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    )
);
```

`appsettings.json` örneği (gerçek değerler production'da User Secrets / environment variable ile sağlanmalıdır):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=FamilyTreeDb;User=familytree_user;Password=CHANGE_ME;CharSet=utf8mb4;"
  }
}
```

Gerekli NuGet paketleri:

```text
Pomelo.EntityFrameworkCore.MySql
Microsoft.EntityFrameworkCore.Design
Microsoft.EntityFrameworkCore.Tools
```

---

# 55. Başlangıç Komutu

Projeyi geliştirmeye başlarken Claude Code'a aşağıdaki görev verilebilir:

> Bu CLAUDE.md dosyasındaki kurallara uygun olarak projeyi oluştur.
>
> Öncelikle ASP.NET Core MVC + Entity Framework Core + MySQL (Pomelo.EntityFrameworkCore.MySql) tabanlı temel proje yapısını oluştur.
>
> İlk aşamada sadece Person, PersonPhoto ve SpouseRelationship modellerini oluştur.
>
> Entity Framework Core ilişkilerini doğru şekilde tanımla (self-referencing FK'lerde `DeleteBehavior.Restrict` kullan).
>
> MySQL bağlantısını yapılandır (utf8mb4 karakter seti ile).
>
> Initial migration oluştur.
>
> Kişi CRUD işlemlerini oluştur.
>
> Kişi detay sayfasını oluştur.
>
> Fotoğraf yükleme sistemini oluştur.
>
> Henüz soy ağacı görselleştirmesine geçme.
>
> Önce veri modelinin ve CRUD sisteminin doğru çalıştığını doğrula.
>
> Build ve test işlemlerini gerçekleştir.
>
> Daha sonra soy ağacı modülüne geçeceğiz.

---

# 56. Kritik Kural

Bu proje öncelikle bir **veri yönetim sistemi**, daha sonra bir **görselleştirme sistemi** olarak ele alınmalıdır.

Soy ağacı görselinin güzel görünmesinden daha önemli olan:

1. Veri bütünlüğü
2. Akrabalık ilişkilerinin doğruluğu
3. Güvenlik
4. Performans
5. Kullanılabilirlik
6. Ölçeklenebilirlik

olmalıdır.

Özellikle aile ilişkilerinde hatalı veri oluşması, görselde yanlış soy ağacı oluşturulmasına neden olacağından ilişki doğrulama mekanizmaları sistemin temel parçalarından biri olmalıdır.
