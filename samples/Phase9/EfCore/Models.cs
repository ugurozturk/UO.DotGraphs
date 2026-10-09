using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfFixture;
[Table("annotated_books", Schema = "catalog")]
public class Book
{
    public int Id { get; set; }
    [Column("title_attr")] public string Title { get; set; } = "";
}
public class Author { public int Id { get; set; } }
public class LibraryContext : DbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Book>().ToTable("books", "library");
        model.Entity<Book>().Property(b => b.Title).HasColumnName("title");
        model.Entity<Author>().ToTable("authors");
        model.ApplyConfiguration(new BookConfiguration());
        model.ApplyConfigurationsFromAssembly(typeof(LibraryContext).Assembly);
        model.Ignore<Author>();
        if (Environment.GetEnvironmentVariable("TENANT") is { } schema)
            model.Entity<Book>().ToTable("tenant_books", schema);
        model.Entity<Book>().ToTable(Environment.GetEnvironmentVariable("TABLE"));
    }
    public IQueryable<Book> Search(string term) => Books.Where(b => b.Title.Contains(term));
}
// Declaration alone must not be linked to a DbContext or presented as an executed mapping.
public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder) => builder.ToTable("configured_books");
}
public class UnusedConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder) => builder.ToView("unused_view");
}
public static class Unsupported
{
    public static void Custom(ModelBuilder model) { }
    public static void Configure(ModelBuilder model, string name)
    {
        Custom(model);
        model.Entity(name).ToTable("shared");
        model.Entity<Book>().OwnsOne(b => b.Title);
        var other = new Book();
        model.Entity<Book>().Property(b => other.Title).HasColumnName("wrong_receiver");
        model.Entity<Book>().Property(b => b.Title.ToUpper()).HasColumnName("computed");
        model.Entity<Book>().Property(b => b.Title).HasColumnName(Environment.GetEnvironmentVariable("COLUMN"));
    }
}
