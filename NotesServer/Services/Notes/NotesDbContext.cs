using Microsoft.EntityFrameworkCore;

namespace NotesServer.Services.Notes;

public class NotesDbContext() : DbContext
{
    public DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var coneccString = Environment.GetEnvironmentVariable("POSTGRES_DB_ACCESS");
        options.UseNpgsql(coneccString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<User>()
                .Property(e => e.NotesPayloadJson)
                .HasColumnType("jsonb");
    }
}