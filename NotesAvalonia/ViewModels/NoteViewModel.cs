using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using Notes.Interface;

namespace NotesAvalonia.ViewModels;

/// <summary>
/// This is a ViewModel which represents a <see cref="Models.ToDoItem"/>
/// </summary>
public partial class NoteViewModel : ViewModelBase
{
    public NoteViewModel(Note item)
    {
        BaseNote = item;

        Done = item.Done;
        Expanded = item.Expanded;
        Text = item.Text;

        SubNotes = new ObservableCollection<NoteViewModel>(
            item.SubNotes.Select(n => new NoteViewModel(n))
        );
    }

    public Note BaseNote { get; set; }
    public ObservableCollection<NoteViewModel> SubNotes { get; }

    // NOTE: This property is made without source generator. Uncomment the line below to use the source generator
    // [ObservableProperty] 
    private bool _done;
    public bool Done
    {
        get { return _done; }
        set
        {
            SetProperty(ref _done, value);
            BaseNote.Done = value;
        }
    }

    private bool _expanded;
    public bool Expanded
    {
        get { return _expanded; }
        set
        {
            SetProperty(ref _expanded, value);
            BaseNote.Expanded = value;
        }
    }

    [ObservableProperty]
    private string? _text;
}

public partial class FlattenedNoteViewModel : ViewModelBase
{
    public FlattenedNoteViewModel(FlattenedNote item)
    {
        FlattenedNote = item;
    }

    public FlattenedNote FlattenedNote { get; set; }

    public uint Depth => FlattenedNote.Depth;

    public string NumRecursiveTodoChildren
    {
        get
        {
            var children = FlattenedNote.OriginalNote.RecursiveSubNotes().Where(x => x.Item2 != FlattenedNote.OriginalNote && !string.IsNullOrWhiteSpace(x.Item2.Text));
            var undoneChildren = children.Where(x => !x.Item2.Done);

            var childCount = children.Count();
            var undoneChildCount = undoneChildren.Count();

            if (childCount <= 0 || FlattenedNote.OriginalNote.Expanded)
                return "";
            if (undoneChildCount >= 10)
                return "✹";
            return undoneChildCount.ToString();
        }
    }

    private bool _done;
    public bool Done
    {
        get { return FlattenedNote.OriginalNote.Done; }
        set
        {
            FlattenedNote.OriginalNote.Done = value;
            if (mainView != null)
                mainView.unsavedChanges = true;
            SetProperty(ref _done, value);
        }
    }

    private bool _expanded;
    public bool Expanded
    {
        get { return FlattenedNote.OriginalNote.Expanded; }
        set
        {
            FlattenedNote.OriginalNote.Expanded = value;
            if (mainView != null)
                mainView.unsavedChanges = true;
            SetProperty(ref _expanded, value);
        }
    }

    private string _text = "";
    public string Text
    {
        get { return FlattenedNote.OriginalNote.Text ?? ""; }
        set
        {
            FlattenedNote.OriginalNote.Text = value;
            if (mainView != null)
                mainView.unsavedChanges = true;
            SetProperty(ref _text, value);
        }
    }
}