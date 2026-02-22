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

    public PersistenceService() { }

    public bool Exists() => db.Users.Any();
    public void Save() => db.SaveChanges();

    public void Dispose() => db.Dispose();
}
