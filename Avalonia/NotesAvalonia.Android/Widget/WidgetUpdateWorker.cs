using System;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using AndroidX.Work;

namespace NotesAvalonia.Android
{
    public class WidgetUpdateWorker : Worker
    {
        public WidgetUpdateWorker(Context context, WorkerParameters workerParams)
            : base(context, workerParams) { }

        public static void Init(Context context)
        {
            var workRequest = PeriodicWorkRequest.Builder.From<WidgetUpdateWorker>(TimeSpan.FromMinutes(3)).Build();
#pragma warning disable CS8604 // Possible null reference argument.
            WorkManager.GetInstance(context).EnqueueUniquePeriodicWork(
                "WidgetUpdateWork",
                ExistingPeriodicWorkPolicy.Keep,
                workRequest);
#pragma warning restore CS8604 // Possible null reference argument.
        }

        public override Result DoWork()
        {
            try
            {
                // Fetch data (HTTP call or compute)
                // var newData = $"Background data {DateTime.Now}";

                // Save to SharedPreferences
                // WidgetDataRepository.SaveData(ApplicationContext, newData);
                // WidgetDataRepository.RequestUpdate(ApplicationContext);

                return Result.InvokeSuccess();
            }
            catch
            {
                return Result.InvokeRetry();
            }
        }
    }
}