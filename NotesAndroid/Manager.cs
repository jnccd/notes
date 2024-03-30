using Android.Content;
using Notes.Interface;
using Configuration;

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

            widgetView.SetTextViewText(Resource.Id.widgetNoteView,
                    Config.Data.Notes.Select(x => x.Text).Combine("\n"));

            widgetView.SetTextColor(Resource.Id.widgetNoteView, Android.Graphics.Color.Black);
        }
    }
}