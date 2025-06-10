using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Interface;
using System.Collections;
using System.Reflection;
using static NotesServer.Configuration;

namespace NotesServer.Services.Notes;

[RegisterImplementation(ServiceRegisterType.Singleton, typeof(PersistenceService))]
public class PersistenceService : IDisposable
{
    readonly NotesDbContext db = new();
    public IEnumerable<User> Users
    {
        get
        {
            lock (this)
            {
                return db.Users;
            }
        }
    }

    public PersistenceService(IConfiguration config)
    {
        // foreach (var user in db.Users)
        // {
        //     FixZeros(user.NotesPayload.Notes);
        // }

        // Init users
        foreach (string userDef in config["NOTES_USERS"]?.Split(' ') ?? [])
        {
            string[] userDefSplit = userDef.Split(':');
            var newUser = new User(userDefSplit[0], userDefSplit[1]);
            if (!db.Users.Any(x => x.Username == newUser.Username))
                db.Users.Add(newUser);
        }
        db.SaveChanges();
    }
    // public void FixZeros(List<Note> notes)
    // {
    //     var zeroGuid = Guid.ParseExact("00000000-0000-0000-0000-000000000000", "D");
    //     foreach (var note in notes)
    //     {
    //         if (note.Id == zeroGuid)
    //         {
    //             note.Id = Guid.NewGuid();
    //         }
    //         FixZeros(note.SubNotes);
    //     }
    // }

    public bool Exists() => db.Users.Any();
    public void Save() => db.SaveChanges();

    public void Dispose() => db.Dispose();
}
