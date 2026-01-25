using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace NotesAvalonia.Android
{
    [BroadcastReceiver(Label = "My Widget", Exported = true)]
    [IntentFilter(new string[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
    [MetaData("android.appwidget.provider", Resource = "@xml/widget_info")]
    public class MyAppWidgetProvider : AppWidgetProvider
    {
        public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
        {
            foreach (var widgetId in appWidgetIds ?? [])
            {
                var views = new RemoteViews(context?.PackageName, Resource.Layout.widget_layout);

                // Update Data
                string data = WidgetDataRepository.GetLatestData(context);
                views.SetTextViewText(Resource.Id.widgetData, data);

                // Click handler to open Avalonia app
                if (context != null)
                {
                    var intent = new Intent(context, typeof(MainActivity));
                    var pendingIntent = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.Immutable);
                    views.SetOnClickPendingIntent(Resource.Id.widgetLayout, pendingIntent);
                }

                appWidgetManager?.UpdateAppWidget(widgetId, views);
            }
        }
    }
}