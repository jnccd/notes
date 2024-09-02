using Notes.Server;
using System.Collections.Generic;

namespace NotesServer.BasicAuth
{
    public class BasicAuthOptions(List<User> users, bool writeLogs = false, bool give404 = false)
    {
        public const string BasicAuth = "BasicAuth";

        public List<User> Users { get; set; } = users;
        public bool WriteLogs { get; set; } = writeLogs;
        public bool Give404 { get; set; } = give404;
    }
}
