using Android.Appwidget;
using Android.Content;
using Android.Runtime;
using Android.Text;
using Android.Views;
using AndroidX.AppCompat.App;
using System.Diagnostics;
using Notes.Interface;
using Config = Configuration.Config;
using Notes.Interface.UiController;
using NotesAndroid.UiInterface;
using Activity = Android.App.Activity;
using Android.Widget;

namespace NotesAndroid
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class MainActivity() : AppCompatActivity
    {
        bool unsavedChanges = false;
        DateTime lastSaveTime = DateTime.Now;
        NoteUi? rootNode = null;
        LinearLayout? rootLayout;

        const float fontSize = 10f;
        const int treeDepthPadding = 20;

        // Load data to GUI or save config to disk
        // config is updated in real time in GUI events
        /// <summary>
        /// Save config to disk
        /// </summary>
        /// <param name="updateSaveTime"></param>
        void SaveConfig(bool updateSaveTime = true)
        {
            lock (Config.Data)
            {
                if (updateSaveTime)
                    Config.Data.SaveTime = DateTime.Now;

                Config.Save();

                lastSaveTime = DateTime.Now;
            }
        }
        /// <summary>
        /// Load config into GUI
        /// </summary>
        public void LoadConfig()
        {
            lock (Config.Data)
            {
                rootLayout.RemoveViews(0, rootLayout.ChildCount);
                NoteUi.UiToNote.Clear();

                rootNode = new NoteUi(Config.Data.Notes, new ActivityWrapper(this), new LayoutWrapper(rootLayout), CreateUi);

                UpdateWidget();
            }
        }
        public void ReqServerNotes()
        {
            Logger.WriteLine($"UpdatePayload");
            if (Manager.comms == null)
            {
                Logger.WriteLine($"No comms");
                return;
            }

            Payload payload = null;
            try { payload = Manager.comms.ReqPayload(); }
            catch (System.Exception ex) { return; }

            Logger.WriteLine($"Got json");

            if (payload == null ||
                payload.Checksum != payload.GenerateChecksum() ||
                Config.Data.SaveTime > payload.SaveTime)
            {
                Logger.WriteLine($"Got invalid json");
                return;
            }

            Config.Data.Notes = payload.Notes;

            RunOnUiThread(() => {
                LoadConfig();
                SaveConfig(false);
            });
        }
        Payload GetNewPayload() => new(Config.Data.SaveTime, Config.Data.Notes);

        // Manage Ui
        IUiLayout CreateUi(IUiWindow mainWindow, Note note, int index = -1, int depth = 0)
        {
            bool enabled = true;
            var rootLayout = FindViewById<LinearLayout>(Resource.Id.noteLinearLayout);

            var newNoteLayout = (LinearLayout)this.LayoutInflater.Inflate(Resource.Layout.notebox, null);
            newNoteLayout.SetPadding((int)(this.Dip2px(treeDepthPadding) * (depth - 0.5)), this.Dip2px(7), 0, 0);
            if (index < 0)
                index = rootLayout.ChildCount;
            rootLayout.AddView(newNoteLayout, index);

            var noteTextbox = newNoteLayout.FindViewById<EditText>(Resource.Id.note);
            noteTextbox.Enabled = enabled;
            noteTextbox.Text = note.Text;
            noteTextbox.TextChanged += OnNoteChange;

            var checkBox = newNoteLayout.FindViewById<CheckBox>(Resource.Id.noteDone);
            checkBox.Enabled = enabled;
            checkBox.Checked = note.Done;
            checkBox.CheckedChange += OnNoteDone;

            var expandButton = newNoteLayout.FindViewById<Button>(Resource.Id.expandButton);
            expandButton.Enabled = enabled;
            expandButton.Click += ExpandButton_Click;

            return new LayoutWrapper(newNoteLayout);
        }

        public void Relayout(NoteUi? node = null, int depth = 0)
        {
            node ??= rootNode;
            if (node != null)
                foreach (var child in node.SubNotes)
                {
                    ((LayoutWrapper)child.UiLayout).Layout.Visibility = child.Shown ? ViewStates.Visible : ViewStates.Gone;
                    Relayout(child, depth + 1);
                }
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

        // Events
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            //Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            // Setup gui events
            var newNote = FindViewById<EditText>(Resource.Id.newNote);
            rootLayout = FindViewById<LinearLayout>(Resource.Id.noteLinearLayout);

            var syncButton = FindViewById<Button>(Resource.Id.syncButton);
            syncButton.Click += (s, e) => Task.Run(() => {
                ReqServerNotes();
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

            // Update GUI to config
            LoadConfig();

            Task.Run(() =>
            {
                // Setup communicator
                if (Config.Data.ServerUri != null)
                    Manager.comms = new Communicator(
                        Config.Data.ServerUri, 
                        Config.Data.ServerUsername, 
                        Config.Data.ServerPassword,
                        GetNewPayload, 
                        Logger.logger);

                // Autosave Thread
                Task.Run(AutosaveThread);
                
                // Hide the loading circle
                RunOnUiThread(() => {
                    var circle = FindViewById<ProgressBar>(Resource.Id.loadingCircle);
                    circle.Visibility = ViewStates.Invisible;
                });
            });
        }
        async void AutosaveThread()
        {
            ReqServerNotes();
            while (true)
            {
                await Task.Delay(500);
                if (unsavedChanges)
                {
                    unsavedChanges = false;
                    SaveConfig();
                    Manager.comms?.SendString(GetNewPayload().ToString());
                    UpdateWidget();
                }
            }
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
                ReqServerNotes();
                Manager.comms?.SendString(GetNewPayload().ToString());

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
                            ReqServerNotes();
                        });
                    });
                });
            }

            return base.OnOptionsItemSelected(item);
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            //Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
        protected override void OnResume()
        {
            base.OnResume();
            ReqServerNotes();
        }
        protected override void OnDestroy()
        {
            SaveConfig(false);

            Manager.comms.Dispose();
            base.OnDestroy();
        }
        // Note Events
        public void OnNoteChange(object? sender, TextChangedEventArgs e)
        {
            var ed = (EditText?)sender;
            var note = (ViewGroup?)ed?.Parent;
            var noteUiOrigin = NoteUi.UiToNote[new LayoutWrapper(note)];

            if (e.Text.Contains('\n'))
            {
                ed.Text = ed.Text.Replace("\n", "");

                int index = noteUiOrigin.Parent.SubNotes.IndexOf(noteUiOrigin);
                var insertionIndex = e.Start == 0 ? index : index + 1;

                noteUiOrigin.Parent.AddSubNoteBefore(new Note(), new ActivityWrapper(this), new LayoutWrapper(rootLayout), CreateUi, insertionIndex);
            }
            else if (e.AfterCount < e.BeforeCount && 
                     ed.Text.Length < 1 && 
                     e.BeforeCount < 3)
            {
                noteUiOrigin.Parent.RemoveSubNote(noteUiOrigin);
            }
            else
            {
                noteUiOrigin.Note.Text = ed.Text;
            }
            unsavedChanges = true;
        }
        public void OnNoteDone(object? sender, CompoundButton.CheckedChangeEventArgs e)
        {
            var checkBox = (CheckBox?)sender;
            var noteView = (ViewGroup?)checkBox?.Parent;
            if (noteView == null) return;
            var noteUiOrigin = NoteUi.UiToNote[new LayoutWrapper(noteView)];

            if (checkBox == null) return;
            noteUiOrigin.Note.Done = checkBox.Checked;

            unsavedChanges = true;
        }
        private void ExpandButton_Click(object? sender, EventArgs e)
        {
            var button = (Button?)sender;
            var noteView = (ViewGroup?)button?.Parent;
            if (noteView == null) return;
            var noteUiOrigin = NoteUi.UiToNote[new LayoutWrapper(noteView)];

            noteUiOrigin.ToggleExpand();

            unsavedChanges = true;
        }
    }
}