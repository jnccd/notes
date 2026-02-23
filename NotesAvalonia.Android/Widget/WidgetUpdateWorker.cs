using System;
using System.IO;
using System.Linq;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using AndroidX.Work;
using Notes.Interface;
using NotesAvalonia.Configuration;

namespace NotesAvalonia.Android
{
    public class WidgetUpdateWorker : Worker
    {
        public WidgetUpdateWorker(Context context, WorkerParameters workerParams)
            : base(context, workerParams) { }

        public static void Init(Context context)
        {
            var workRequest = PeriodicWorkRequest.Builder.From<WidgetUpdateWorker>(TimeSpan.FromMinutes(30)).Build();
            WorkManager.GetInstance(context).EnqueueUniquePeriodicWork(
                "WidgetUpdateWork",
                ExistingPeriodicWorkPolicy.Keep!,
                workRequest);
        }

        public override Result DoWork()
        {
            try
            {
                var communicator = new Communicator(
                    Config.Data.ServerUri!,
                    Config.Data.Username!,
                    Config.Data.KeycloakRefreshToken, (string newKeycloakRefreshToken) =>
                    {
                        Config.Data.KeycloakRefreshToken = newKeycloakRefreshToken;
                        Config.Save();
                    },
                    (CommsState state) => { }
                );

                var payload = communicator.ReqPayload();
                var virtualRootNote = new Note() { SubNotes = payload?.Notes ?? [] };
                var flattenedNotes = virtualRootNote.Flatten();
                var dataToShow = flattenedNotes.Count > 1 ?
                    flattenedNotes
                        .Select(x =>
                            Enumerable
                                .Repeat("  ", (int)x.Depth)
                                .Aggregate((x, y) => x + y)
                            + " " +
                            (x.OriginalNote.Expanded ? "▼" : "▶") +
                            (x.OriginalNote.Done ?
                                x.OriginalNote.Text.Select(x => x + "" + (char)822).Aggregate((x, y) => x + y) : // Cross through if done
                                x.OriginalNote.Text))
                        .Aggregate((x, y) => x + "\n" + y)
                    : "No notes available.";

                // Save to SharedPreferences
                WidgetDataRepository.SaveData(ApplicationContext, dataToShow);
                WidgetDataRepository.RequestUpdate(ApplicationContext);

                communicator.Dispose();

                return Result.InvokeSuccess();
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(Config.PersonalPath, "logs.txt"), DateTime.Now.ToString() + $": Failed to update widget {ex}\n");
                return Result.InvokeFailure();
            }
        }
    }
}