using Notes.Interface;
using Notes.Server;
using System.Reflection;

namespace NotesServer.Notes
{
    public interface INotesEnvironmentService
    {
        public string UsersNotesJsonPath(User user);
        public List<User>? Users { get; }
    }

    public class NotesEnvironmentService : INotesEnvironmentService
    {
        private static readonly char _s = Path.DirectorySeparatorChar;
        private static string _notesDir = $".{_s}";
        public string UsersNotesJsonPath(User user) => $"{_notesDir}{_s}{user.Username}-notes.json";
        public List<User>? Users { get; private set; }

        public NotesEnvironmentService(IHostApplicationLifetime appLifetime, IConfiguration config)
        {
            // Init dir
            string? localDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            _notesDir = $"{localDir}{_s}notes";
            Directory.CreateDirectory(_notesDir);
            
            // Stop in invalid state
            if (string.IsNullOrWhiteSpace(config["NOTES_USERS"]))
            {
                Logger.WriteLine($"NOTES_USERS is empty! Closing...");
                appLifetime.StopApplication();
                return;
            }

            // Init users
            Users = [];
            foreach (string userDef in config["NOTES_USERS"]?.Split(' ') ?? [])
            {
                string[] userDefSplit = userDef.Split(':');
                var newUser = new User(userDefSplit[0], userDefSplit[1]);
                if (File.Exists(UsersNotesJsonPath(newUser)))
                {
                    Payload? parsedPayload = null;
                    try
                    {
                        parsedPayload = Payload.Parse(File.ReadAllText(UsersNotesJsonPath(newUser)));
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(ex);
                    }
                    if (parsedPayload != null)
                        newUser.NotesPayload = parsedPayload;
                }
                Users.Add(newUser);
            }
        }
    }
}
