using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using EzAuth;
using Notes.Interface;
using Notes.Interface.DTO;
using NotesAvalonia.Configuration;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public record OpenUrlActionOnSystem(bool IsCurrentOperatingSystem, Action<string> OpenUrl);

public partial class MainView : UserControl
{
    public Communicator? communicator { get; private set; } = null;
    DateTime lastSaveTime = DateTime.MinValue;

    // Incoming payloads are applied without tearing down an active edit (see
    // ApplyReceivedPayloadSmart): data-only changes merge into the live tree in place, structural
    // changes reload but preserve the note being edited.
    //
    // The note that was last being edited (or is being edited during a structural reload), with the
    // exact content/revision at that moment. A payload can lag the last keystrokes (server fetches
    // lag the ~500ms autosave cadence), so applying it must not regress this note.
    Guid? lastEditedNoteId;
    NoteData? lastEditedNoteData;

    static NoteData CloneNoteData(NoteData data) => new()
    {
        Done = data.Done,
        Canceled = data.Canceled,
        Text = data.Text,
        Expanded = data.Expanded,
        Hidden = data.Hidden,
        Prio = data.Prio,
        Created = data.Created,
        LinkTargetId = data.LinkTargetId,
        Rev = data.Rev
    };

    static Note? FindNoteInTree(List<Note> notes, Guid id)
    {
        foreach (var note in notes)
        {
            if (note.Id == id)
                return note;
            var found = FindNoteInTree(note.SubNotes, id);
            if (found != null)
                return found;
        }
        return null;
    }

    void OnPayloadReceived(string receivedText, Payload? payload)
    {
        // Accept every server payload. Staleness/regression protection no longer relies on the
        // SaveTime gate (typing bumps the local SaveTime, which made remote changes - including
        // deletes - only apply seconds after you stopped typing): revision-aware merging in
        // ApplyReceivedPayloadSmart decides what actually wins.
        if (payload == null)
            return;

        var notes = payload.Notes;
        Dispatcher.UIThread.Post(() => ApplyReceivedPayloadSmart(notes));
    }

    void ApplyReceivedPayload(List<Note> notes)
    {
        lock (Config.Data)
        {
            var currentPayload = Config.Data.CurrentUsersNotePayload();
            if (currentPayload == null)
                return;

            currentPayload.Notes = notes;

            // Keep the exact content of the note the user just finished editing: the payload can
            // lag the last keystrokes (fetch cadence vs ~500ms autosave), so only a strictly newer
            // revision is allowed to replace it.
            if (lastEditedNoteId is Guid editedId && lastEditedNoteData is { } editedData)
            {
                var editedNode = FindNoteInTree(currentPayload.Notes, editedId);
                if (editedNode != null && editedNode.Data.Rev <= editedData.Rev)
                    editedNode.Data = CloneNoteData(editedData);
                lastEditedNoteId = null;
                lastEditedNoteData = null;
            }
        }
        LoadConfig();
        SaveConfig(false);
    }

    // Applies an incoming server payload without disturbing an active edit:
    //  - tree structure unchanged -> merge changed note data in place (no row rebuild, focus/IME
    //    and the E3 height freeze stay untouched);
    //  - structure changed (notes added/removed/moved) -> full reload, but the note currently
    //    being edited keeps its content and gets focus (and caret) back afterwards.
    void ApplyReceivedPayloadSmart(List<Note> incomingNotes)
    {
        var focusedTextBox = this.GetLogicalDescendants().OfType<TextBox>()
            .FirstOrDefault(tb => tb.IsFocused && tb.DataContext is FlattenedNoteViewModel);
        var focusedVm = focusedTextBox?.DataContext as FlattenedNoteViewModel;

        List<Note> localNotes;
        lock (Config.Data)
        {
            var currentPayload = Config.Data.CurrentUsersNotePayload();
            if (currentPayload == null)
                return;
            localNotes = currentPayload.Notes;
        }

        if (SameStructure(localNotes, incomingNotes))
        {
            ApplyDataOnlyMerge(localNotes, incomingNotes, focusedVm?.EffectiveNote);
            return;
        }

        // Structure changed: full reload. Protect the edited note from regressing and re-focus it.
        if (focusedVm != null)
        {
            lastEditedNoteId = focusedVm.EffectiveNote.Id;
            lastEditedNoteData = CloneNoteData(focusedVm.EffectiveNote.Data);
        }
        ApplyReceivedPayload(incomingNotes);
        if (focusedVm != null && focusedTextBox != null)
            RestoreFocusedNote(focusedVm.EffectiveNote.Id, focusedTextBox.CaretIndex);
    }

    static bool SameStructure(List<Note> a, List<Note> b)
    {
        var idsA = new List<Guid>();
        var idsB = new List<Guid>();
        FlattenNoteIds(a, idsA);
        FlattenNoteIds(b, idsB);
        return idsA.SequenceEqual(idsB);
    }

    static void FlattenNoteIds(List<Note> notes, List<Guid> ids)
    {
        foreach (var note in notes)
        {
            ids.Add(note.Id);
            FlattenNoteIds(note.SubNotes, ids);
        }
    }

