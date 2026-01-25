using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace NotesAvalonia.Android
{
    public static class WidgetDataRepository
    {
        private const string PREFS_NAME = "MyWidgetPrefs";
        private const string KEY_DATA = "WidgetData";

        public static void SaveData(Context context, string data)
        {
            var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
            var editor = prefs?.Edit();
            editor?.PutString(KEY_DATA, data);
            editor?.Apply();
        }

        public static string GetLatestData(Context? context)
        {
            if (context == null) return "No data";
            var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
            return prefs?.GetString(KEY_DATA, "No data available") ?? "";
        }

        public static void RequestUpdate(Context context)
        {
            var appWidgetManager = AppWidgetManager.GetInstance(context);

            // Identify which widget(s) you want to update
            var componentName = new ComponentName(context, Java.Lang.Class.FromType(typeof(MyAppWidgetProvider)));
            var ids = appWidgetManager?.GetAppWidgetIds(componentName);

            // Build the broadcast Intent
            var intent = new Intent(context, typeof(MyAppWidgetProvider));
            intent.SetAction(AppWidgetManager.ActionAppwidgetUpdate);
            intent.PutExtra(AppWidgetManager.ExtraAppwidgetIds, ids);

            // Send it to the system — this will call MyAppWidgetProvider.OnUpdate()
            context.SendBroadcast(intent);
        }
    }
}