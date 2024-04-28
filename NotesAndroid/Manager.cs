using Android.Content;
using Notes.Interface;
using Configuration;
using System.Text;
using System.Diagnostics;

namespace NotesAndroid
{
    public static class Manager
    {
        public static Communicator comms = null;

        public static Context widgetContext;

        public static void UpdateWidgetText(RemoteViews widgetView)
        {
            widgetView.SetTextViewText(Resource.Id.widgetUpdateText,
                string.Format("Last update: {0:H:mm:ss}", DateTime.Now));

            //Debug.WriteLine("bulding shit");
            lock (Config.Data)
                widgetView.SetTextViewText(Resource.Id.widgetNoteView,
                    GetWidgetText(Config.Data.Notes).ToString());
            //Debug.WriteLine("doone!");

            widgetView.SetTextColor(Resource.Id.widgetNoteView, Android.Graphics.Color.Black);
        }

        public static StringBuilder GetWidgetText(List<Note> Notes, StringBuilder? re = null, int depth = 0)
        {
            re ??= new();
            foreach (Note note in Notes)
            {
                re.AppendLine(
                    new string(Enumerable.Repeat(' ', depth * 2).ToArray()) + // Add indentation
                    (note.Expanded ? "▼ " : "  ") +                           // Show expaded state
                    (note.Done ?
                        note.Text.Select(x => x + "" + (char)822).Combine() : // Cross through if done
                        note.Text));
                if (note.Expanded)
                    GetWidgetText(note.SubNotes, re, depth + 1);
            }
            //Debug.WriteLine($"text: {re}");
            return re;
        }
    }
}