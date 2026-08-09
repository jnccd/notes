using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Notes.Interface;
using Notes.Interface.DTO;
using NotesAvalonia.Configuration;
using NotesAvalonia.Views;

namespace NotesAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public Note VirtualRoot { get; private set; } = new();
    public Note? FocusedNote { get; private set; } = null;

    public MainViewModel()
    {

    }

    public void LoadNew(List<Note> notes)
    {
        VirtualRoot = new Note()
        {
            Data = { Expanded = true },
            SubNotes = notes
        };
        ReFlatten();
    }

    public void ReFlatten()
    {
        IEnumerable<FlattenedNote> flattenedNotes;
        if (FocusedNote == null)
            flattenedNotes = VirtualRoot.Flatten().Skip(1); // Skip virtual root
        else
            flattenedNotes = FocusedNote.Flatten(1);

        FlattenedNoteVMs.Clear();
        var newFlattenedNvms = flattenedNotes
                .Select(n => new FlattenedNoteViewModel(n));
        foreach (var fnvm in newFlattenedNvms)
            FlattenedNoteVMs.Add(fnvm);
    }

    // Login flyout bindings
    public string? LoginServerUri
    {
        get
        {
            return Config.Data.ServerUri;
        }
        set { Config.Data.ServerUri = value; SetProperty(ref Config.Data.ServerUri, value); }
    }
    public string? LoginServerUsername
    {
        get { return Config.Data.Username; }
        set { Config.Data.Username = value; SetProperty(ref Config.Data.Username, value); }
    }
    [ObservableProperty]
    private string _loginPassword = "";

    public ObservableCollection<FlattenedNoteViewModel> FlattenedNoteVMs { get; } = new();

    [ObservableProperty]
    private string _connectionState = "Disconnected";
    [ObservableProperty]
    private string _debugText = "";
    public void AddDebugText(string text)
    {
        if (Globals.RunConfig == "Release")
            return;
        DebugText = DebugText
            .Split('\n')
            .Append(text)
            .TakeLast(Globals.IsDesktop ? 4 : 32)
            .Aggregate((a, b) => a + "\n" + b);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
    private string? _newItemContent;
    private bool CanAddItem() => !string.IsNullOrWhiteSpace(NewItemContent);
    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItem()
    {
        // Notes.Add(new NoteViewModel() { Text = NewItemContent });
        // NewItemContent = null;
    }

    [RelayCommand]
    public void RemoveItem(FlattenedNoteViewModel toDeleteFlattenedNote)
    {
        var ogToDeleteFlattenedNote = toDeleteFlattenedNote.FlattenedNote.OriginalNote;
        var ogToDeleteFlattenedNoteParent = toDeleteFlattenedNote.FlattenedNote.Parent!.OriginalNote;
        ogToDeleteFlattenedNote.DeleteFrom(ogToDeleteFlattenedNoteParent);

        ReFlatten();
        if (mainView != null)
            Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
            {
                Type = NoteChangeType.Delete,
                NoteId = ogToDeleteFlattenedNote.Id
            });
    }

    [RelayCommand]
    public void ExportItemToClipboard(FlattenedNoteViewModel flattenedNoteVM)
    {
        var topLevel = TopLevel.GetTopLevel(mainView);
        if (topLevel != null)
        {
            var exportText = flattenedNoteVM.FlattenedNote.OriginalNote.SubtreeToStyledString();
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.Create(DataFormat.Text, exportText));
            topLevel.Clipboard?.SetDataAsync(dataTransfer);
        }
    }

    [RelayCommand]
    public void FocusNote(FlattenedNoteViewModel flattenedNoteVM)
    {
        if (FocusedNote == flattenedNoteVM.FlattenedNote.OriginalNote)
            FocusedNote = null;
        else
            FocusedNote = flattenedNoteVM.FlattenedNote.OriginalNote;

        ReFlatten();
    }

    [RelayCommand]
    public void ToggleNoteHidden(FlattenedNoteViewModel flattenedNoteVM)
    {
        flattenedNoteVM.Hidden = !flattenedNoteVM.Hidden;
    }

    [RelayCommand]
    public void ToggleNoteSubtreeHidden(FlattenedNoteViewModel flattenedNoteVM)
    {
        var recursiveSubnotesResult = flattenedNoteVM.FlattenedNote.OriginalNote.RecursiveSubNotes();
        foreach (var snResult in recursiveSubnotesResult)
        {
            snResult.Note.Data.Hidden = !snResult.Note.Data.Hidden;
        }

        // Notify UI
        var subtreeFlattenedNotesVM = FlattenedNoteVMs.Where(x => recursiveSubnotesResult.Any(y => x.FlattenedNote.OriginalNote.Id == y.Note.Id));
        foreach (var fnvm in subtreeFlattenedNotesVM)
        {
            fnvm.Hidden = fnvm.FlattenedNote.OriginalNote.Data.Hidden;
        }
    }

    [RelayCommand]
    public void RemoveDoneSubnotes(FlattenedNoteViewModel flattenedNoteVM)
    {
        var doneSubNotes = flattenedNoteVM.FlattenedNote.OriginalNote.RecursiveSubNotes()
            .Where(x => x.Note.Data.Done);
        foreach (var toDeleteSubNote in doneSubNotes)
        {
            toDeleteSubNote.Note.DeleteFrom(toDeleteSubNote.Parent);
        }

        ReFlatten();
        if (mainView != null)
            Config.Data.CurrentUsersUnsyncedChanges?.AddRange(
                doneSubNotes.Select(toDeleteSubNote => new NoteChange()
                {
                    Type = NoteChangeType.Delete,
                    NoteId = toDeleteSubNote.Note.Id
                }));
    }

    [RelayCommand]
    public void ToggleExpand(FlattenedNoteViewModel item)
    {
        item.Expanded = !item.Expanded;
        if (mainView != null)
            Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
            {
                Type = NoteChangeType.Update,
                NoteId = item.FlattenedNote.OriginalNote.Id,
                Data = item.FlattenedNote.OriginalNote.Data
            });

        if (item.Expanded && item.FlattenedNote.OriginalNote.SubNotes.Count == 0)
        {
            var newNote = Note.EmptyNote();
            item.FlattenedNote.OriginalNote.SubNotes.Add(newNote);
            if (mainView != null)
                Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
                {
                    Type = NoteChangeType.Add,
                    NoteId = newNote.Id,
                    Data = newNote.Data,
                    ParentId = item.FlattenedNote.OriginalNote.Id,
                    ChildInsertionIndex = item.FlattenedNote.OriginalNote.SubNotes.IndexOf(newNote)
                });

            // Focus the new note's TextBox
            Dispatcher.UIThread.Post(() =>
            {
                var newTextbox = mainView?.GetLogicalDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault(ic => (ic.DataContext as FlattenedNoteViewModel)?.FlattenedNote.OriginalNote == newNote);
                if (newTextbox != null)
                    newTextbox.Focusable = true;
                newTextbox?.Focus();
            });
        }
        if (!item.Expanded)
        {
            var toDeleteSubNotes = item.FlattenedNote.OriginalNote.SubNotes.Where(x => string.IsNullOrWhiteSpace(x.Data.DecodedText)).ToList();
            if (mainView != null)
                Config.Data.CurrentUsersUnsyncedChanges?.AddRange(toDeleteSubNotes.Select(subNote => new NoteChange()
                {
                    Type = NoteChangeType.Delete,
                    NoteId = subNote.Id,
                }));
            foreach (var toDeleteSubNote in toDeleteSubNotes)
                item.FlattenedNote.OriginalNote.SubNotes.Remove(toDeleteSubNote);
        }
        ReFlatten();
    }
}