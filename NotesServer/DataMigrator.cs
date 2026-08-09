using Microsoft.EntityFrameworkCore;
using Notes.Interface.DTO;
using NotesServer.Services.Notes;

namespace NotesServer;

public static class DataMigrator
{
    public static void Migrate()
    {
        // var dbContext = new NotesDbContext(new DbContextOptions<NotesDbContext>());

        // foreach (var user in dbContext.Users)
        // {
        //     user.NotesPayload?.GetAllNotes().ForEach(x =>
        //     {
        //         x.Note.Data.Text = x.Note.Text ?? "";
        //         x.Note.Data.Done = x.Note.Done ?? false;
        //         x.Note.Data.Expanded = x.Note.Expanded ?? false;
        //         x.Note.Data.Hidden = x.Note.Hidden ?? false;
        //         x.Note.Data.Prio = x.Note.Prio ?? NotePriority.Medium;
        //     });
        //     user.NotesPayload?.SaveTime = DateTime.Now;
        // }

        // dbContext.SaveChanges();
    }
}