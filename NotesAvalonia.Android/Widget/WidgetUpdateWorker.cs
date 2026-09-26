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

        /// <summary>
        /// Writes one line to the app's log file (the "Show Logs" popup in the app reads the same
        /// file) and to logcat, so everything this worker decides can be seen either way:
        /// `adb logcat -s NotesWidget` on the phone, or the log file when the app is not running.
        /// </summary>
        static void Log(string message) => WidgetDataRepository.Log(message);

        /// <summary>
        /// Whether a failure message says the widget's own login session is gone for good.
        ///
        /// Keycloak answers a refresh with `invalid_grant` (e.g. "Session doesn't have required
        /// client") once the session behind the stored refresh token is gone - idle/max lifetime
        /// reached, client changed, session revoked. No amount of retrying brings it back (the
        /// password is deliberately not stored), so this has to be told apart from a normal
        /// network/HTTP failure.
        /// </summary>
        static bool IsDeadSession(string? message) =>
            message != null &&
            (message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("required client", StringComparison.OrdinalIgnoreCase));

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
                {
                    Log("no server/widget session configured yet - skipping until the app logs in");
                    return Result.InvokeSuccess();
                }

                // ReqPayload() does not throw on a failed request: it reports the error through this
                // callback and answers null. Without collecting it here the worker could not tell a
                // dead session from a plain network failure (which is exactly why the
                // invalid_grant handling below used to be unreachable dead code).
                Exception? fetchError = null;
                var communicator = new Communicator(
                    Config.Data.ServerUri!,
                    Config.Data.AuthBackendRefreshTokenForAndroidWidget, newAuthBackendRefreshToken =>
                    {
                        Config.Data.AuthBackendRefreshTokenForAndroidWidget = newAuthBackendRefreshToken;
                        try { Config.Save(); } catch { }
                    },
                    stateChanged: (CommsState state) => { },
                    onPayloadRequestError: e => fetchError = e
                );

                Payload? payload;
                string receivedText;
                try
                {
                    payload = communicator.ReqPayload(out receivedText);
                }
                finally
                {
                    communicator.Dispose();
                }

                // A null payload means the fetch failed (offline, HTTP error, dead session,
                // unparsable body) - the account having no notes arrives as a non-null payload with
                // an empty note list. It used to look like "nothing to show" and the run still
                // reported success: no retry, no trace, widget silently kept its old text.
                if (payload == null)
                {
                    if (IsDeadSession(fetchError?.Message))
                    {
                        Log($"widget session is dead ({fetchError!.Message}) - log in again in the app to provision a new one");
                        try
                        {
                            Config.Data.AuthBackendRefreshTokenForAndroidWidget = "";
                            Config.Save();
                        }
                        catch { }
                        return Result.InvokeSuccess();
                    }

                    string reason = fetchError?.Message ?? $"{receivedText.Length} chars received, no payload";
                    Log($"could not fetch a payload ({reason}) - keeping the displayed text, retrying later");
                    return Result.InvokeFailure();
                }

                var virtualRootNote = new Note() { SubNotes = payload.Notes ?? [] };
                var widgetText = WidgetDataRepository.BuildWidgetText(virtualRootNote);
                if (widgetText == null)
                {
                    // Nothing to show (no notes, or only empty content): keep whatever the widget
                    // currently displays. Overwriting it with an empty string here would blank the
                    // widget whenever the server account is (temporarily) empty.
                    Log($"payload has no displayable notes ({payload.Notes?.Count ?? 0} top level) - keeping the displayed text");
                    return Result.InvokeSuccess();
                }

                WidgetDataRepository.SaveData(ApplicationContext, widgetText);
                WidgetDataRepository.RequestUpdate(ApplicationContext);
                Log($"updated widget from {payload.Notes?.Count ?? 0} top level note(s), {widgetText.Length} chars");

                return Result.InvokeSuccess();
            }
            catch (Exception ex)
            {
                // Transient failure (network, auth/session expired, server error): log it and let
                // WorkManager retry on the next period.
                Log($"failed to update widget: {ex}");

                // A dead/expired widget session can never recover on its own (the password is not
                // stored), so drop the stale token: later runs will short-circuit instead of
                // failing against the auth server on every period until the user logs in again.
                if (IsDeadSession(ex.Message))
                {
                    Log("widget session is dead (invalid_grant) - log in again in the app to provision a new one");
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
