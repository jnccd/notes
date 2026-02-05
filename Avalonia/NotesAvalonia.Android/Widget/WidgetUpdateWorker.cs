using System;
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
        public static Communicator? communicator = null;

        public WidgetUpdateWorker(Context context, WorkerParameters workerParams)
            : base(context, workerParams) { }

        public static void Init(Context context)
        {
            var workRequest = PeriodicWorkRequest.Builder.From<WidgetUpdateWorker>(TimeSpan.FromMinutes(30)).Build();
#pragma warning disable CS8604 // Possible null reference argument.
            WorkManager.GetInstance(context).EnqueueUniquePeriodicWork(
                "WidgetUpdateWork",
                ExistingPeriodicWorkPolicy.Keep,
                workRequest);
#pragma warning restore CS8604 // Possible null reference argument.

            try
            {
                if (communicator != null)
                    communicator.Dispose();
                if (Config.Data.ServerUri != null && Config.Data.ServerUsername != null && Config.Data.ServerPassword != null)
                    communicator = new Communicator(
                        Config.Data.ServerUri,
                        Config.Data.ServerUsername,
                        Config.Data.ServerPassword, (CommsState state) =>
                        {

                        }
                    );
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error while creating communicator");
                Console.Error.WriteLine(e);
            }
        }

        public override Result DoWork()
        {
            try
            {
                // Fetch data (HTTP call or compute)
                if (communicator != null)
                {
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
                }

                return Result.InvokeSuccess();
            }
            catch
            {
                return Result.InvokeRetry();
            }
        }
    }
}