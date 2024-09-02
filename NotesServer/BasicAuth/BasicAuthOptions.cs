using Notes.Server;
using System.Collections.Generic;

namespace NotesServer.BasicAuth
{
    public class BasicAuthOptions(bool writeLogs = false, bool give404 = false)
    {
        public const string BasicAuth = "BasicAuth";

        public bool WriteLogs { get; set; } = writeLogs;
        public bool Give404 { get; set; } = give404;
    }
}
