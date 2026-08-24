using FamilyTree.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Person> Persons => Set<Person>();

    public DbSet<PersonPhoto> PersonPhotos => Set<PersonPhoto>();

    public DbSet<SpouseRelationship> SpouseRelationships => Set<SpouseRelationship>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Sulale> Sulaleler => Set<Sulale>();

    public DbSet<PersonSulale> PersonSulaleler => Set<PersonSulale>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasQueryFilter(p => !p.IsDeleted);

            entity.HasIndex(p => p.TcKimlikNo).IsUnique();
            entity.HasIndex(p => new { p.Ad, p.Soyad });
            entity.HasIndex(p => p.AnneId);
            entity.HasIndex(p => p.BabaId);

            entity.Property(p => p.Cinsiyet)
                .HasConversion<string>()
                .HasMaxLength(10);

            entity.HasOne(p => p.Anne)
                .WithMany(p => p.AnneCocuklari)
                .HasForeignKey(p => p.AnneId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Baba)
                .WithMany(p => p.BabaCocuklari)
                .HasForeignKey(p => p.BabaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sulale>(entity =>
        {
            entity.HasIndex(s => s.Ad).IsUnique();
        });

        modelBuilder.Entity<PersonSulale>(entity =>
        {
            // Bir kişi aynı sülaleye iki kez eklenemez; birden fazla FARKLI sülaleye
            // eklenebilmesi (many-to-many) bu tablonun asıl amacıdır.
            entity.HasIndex(ps => new { ps.PersonId, ps.SulaleId }).IsUnique();

            // Person üzerindeki soft-delete filtresiyle tutarlı: yumuşak silinmiş bir
            // kişinin sülale bağlantıları da varsayılan sorgularda görünmez (bkz. Bölüm 8.1'de
            // belgelenen Include + global query filter bulgusu — burada filtre PersonSulale'e
            // açıkça tanımlanarak aynı sınıf hatanın tekrarlanması önlendi).
            entity.HasQueryFilter(ps => !ps.Person.IsDeleted);

            entity.HasOne(ps => ps.Person)
                .WithMany(p => p.PersonSulaleler)
                .HasForeignKey(ps => ps.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ps => ps.Sulale)
                .WithMany(s => s.PersonSulaleler)
                .HasForeignKey(ps => ps.SulaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonPhoto>(entity =>
        {
            entity.HasIndex(pp => pp.PersonId);

            entity.HasOne(pp => pp.Person)
                .WithMany(p => p.Photos)
                .HasForeignKey(pp => pp.PersonId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SpouseRelationship>(entity =>
        {
            entity.HasIndex(sr => new { sr.Person1Id, sr.Person2Id }).IsUnique();

            entity.HasOne(sr => sr.Person1)
                .WithMany(p => p.SpouseRelationshipsAsPerson1)
                .HasForeignKey(sr => sr.Person1Id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sr => sr.Person2)
                .WithMany(p => p.SpouseRelationshipsAsPerson2)
                .HasForeignKey(sr => sr.Person2Id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.Tarih);
            entity.HasIndex(a => a.UserId);
        });
    }
}
