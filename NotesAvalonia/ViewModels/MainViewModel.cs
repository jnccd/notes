using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Notes.Interface;
using NotesAvalonia.Configuration;
using NotesAvalonia.Views;

namespace NotesAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public Note VirtualRoot { get; set; } = new();
    public MainViewModel()
    {

    }

    public void LoadNew(List<Note> notes)
    {
        VirtualRoot = new Note()
        {
            Expanded = true,
            SubNotes = notes
        };
        ReFlatten();
    }

    public void ReFlatten()
    {
        var flattenedNotes = VirtualRoot.Flatten().Skip(1); // Skip virtual root
        FlattenedNotes.Clear();
        var newFlattenedNvms = flattenedNotes
                .Select(n => new FlattenedNoteViewModel(n));
        foreach (var fnvm in newFlattenedNvms)
            FlattenedNotes.Add(fnvm);
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

    public ObservableCollection<FlattenedNoteViewModel> FlattenedNotes { get; } = new();

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
        ogToDeleteFlattenedNoteParent.SubNotes.Remove(ogToDeleteFlattenedNote);
        if (ogToDeleteFlattenedNoteParent.SubNotes.Count == 0)
            ogToDeleteFlattenedNoteParent.Expanded = false;

        ReFlatten();
        if (mainView != null)
            mainView.unsavedChanges = true;
    }

    [RelayCommand]
    public void ExportItemToClipboard(FlattenedNoteViewModel flattenedNoteVM)
    {
        var topLevel = TopLevel.GetTopLevel(mainView);
        if (topLevel != null)
        {
            var exportText = flattenedNoteVM.FlattenedNote.OriginalNote.SubtreeToStyledString();
            topLevel.Clipboard?.SetTextAsync(exportText);
        }
    }

    [RelayCommand]
    public void RemoveDoneSubnotes(FlattenedNoteViewModel flattenedNoteVM)
    {
        var doneSubNotes = flattenedNoteVM.FlattenedNote.OriginalNote.RecursiveSubNotes()
            .Where(x => x.Note.Done);
        foreach (var toDeleteSubNote in doneSubNotes)
        {
            if (toDeleteSubNote.Parent != null)
                toDeleteSubNote.Parent.SubNotes.Remove(toDeleteSubNote.Note);
        }
        foreach (var toDeleteSubNotesParent in doneSubNotes.Where(x => x.Parent != null))
        {
            var parent = toDeleteSubNotesParent.Parent;
            if (parent != null && parent.SubNotes.Count == 0)
                parent.Expanded = false;
        }

        ReFlatten();
        if (mainView != null)
            mainView.unsavedChanges = true;
    }

    [RelayCommand]
    public void ToggleExpand(FlattenedNoteViewModel item)
    {
        item.Expanded = !item.Expanded;
        if (item.Expanded && item.FlattenedNote.OriginalNote.SubNotes.Count == 0)
        {
            var newNote = Note.EmptyNote();
            item.FlattenedNote.OriginalNote.SubNotes.Add(newNote);
            if (mainView != null)
                mainView.unsavedChanges = true;

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
            item.FlattenedNote.OriginalNote.SubNotes.RemoveAll(x => string.IsNullOrWhiteSpace(x.Text));
            if (mainView != null)
                mainView.unsavedChanges = true;
        }
        ReFlatten();
    }
}