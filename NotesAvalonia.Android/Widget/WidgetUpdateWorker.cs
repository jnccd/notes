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
                    Config.Data.KeycloakRefreshTokenForAndroidWidget, newKeycloakRefreshToken =>
                    {
                        Config.Data.KeycloakRefreshTokenForAndroidWidget = newKeycloakRefreshToken;
                        Config.Save();
                    },
                    (CommsState state) => { }
                );

                var payload = communicator.ReqPayload();
                var virtualRootNote = new Note() { SubNotes = payload?.Notes ?? [] };
                var dataToShow = virtualRootNote.SubtreeToStyledString() // TODO: Add a way to filter virtual root in data instead of string representation and unify
                    .Split('\n')
                    .Skip(1)
                    .Select(x => x[2..])
                    .Aggregate((x, y) => x + "\n" + y);

                // Save to SharedPreferences
                WidgetDataRepository.SaveData(ApplicationContext, dataToShow);
                WidgetDataRepository.RequestUpdate(ApplicationContext);

                communicator.Dispose();

                return Result.InvokeSuccess();
            }
            catch (Exception ex)
            {
                try { Notes.Interface.Logger.WriteLine(DateTime.Now.ToString() + $": Failed to update widget {ex}\n"); } catch { }
                return Result.InvokeFailure();
            }
        }
    }
}