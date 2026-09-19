using Microsoft.EntityFrameworkCore;
using Notes.Interface.DTO;

namespace NotesServer.Services.Notes;

public class NotesDbContext : DbContext
{
    public NotesDbContext(DbContextOptions<NotesDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    /// <summary>
    /// Notes that were removed from the active tree, one row per note. Every row carries the id of the
    /// user it belonged to, and that id is the only way trash is ever read - a query must always be
    /// scoped to the authenticated user, so one user's deleted notes can never show up for another.
    /// </summary>
    public DbSet<DeletedNote> DeletedNotes { get; set; }

    /// <summary>
    /// The deleted notes of a single user - the only shape trash should ever be read in. Rows are
    /// written with the authenticated user's id and can only be retrieved through it.
    /// </summary>
    public IQueryable<DeletedNote> DeletedNotesOf(string userId) =>
        DeletedNotes.Where(deletedNote => deletedNote.UserId == userId);

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

        modelBuilder.Entity<DeletedNote>(deletedNote =>
        {
            deletedNote.HasKey(e => e.Id);

            // Ownership: a deleted note belongs to exactly one user, and trash is only ever queried
            // per user. Cascades with the user, so nothing is left behind when an account goes.
            deletedNote
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // The deleted subtree: each row points at the trash entry of its parent, and the note that
            // was actually deleted has none. Purging a deletion removes its subtree with it.
            deletedNote
                .HasOne<DeletedNote>()
                .WithMany()
                .HasForeignKey(e => e.ParentDeletedNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            deletedNote.HasIndex(e => e.UserId);
            deletedNote.HasIndex(e => e.DeletionId);
            deletedNote.HasIndex(e => e.NoteId);
            deletedNote.HasIndex(e => e.ParentDeletedNoteId);

            // The note's data is jsonb: same storage as inside the active payload, and no migration
            // whenever NoteData gains a field.
            deletedNote.Property(e => e.DataJson).HasColumnType("jsonb");
        });
    }
}