    // Merges changed note data into the live tree without rebuilding any row. The note currently
    // being edited keeps its local content (a strictly newer revision from elsewhere will win on a
    // later reload). Returns whether anything changed.
    static bool MergeDataInPlace(List<Note> local, List<Note> incoming, Note? editingNote)
    {
        bool changed = false;
        for (int i = 0; i < Math.Min(local.Count, incoming.Count); i++)
        {
            var localNote = local[i];
            var incomingNote = incoming[i];
            if (localNote.Id != incomingNote.Id)
                continue; // shapes were checked; this is only a safety net
            if (!ReferenceEquals(localNote, editingNote) && incomingNote.Data.Rev > localNote.Data.Rev)
            {
                localNote.Data = CloneNoteData(incomingNote.Data);
                changed = true;
            }
            changed |= MergeDataInPlace(localNote.SubNotes, incomingNote.SubNotes, editingNote);
        }
        return changed;
    }

    void ApplyDataOnlyMerge(List<Note> localNotes, List<Note> incomingNotes, Note? editingNote)
    {
        bool changed = MergeDataInPlace(localNotes, incomingNotes, editingNote);
        if (!changed)
            return;

        // Rows read their note's data directly - refresh all bindings (except the note being
        // edited, whose content we deliberately did not touch).
        foreach (var fnvm in (DataContext as MainViewModel)?.FlattenedNoteVMs ?? [])
        {
            if (editingNote != null && ReferenceEquals(fnvm.EffectiveNote, editingNote))
                continue;
            fnvm.RefreshBindings();
        }
        SaveConfig(false);
    }

