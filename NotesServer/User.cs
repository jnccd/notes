using Notes.Interface;
using System.Reflection;

namespace Notes.Server
{
    public class User
    {
        public readonly string Username;
        public readonly string Password;

        public Payload NotesPayload {
            get => notesPayload;
            set
            {
                notesPayload = value;
                notesPayloadText = notesPayload.ToString();
            }
        }
        private Payload notesPayload;
        public string NotesPayloadText
        {
            get => notesPayloadText;
            private set { }
        }
        private string notesPayloadText;

        private static readonly char _s = Path.DirectorySeparatorChar;
        public static string NotesDir = $".{_s}";
        public string NotesJsonPath
        {
            get => $"{NotesDir}{_s}{Username}-notes.json";
            private set { }
        }

        public User(string username, string password)
        {
            Username = username;
            Password = password;
            NotesPayload = new(new DateTime(2000, 1, 1), new List<Note>());
        }
    }
}
