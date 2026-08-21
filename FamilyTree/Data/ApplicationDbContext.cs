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

            entity.HasIndex(p => p.SulaleId);

            entity.HasOne(p => p.Sulale)
                .WithMany(s => s.Uyeler)
                .HasForeignKey(p => p.SulaleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Sulale>(entity =>
        {
            entity.HasIndex(s => s.Ad).IsUnique();
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
