using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Worksheet> Worksheets { get; set; }
    public DbSet<Rating> Ratings { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<FavoriteWorksheet> FavoriteWorksheets { get; set; }
    public object DownloadLogs { get; internal set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User - Worksheet (One-to-Many)
        modelBuilder.Entity<Worksheet>()
            .HasOne(w => w.User)
            .WithMany(u => u.Worksheets)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Worksheet - FileCategory (Many-to-One)
        // modelBuilder.Entity<Worksheet>()
        //     .HasOne(w => w.Category)
        //     .WithMany()
        //     .HasForeignKey(w => w.Category)
        //     .OnDelete(DeleteBehavior.Restrict);
       modelBuilder.Entity<Worksheet>()
    .HasOne(w => w.Category)
    .WithMany(c => c.Worksheets) // קשר מפורש!
    .HasForeignKey(w => w.CategoryId)
    .OnDelete(DeleteBehavior.Restrict);
        // Worksheet - Rating (One-to-Many)
        modelBuilder.Entity<Rating>()
            .HasOne(r => r.Worksheet)
            .WithMany(w => w.Ratings)
            .HasForeignKey(r => r.WorksheetId)
            .OnDelete(DeleteBehavior.Cascade);

        // User - Rating (One-to-Many)
        modelBuilder.Entity<Rating>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // User - FavoriteWorksheet (One-to-Many)
        modelBuilder.Entity<FavoriteWorksheet>()
            .HasOne(fw => fw.User)
            .WithMany(u => u.FavoriteWorksheets)
            .HasForeignKey(fw => fw.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Worksheet - FavoriteWorksheet (One-to-Many)
        modelBuilder.Entity<FavoriteWorksheet>()
            .HasOne(fw => fw.Worksheet)
            .WithMany()
            .HasForeignKey(fw => fw.WorksheetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
