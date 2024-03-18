using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using NotesAndroid2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NotesAndroid
{
    [BroadcastReceiver(Label = "HellApp Widget")]
    [IntentFilter(new string[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
    [MetaData("android.appwidget.provider", Resource = "@xml/appwidgetprovider")]
    public class AppWidget : AppWidgetProvider
    {
        public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
        {
            Manager.widgetContext = context;
            var me = new ComponentName(context, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
            appWidgetManager.UpdateAppWidget(me, BuildRemoteViews(context, appWidgetIds));
        }

        private RemoteViews BuildRemoteViews(Context context, int[] appWidgetIds)
        {
            var widgetView = new RemoteViews(context.PackageName, Resource.Layout.widget);

            Intent intent = new Intent(context, typeof(AppWidget));
            intent.SetAction(AppWidgetManager.ActionAppwidgetUpdate);
            intent.PutExtra(AppWidgetManager.ExtraAppwidgetIds, appWidgetIds);
            var piBackground = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent);
            widgetView.SetOnClickPendingIntent(Resource.Id.widgetUpdateText, piBackground);

            Manager.UpdateWidgetText(widgetView);
            Intent configIntent = new Intent(context, typeof(MainActivity));
            PendingIntent configPendingIntent = PendingIntent.GetActivity(context, 0, configIntent, 0);
            widgetView.SetOnClickPendingIntent(Resource.Id.widgetNoteView, configPendingIntent);

            return widgetView;
        }
    }
}