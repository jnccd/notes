using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
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
    MainView? mainView = null;
    public MainView? MainView
    {
        get => mainView;
        set => mainView ??= value;
    }

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

    public ObservableCollection<FlattenedNoteViewModel> FlattenedNotes { get; } = new();

    [ObservableProperty]
    private CommsState _connectionState = CommsState.Disconnected;
    [ObservableProperty]
    private string _debugText = "";
    public void AddDebugText(string text)
    {
        if (Globals.RunConfig == "Release")
            return;
        DebugText = DebugText
            .Split('\n')
            .Append(text)
            .TakeLast(Globals.IsDesktop ? 1 : 16)
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

        FlattenedNotes.Remove(toDeleteFlattenedNote);
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