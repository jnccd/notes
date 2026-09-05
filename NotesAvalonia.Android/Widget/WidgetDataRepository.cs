using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using Notes.Interface.DTO;
using System;
using System.Linq;

namespace NotesAvalonia.Android
{
    public static class WidgetDataRepository
    {
        private const string PREFS_NAME = "MyWidgetPrefs";
        private const string KEY_DATA = "WidgetData";

        /// <summary>
        /// Builds the multi-line text the widget displays from a note tree (reusing the shared
        /// <see cref="Note.SubtreeToStyledString"/> formatting and stripping the virtual root line
        /// plus the top-level indent). Returns null when there is nothing to show, never throws
        /// on empty/edge-case content.
        /// </summary>
        public static string? BuildWidgetText(Note virtualRootNote)
        {
            if (virtualRootNote == null || virtualRootNote.SubNotes == null || virtualRootNote.SubNotes.Count == 0)
                return null;

            // Resolve symlinks against the payload root so links show their target's content.
            Note? FindInRoot(Note node, Guid id)
            {
                if (node.Id == id)
                    return node;
                foreach (var subNote in node.SubNotes)
                {
                    var found = FindInRoot(subNote, id);
                    if (found != null)
                        return found;
                }
                return null;
            }

            var lines = virtualRootNote.SubtreeToStyledString(id => FindInRoot(virtualRootNote, id))
                .Split('\n')
                .Skip(1)                    // first line is the (virtual) root note itself
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Length > 2 ? l[2..] : l) // strip the depth indent of top-level notes
                .ToList();

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

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