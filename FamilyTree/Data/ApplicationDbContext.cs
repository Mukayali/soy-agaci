using FamilyTree.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Person> Persons => Set<Person>();

    public DbSet<PersonPhoto> PersonPhotos => Set<PersonPhoto>();

    public DbSet<SpouseRelationship> SpouseRelationships => Set<SpouseRelationship>();

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

            entity.HasOne(p => p.Anne)
                .WithMany(p => p.AnneCocuklari)
                .HasForeignKey(p => p.AnneId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Baba)
                .WithMany(p => p.BabaCocuklari)
                .HasForeignKey(p => p.BabaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PersonPhoto>(entity =>
        {
            entity.HasOne(pp => pp.Person)
                .WithMany(p => p.Photos)
                .HasForeignKey(pp => pp.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }
}
