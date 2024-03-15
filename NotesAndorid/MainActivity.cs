using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.AppCompat.App;
using Java.Lang;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Notes.Interface;
using Android.Util;
using Config = Configuration.Config;

namespace NotesAndroid
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class MainActivity : AppCompatActivity
    {
        bool unsavedChanges = false;
        NoteUi rootNode = null;

        // Update Data
        public void UpdateGUItoNotes(List<Note> notes)
        {
            var rootLayout = FindViewById<LinearLayout>(Resource.Id.noteLinearLayout);

            rootLayout.RemoveViews(0, rootLayout.ChildCount);
            NoteUi.UiToNote.Clear();

            rootNode = new NoteUi(Config.Data.Payload.Notes, this);

            UpdateWidget();

            //parent.GetChildrenR().ForEach(x => x.Enabled = true);
        }
        public void UpdateWidget()
        {
            if (Manager.widgetContext != null)
            {
                RemoteViews remoteViews = new RemoteViews(Manager.widgetContext.PackageName, Resource.Layout.widget);
                ComponentName thisWidget = new ComponentName(Manager.widgetContext, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
                Manager.UpdateWidgetText(remoteViews);
                AppWidgetManager.GetInstance(Manager.widgetContext).UpdateAppWidget(thisWidget, remoteViews);
            }
        }
        public void UpdateNotes()
        {
            Logger.WriteLine($"UpdatePayload");
            if (Manager.comms != null)
            {
                Payload payload = null;
                try { payload = Manager.comms.ReqPayload(); }
                catch (System.Exception ex) { return; }

                Logger.WriteLine($"Got json");

                if (payload != null &&
                    payload.Checksum == payload.GenerateChecksum() &&
                    Config.Data.Payload.SaveTime < payload.SaveTime)
                {
                    Config.Data.Payload.Notes = payload.Notes;
                    Config.Data.Payload.SaveTime = payload.SaveTime;
                    Config.Save();
                    RunOnUiThread(() => UpdateGUItoNotes(Config.Data.Payload.Notes));
                }
                else
                    Logger.WriteLine($"Got invalid json");
            }
            else
                Logger.WriteLine($"No comms");
        }
        Payload GetNewPayload() => Config.Data.Payload;

        // Events
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            // Setup gui events
            var newNote = FindViewById<EditText>(Resource.Id.newNote);
            //newNote.TextChanged += OnNewNote;

            var syncButton = FindViewById<Button>(Resource.Id.syncButton);
            syncButton.Click += (s, e) => Task.Run(() => {
                UpdateNotes();
                Manager.comms.SendString(GetNewPayload().ToString());
            });

            var widgetButton = FindViewById<Button>(Resource.Id.widgetUpdateButton);
            widgetButton.Click += (o, e) =>
            {
                if (Manager.widgetContext != null)
                {
                    RemoteViews remoteViews = new RemoteViews(Manager.widgetContext.PackageName, Resource.Layout.widget);
                    ComponentName thisWidget = new ComponentName(Manager.widgetContext, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
                    Manager.UpdateWidgetText(remoteViews);
                    AppWidgetManager.GetInstance(Manager.widgetContext).UpdateAppWidget(thisWidget, remoteViews);
                }
            };

            Task.Run(() =>
            {
                // Update GUI to config
                RunOnUiThread(() => UpdateGUItoNotes(Config.Data.Payload.Notes));

                // Setup communicator
                if (Config.Data.ServerUri != null)
                    Manager.comms = new Communicator(
                        Config.Data.ServerUri, 
                        Config.Data.ServerUsername, 
                        Config.Data.ServerPassword,
                        GetNewPayload, 
                        Logger.logger);

                // Autosave Thread
                Task.Run(async () =>
                {
                    UpdateNotes();
                    while (true)
                    {
                        await Task.Delay(500);
                        if (unsavedChanges)
                        {
                            Config.Save();
                            Config.Data.Payload.SaveTime = DateTime.Now;
                            Manager.comms?.SendString(GetNewPayload().ToString());
                            UpdateWidget();
                            unsavedChanges = false;
                        }
                    }
                });
                
                // Hide the loading circle
                RunOnUiThread(() => {
                    var circle = FindViewById<ProgressBar>(Resource.Id.loadingCircle);
                    circle.Visibility = ViewStates.Invisible;
                });
            });
        }
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.actionbar_menu, menu);
            return base.OnCreateOptionsMenu(menu);
        }
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Resource.Id.menu_refresh_button)
            {
                UpdateNotes();
                Manager.comms.SendString(GetNewPayload().ToString());

                if (Manager.widgetContext != null)
                {
                    RemoteViews remoteViews = new RemoteViews(Manager.widgetContext.PackageName, Resource.Layout.widget);
                    ComponentName thisWidget = new ComponentName(Manager.widgetContext, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
                    Manager.UpdateWidgetText(remoteViews);
                    AppWidgetManager.GetInstance(Manager.widgetContext).UpdateAppWidget(thisWidget, remoteViews);
                }
            }
            if (item.ItemId == Resource.Id.menu_upstream_button)
            {
                this.ShowAsAlertPrompt("What Server Url should I use?", "", (string newServerUri) =>
                {
                    this.ShowAsAlertPrompt("What Server Username should I use?", "", (string newServerUsername) =>
                    {
                        this.ShowAsAlertPrompt("What Server Password should I use?", "", (string newServerPassword) =>
                        {
                            if (Manager.comms != null)
                                Manager.comms.Dispose();
                            Config.Data.ServerUri = newServerUri;
                            Config.Data.ServerUsername = newServerUsername;
                            Config.Data.ServerPassword = newServerPassword;
                            Manager.comms = new Communicator(Config.Data.ServerUri, Config.Data.ServerUsername, Config.Data.ServerPassword, GetNewPayload, Logger.logger);
                            //Manager.comms.StartRequestLoop(OnPayloadRecieved);
                            UpdateNotes();
                        });
                    });
                });
            }

            return base.OnOptionsItemSelected(item);
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
        protected override void OnResume()
        {
            base.OnResume();
            UpdateNotes();
        }
        protected override void OnDestroy()
        {
            Config.Save();

            Manager.comms.Dispose();
            base.OnDestroy();
        }

        // GUI Events
        //public void OnNewNote(object o, TextChangedEventArgs e)
        //{
        //    if (e.Text.Count() == 0)
        //        return;

        //    var newNote = FindViewById<EditText>(Resource.Id.newNote);

        //    AddNewNoteBox(new Note() { Text = newNote.Text }, true, true, true);

        //    newNote.Text = "";

        //    Config.Data.Payload.Update();
        //    unsavedChanges = true;
        //}
        public void OnNoteChange(object o, TextChangedEventArgs e)
        {
            EditText ed = (EditText)o;
            ViewGroup note = (ViewGroup)ed.Parent.Parent;
            ViewGroup notes = (ViewGroup)note.Parent;
            var noteUi = NoteUi.UiToNote[note];

            int i = notes.IndexOfChild(note);

            if (e.Text.Contains('\n'))
            {
                ed.Text = ed.Text.Replace("\n", "");

                int index = noteUi.Parent.SubNotes.IndexOf(noteUi);
                var insertionIndex = e.Start == 0 ? index : index + 1;

                noteUi.Parent.AddSubNoteAt(new Note(), this, insertionIndex);
            }
            else if (e.AfterCount < e.BeforeCount && 
                     ed.Text.Length < 1 && 
                     e.BeforeCount < 3)
            {
                notes.RemoveView(note);

                Config.Data.Payload.Notes.RemoveAt(i);

                Config.Data.Payload.Update();
                unsavedChanges = true;
            }
            else
            {
                Config.Data.Payload.Notes[i].Text = ed.Text;

                Config.Data.Payload.Update();
                unsavedChanges = true;
            }
        }
        private void OnNoteDone(object o, CompoundButton.CheckedChangeEventArgs e)
        {
            CheckBox ch = (CheckBox)o;
            ViewGroup note = (ViewGroup)ch.Parent.Parent;
            ViewGroup notes = (ViewGroup)note.Parent;

            int i = notes.IndexOfChild(note);

            Config.Data.Payload.Notes[i].Done = ch.Checked;

            Config.Data.Payload.Update();
            unsavedChanges = true;
        }
    }
}