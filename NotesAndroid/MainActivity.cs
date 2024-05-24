using Android.Appwidget;
using Android.Content;
using Android.Runtime;
using Android.Text;
using Android.Views;
using AndroidX.AppCompat.App;
using Notes.Interface;
using Config = Configuration.Config;
using Notes.Interface.UiController;
using NotesAndroid.UiInterface;
using Activity = Android.App.Activity;
using System.Numerics;
using Note = Notes.Interface.Note;
using Toolbar = Android.Widget.Toolbar;
using Android.Graphics;

namespace NotesAndroid
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class MainActivity() : AppCompatActivity
    {
        bool unsavedChanges = false;
        DateTime lastSaveTime = DateTime.Now;
        NoteUi? rootNode = null;
        LinearLayout? rootLayout;

        // Drag anim
        int dragAnimationTimer = 0;
        Task? dragAnimDriver = null;
        bool cancelDragAnim = false;
        NoteUi? draggedNoteUi = null;

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

                Relayout();
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

            Logger.WriteLine($"Got json from {payload?.SaveTime}");

            if (payload == null ||
                payload.Checksum != payload.GenerateChecksum()
                || Config.Data.SaveTime > payload.SaveTime
                )
            {
                Logger.WriteLine($"Got invalid json {payload == null} {payload?.Checksum != payload?.GenerateChecksum()} {Config.Data.SaveTime > payload?.SaveTime}");
                return;
            }

            Config.Data.Notes = payload.Notes;

            RunOnUiThread(() =>
            {
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
            expandButton?.Animate()?.
                Rotation(note.Expanded ? 90 : 0).
                SetDuration(0).
                Start();
            expandButton.Click += ExpandButton_Click;

            var dragButton = newNoteLayout.FindViewById<Button>(Resource.Id.dragButton);
            dragButton.Enabled = enabled;
            dragButton.Touch += DragButton_Touch;

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
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Base setup
            Logger.ConfigureLogger(logToFile: false);
            base.OnCreate(savedInstanceState);
            //Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            // Setup actionBar
            Toolbar? myToolbar = (Toolbar?)FindViewById(Resource.Id.my_toolbar);
            myToolbar?.InflateMenu(Resource.Menu.actionbar_menu);
            SetActionBar(myToolbar);

            // Setup gui events
            var newNote = FindViewById<EditText>(Resource.Id.newNote);
            rootLayout = FindViewById<LinearLayout>(Resource.Id.noteLinearLayout);

            var syncButton = FindViewById<Button>(Resource.Id.syncButton);
            if (syncButton != null)
                syncButton.Click += (s, e) => Task.Run(() =>
                {
                    ReqServerNotes();
                    Manager.comms.SendString(GetNewPayload().ToString());
                });

            var widgetButton = FindViewById<Button>(Resource.Id.widgetUpdateButton);
            if (widgetButton != null)
                widgetButton.Click += (o, e) =>
                {
                    if (Manager.widgetContext != null)
                    {
                        RemoteViews remoteViews = new(Manager.widgetContext.PackageName, Resource.Layout.widget);
                        ComponentName thisWidget = new(Manager.widgetContext, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
                        Manager.UpdateWidgetText(remoteViews);
                        AppWidgetManager.GetInstance(Manager.widgetContext)?.UpdateAppWidget(thisWidget, remoteViews);
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
                            ShowCommsState);

                // Autosave Thread
                Task.Run(AutosaveThread);

                // Hide the loading circle
                RunOnUiThread(() =>
                {
                    var circle = FindViewById<ProgressBar>(Resource.Id.loadingCircle);
                    circle.Visibility = ViewStates.Invisible;
                });
            });
        }
        void ShowCommsState(CommsState state)
        {
            RunOnUiThread(() =>
            {
                Toolbar? myToolbar = (Toolbar?)FindViewById(Resource.Id.my_toolbar);
                IMenuItem theButton = myToolbar.Menu.FindItem(Resource.Id.menu_connection_state);
                if (state == CommsState.Connected)
                {
                    var drawable = Resources.GetDrawable(Resource.Drawable.btn_radio_on_mtrl);
                    drawable.SetColorFilter(new BlendModeColorFilter(Color.Green, BlendMode.SrcIn));
                    theButton.SetIcon(drawable);
                    theButton.SetChecked(true);
                    theButton.SetVisible(true);
                }
                else if (state == CommsState.Working)
                {
                    var drawable = Resources.GetDrawable(Resource.Drawable.btn_radio_on_mtrl);
                    drawable.SetColorFilter(new BlendModeColorFilter(Color.LightCyan, BlendMode.SrcIn));
                    theButton.SetIcon(drawable);
                    theButton.SetChecked(false);
                }
                else if (state == CommsState.Disconnected)
                {
                    var drawable = Resources.GetDrawable(Resource.Drawable.btn_radio_off_mtrl);
                    drawable.SetColorFilter(new BlendModeColorFilter(Color.Red, BlendMode.SrcIn));
                    theButton.SetIcon(drawable);
                    theButton.SetChecked(false);
                }
                InvalidateOptionsMenu();
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
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Resource.Id.menu_refresh_button)
            {
                ReqServerNotes();
                Manager.comms?.SendString(GetNewPayload().ToString());

                if (Manager.widgetContext != null)
                {
                    RemoteViews remoteViews = new(Manager.widgetContext.PackageName, Resource.Layout.widget);
                    ComponentName thisWidget = new(Manager.widgetContext, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
                    Manager.UpdateWidgetText(remoteViews);
                    AppWidgetManager.GetInstance(Manager.widgetContext)?.UpdateAppWidget(thisWidget, remoteViews);
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
                            Manager.comms?.Dispose();
                            Config.Data.ServerUri = newServerUri;
                            Config.Data.ServerUsername = newServerUsername;
                            Config.Data.ServerPassword = newServerPassword;
                            Manager.comms = new Communicator(Config.Data.ServerUri, Config.Data.ServerUsername, Config.Data.ServerPassword, ShowCommsState);
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
            var circle = FindViewById<ProgressBar>(Resource.Id.loadingCircle);
            circle.Visibility = ViewStates.Visible;
            SetNoteEnabled(false);

            Task.Run(() =>
            {
                ReqServerNotes();

                RunOnUiThread(() =>
                {
                    circle.Visibility = ViewStates.Invisible;
                    SetNoteEnabled(false);
                });
            });

            base.OnResume();
        }
        void SetNoteEnabled(bool enabled)
        {
            foreach (var v in NoteUi.UiToNote.Keys)
            {
                ((LayoutWrapper)v).Layout.Enabled = enabled;
            }
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

                var newNoteUi = noteUiOrigin.Parent.AddSubNoteBefore(new Note(), new ActivityWrapper(this), new LayoutWrapper(rootLayout), CreateUi, insertionIndex);
                ((LayoutWrapper)newNoteUi.UiLayout).Layout.RequestFocus();
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
            button?.Animate()?.
                Rotation(noteUiOrigin.Note.Expanded ? 90 : 0).
                SetDuration(100).
                Start();

            unsavedChanges = true;
        }
        private void DragButton_Touch(object? sender, View.TouchEventArgs e)
        {
            //Debug.WriteLine($"hat das auch touch? {e.Event?.Action}"); 
            e.Handled = true;

            var but = (Button?)sender;
            var note = (ViewGroup?)but?.Parent;
            if (note == null)
                return;
            var noteUiOrigin = NoteUi.UiToNote[new LayoutWrapper(note)];
            var otherNoteViews = NoteUi.UiToNote.Values.
                    Select(x => ((LayoutWrapper)x.UiLayout).Layout).
                    Where(x => x != note);

            if (e.Event?.Action == MotionEventActions.Down)
            {

                FindViewById<ScrollView>(Resource.Id.noteScrollView)?.RequestDisallowInterceptTouchEvent(true);

                noteUiOrigin.UiProperties["DownX"] = e.Event?.GetRawX(0) ?? 0;
                noteUiOrigin.UiProperties["DownY"] = e.Event?.GetRawY(0) ?? 0;

                foreach (var noteUi in NoteUi.UiToNote.Values)
                {
                    var view = ((LayoutWrapper)noteUi.UiLayout).Layout;
                    noteUi.UiProperties["origX"] = view.GetX();
                    noteUi.UiProperties["origY"] = view.GetY();
                }

                dragAnimationTimer = 0;
                draggedNoteUi = noteUiOrigin;
                dragAnimDriver = Task.Run(async () =>
                {
                    Vector2 origMid, draggedPos, displaceVec;
                    var undraggedNotes = NoteUi.UiToNote.Values.Where(x => x != noteUiOrigin).ToArray();
                    cancelDragAnim = false;

                    while (true)
                    {
                        if (cancelDragAnim)
                            break;

                        RunOnUiThread(() =>
                        {
                            // Repell other notes
                            foreach (var noteUi in undraggedNotes)
                            {
                                if (cancelDragAnim)
                                    break;

                                try
                                {
                                    var view = ((LayoutWrapper)noteUi.UiLayout).Layout;

                                    origMid.X = (float)noteUi.UiProperties["origX"] + view.Width / 2f;
                                    origMid.Y = (float)noteUi.UiProperties["origY"] + view.Height / 2f;
                                    draggedPos.X = (float)noteUi.UiProperties["origX"] + view.Width / 3f * 2;
                                    draggedPos.Y = note.GetY();
                                    var draggedToOrig = origMid - draggedPos;
                                    var ang = Math.Atan2(draggedToOrig.Y, draggedToOrig.X);
                                    var animStrength = dragAnimationTimer / 30f;
                                    if (animStrength > 1)
                                        animStrength = 1;
                                    var mag = animStrength * 20000000 / draggedToOrig.LengthSquared();
                                    if (mag > 100)
                                        mag = 100;
                                    displaceVec.X = (float)Math.Cos(ang) * mag;
                                    displaceVec.Y = (float)Math.Sin(ang) * mag;

                                    view.Animate()?.
                                        X((float)noteUi.UiProperties["origX"] + displaceVec.X).
                                        Y((float)noteUi.UiProperties["origY"] + displaceVec.Y).
                                        SetDuration(0).
                                        Start();
                                }
                                catch { }
                            }
                        });

                        if (cancelDragAnim)
                            break;

                        dragAnimationTimer++;
                        await Task.Delay(32);
                    }
                });
            }
            else if (e.Event?.Action == MotionEventActions.Move)
            {
                // Drag note to touch pos
                note.Animate()?.
                    XBy(e.Event.GetRawX(0) - (float)noteUiOrigin.UiProperties["DownX"]).
                    YBy(e.Event.GetRawY(0) - (float)noteUiOrigin.UiProperties["DownY"]).
                    SetDuration(0).
                    Start();
                noteUiOrigin.UiProperties["DownX"] = e.Event?.GetRawX(0) ?? 0;
                noteUiOrigin.UiProperties["DownY"] = e.Event?.GetRawY(0) ?? 0;
            }
            else if (e.Event?.Action == MotionEventActions.Up)
            {
                // Return touch consumption rights
                var scrollView = FindViewById<ScrollView>(Resource.Id.noteScrollView);
                scrollView?.RequestDisallowInterceptTouchEvent(false);

                // Find note dragged to
                ViewGroup? noteViewAfterMouseY = null;
                foreach (var noteViews in otherNoteViews)
                {
                    if (noteViews.GetY() > e.Event.GetY() + but.GetY() + note.GetY() - note.Height / 2)
                    {
                        noteViewAfterMouseY = noteViews;
                        break;
                    }
                }
                //noteViewAfterMouseY?.SetBackgroundColor(Android.Graphics.Color.Red);

                // Apply move operation on underlying Note datastructure
                noteUiOrigin.Parent.RemoveSubNote(noteUiOrigin);
                var draggedTo = NoteUi.UiToNote[new LayoutWrapper(noteViewAfterMouseY)];
                draggedTo.Parent.AddSubNoteBefore(
                    noteUiOrigin.Note,
                    new ActivityWrapper(this),
                    new LayoutWrapper(rootLayout),
                    CreateUi,
                    draggedTo.Parent.SubNotes.IndexOf(draggedTo));

                // Cancel anims
                note.Animate()?.Cancel();
                cancelDragAnim = true;
                dragAnimDriver?.Wait();

                // Reload
                LoadConfig();
                unsavedChanges = true;
            }
        }
    }
}