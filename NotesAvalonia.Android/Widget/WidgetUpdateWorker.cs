using System;
using System.Linq;
using Android.Content;
using AndroidX.Work;
using Notes.Interface;
using Notes.Interface.DTO;
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
                // Never logged in / no separate widget session configured: there is nothing to
                // fetch or display. Report success so the periodic worker does not churn on a
                // guaranteed failure; the widget only becomes meaningful after the user logs in
                // inside the app (which provisions AuthBackendRefreshTokenForAndroidWidget).
                if (string.IsNullOrWhiteSpace(Config.Data.ServerUri) ||
                    string.IsNullOrWhiteSpace(Config.Data.AuthBackendRefreshTokenForAndroidWidget))
                    return Result.InvokeSuccess();

                var communicator = new Communicator(
                    Config.Data.ServerUri!,
                    Config.Data.AuthBackendRefreshTokenForAndroidWidget, newAuthBackendRefreshToken =>
                    {
                        Config.Data.AuthBackendRefreshTokenForAndroidWidget = newAuthBackendRefreshToken;
                        try { Config.Save(); } catch { }
                    },
                    (CommsState state) => { }
                );

                Payload? payload;
                try
                {
                    payload = communicator.ReqPayload();
                }
                finally
                {
                    communicator.Dispose();
                }

                var virtualRootNote = new Note() { SubNotes = payload?.Notes ?? [] };
                var widgetText = WidgetDataRepository.BuildWidgetText(virtualRootNote);
                if (widgetText == null)
                {
                    // Nothing to show (no notes, or only empty content): keep whatever the widget
                    // currently displays. Overwriting it with an empty string here would blank the
                    // widget whenever the server account is (temporarily) empty.
                    return Result.InvokeSuccess();
                }

                WidgetDataRepository.SaveData(ApplicationContext, widgetText);
                WidgetDataRepository.RequestUpdate(ApplicationContext);

                return Result.InvokeSuccess();
            }
            catch (Exception ex)
            {
                // Transient failure (network, auth/session expired, server error): log it and let
                // WorkManager retry on the next period.
                try { Notes.Interface.Logger.WriteLine(DateTime.Now.ToString() + $": Failed to update widget {ex}\n"); } catch { }

                // A dead/expired widget session can never recover on its own (the password is not
                // stored), so drop the stale token: later runs will short-circuit instead of
                // failing against the auth server on every period until the user logs in again.
                var message = ex.Message ?? "";
                if (message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("required client", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Config.Data.AuthBackendRefreshTokenForAndroidWidget = "";
                        Config.Save();
                    }
                    catch { }
                    return Result.InvokeSuccess();
                }

                return Result.InvokeFailure();
            }
        }
    }
}