    void RestoreFocusedNote(Guid noteId, int caretIndex)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var textBox = this.GetLogicalDescendants().OfType<TextBox>()
                .FirstOrDefault(tb => tb.DataContext is FlattenedNoteViewModel fnvm &&
                    (fnvm.EffectiveNote.Id == noteId || fnvm.FlattenedNote.OriginalNote.Id == noteId));
            if (textBox == null)
                return;
            textBox.Focusable = true;
            textBox.Focus();
            if (caretIndex >= 0)
                textBox.CaretIndex = Math.Min(caretIndex, textBox.Text?.Length ?? 0);
            // On mobile the GotFocus handler re-freezes the editing height (E3).
        });
    }    public List<OpenUrlActionOnSystem> OpenUrlActionsOnSystem { get; private set; } = [
        new(OperatingSystem.IsWindows(), (url) =>
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            })),
        new(OperatingSystem.IsLinux(), (url) =>
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
                UseShellExecute = true
            }))
    ];

    private void InitCommunicatorBasedOnConfig(string? password = null)
    {
        if (Config.Data.ServerUri != null && Config.Data.Username != null)
        {
            if (communicator != null)
                communicator.Dispose();
            communicator = new Communicator(
                Config.Data.ServerUri,
                Config.Data.AuthBackendRefreshToken, (string authBackendRefreshToken) =>
                {
                    Config.Data.AuthBackendRefreshToken = authBackendRefreshToken;
                    Config.Save();
                },
                stateChanged: state =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var connectionBar = this.GetLogicalDescendants()
                                .OfType<Rectangle>()
                                .FirstOrDefault(x => x.Name == "ConnectionBar");
                        if (connectionBar == null)
                            return;
                        if (state == CommsState.Connected)
                        {
                            connectionBar.Fill = Avalonia.Media.Brushes.Green;
                        }
                        else if (state == CommsState.Disconnected)
                        {
                            connectionBar.Fill = Avalonia.Media.Brushes.Red;
                        }
                        if (viewModel != null)
                            viewModel.ConnectionState = state == CommsState.Disconnected ? "Disconnected" : $"Connected to {Config.Data.Username}@{Config.Data.ServerUri.Split("//").Last()}";
                    });
                },
                onPayloadRequestError: e =>
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        popupManager?.Show("Error Connecting to Server!", e.Message, TakeFocus: false, AlwaysAsFlyout: true);
                    });
                }
            );
            if (password != null)
            {
                communicator.DoNewLogIn(Config.Data.Username, password);
                Config.Data.AuthBackendRefreshTokenForAndroidWidget = communicator.GetSeparateSessionRefreshToken(Config.Data.Username, password);
            }
            communicator.RequestLoopInterval = 5000;
            communicator.StartRequestLoop(OnPayloadReceived);
        }
    }

    private void Handle_Communicator_On_MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        LoadConfig();

        Task.Run(() =>
        {
            Thread.CurrentThread.Name = "Autosave Thread";
            int lastPersistedQueueCount = 0;
            DateTime lastPersistTime = DateTime.Now;
            while (true)
            {
                Task.Delay(500).Wait();
                try
                {
                    var unsyncedChanges = Config.Data.CurrentUsersUnsyncedChanges;
                    if (unsyncedChanges == null || unsyncedChanges.Count == 0)
                    {
                        // Queue drained (e.g. after a successful send): persist the now
                        // empty queue so a restart does not re-send old changes. Don't
                        // bump SaveTime here - the server state is at least as new as
                        // ours after a successful sync, and bumping would make the
                        // request loop reject the server's payload as stale.
                        if (lastPersistedQueueCount != 0)
                        {
                            SaveConfig(false);
                            lastPersistedQueueCount = 0;
                        }
                        continue;
                    }

                    // Persist whenever the queue changed (new offline changes were added,
                    // or the previous send removed delivered ones). While a backlog just
                    // sits there (offline wait), only do a slow safety save instead of
                    // rewriting config.json every 500 ms. SaveTime is only bumped when
                    // the queue grew (fresh local edits); after a send or while idling
                    // the server state is at least as new, so we don't claim otherwise.
                    bool queueGrew = unsyncedChanges.Count > lastPersistedQueueCount;
                    bool queueChanged = unsyncedChanges.Count != lastPersistedQueueCount;
                    bool safetySaveDue = DateTime.Now - lastPersistTime > TimeSpan.FromSeconds(10);
                    if (queueChanged || safetySaveDue)
                    {
                        SaveConfig(queueGrew);
                        lastPersistedQueueCount = unsyncedChanges.Count;
                        lastPersistTime = DateTime.Now;
                    }

                    if (communicator == null)
                    {
                        // Not logged in / not configured yet: there is nothing to send to, but the
                        // changes were already persisted above. This is the normal state for an
                        // anonymous (offline) workspace - not an error, and it would spam popups
                        // every 500ms tick if we surfaced it.
                        continue;
                    }
                    communicator.SendChanges(unsyncedChanges);
                }
                catch (Exception e)
                {
                    // Never let one bad tick kill the autosave/retry loop.
                    Notes.Interface.Logger.WriteLine(e, Notes.Interface.LogLevel.Error);
                }
            }
        });
    }

    private void PasswordTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginButton_Click(sender, e);
        }
    }

    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (viewModel != null)
                viewModel.AddDebugText($"LoginButton_PointerPressed");
            var parent = (sender as Button)?.Parent;
            var server = viewModel?.LoginServerUri;
            var username = viewModel?.LoginServerUsername;
            var password = viewModel?.LoginPassword;
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                popupManager?.Show("Login Error", "Please fill in all fields.");
                return;
            }

            Config.Data.ServerUri = server;
            Config.Data.Username = username;

            InitCommunicatorBasedOnConfig(password);

            Config.Save();
        }
        catch (Exception ex)
        {
            if (viewModel != null)
                viewModel.AddDebugText(ex.ToString());
            popupManager?.Show("Login Error", ex.ToString());
        }
    }

    private void RegisterButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var server = viewModel?.LoginServerUri;
            if (string.IsNullOrWhiteSpace(server))
                throw new Exception("You need to set the Connect URL of the note server first!");
            var authBackendAddress = Communicator.GetAuthBackendAddress(server!, new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) });
            var url = authBackendAddress.RealmUrl + "/account";

            var action = OpenUrlActionsOnSystem.FirstOrDefault(x => x.IsCurrentOperatingSystem);

            if (action != null)
            {
                action.OpenUrl(url);
            }
            else
            {
                popupManager?.Show("Platform not supported", "This platform cant show links :(\nPlease open " + url);
            }
        }
        catch (Exception ex)
        {
            popupManager?.Show("Registration Error", ex.Message);
        }
    }
    private void ShowLogsTextBlock_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var lines = File.ReadAllLines(Config.PersonalPath + "log.txt");
            lines.Reverse();
            popupManager?.Show("Logs", string.Join(Environment.NewLine, lines), SelectableText: true);
        }
        catch (Exception ex)
        {
            if (viewModel != null)
                viewModel.AddDebugText(ex.ToString());
            popupManager?.Show("Error Showing Logs", "Could not read log file: " + ex.Message);
        }
    }

    void SaveConfig(bool updateSaveTime = true)
    {
        lock (Config.Data)
        {
            var window = this.Parent as Window;
            var windowPos = window?.Position;
            if (windowPos != null)
                Config.Data.Pos = window!.Position;
            if (window != null && window.FrameSize != null)
            {
                Config.Data.Width = window.FrameSize.Value.Width;
                Config.Data.Height = window.FrameSize.Value.Height;
            }

            var currentPayload = Config.Data.CurrentUsersNotePayload();
            if (updateSaveTime && currentPayload != null)
                currentPayload.SaveTime = DateTime.Now;

            Config.Save();

            lastSaveTime = DateTime.Now;
        }
    }
    void LoadConfig()
    {
        lock (Config.Data)
        {
            if (viewModel == null)
                return;
            var currentPayload = Config.Data.CurrentUsersNotePayload();
            var notes = currentPayload?.Notes;
            if (notes == null || notes.Count == 0)
            {
                // No notes yet - either not logged in yet (fresh config, no user payload) or the
                // current user has no notes on the server. Seed the UI with one empty note so it
                // is immediately usable; once a payload arrives it replaces this seed.
                notes = [Note.EmptyNote()];
                if (currentPayload != null)
                    currentPayload.Notes = notes;
            }
            viewModel.LoadNew(notes);
        }
    }
}