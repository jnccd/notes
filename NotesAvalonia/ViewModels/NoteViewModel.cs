using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Notes.Interface.DTO;
using NotesAvalonia.Configuration;

namespace NotesAvalonia.ViewModels;

/// <summary>
/// This is a ViewModel which represents a <see cref="Models.ToDoItem"/>
/// </summary>
public partial class NoteViewModel : ViewModelBase
{
    public NoteViewModel(Note item)
    {
        BaseNote = item;

        Done = item.Data.Done;
        Expanded = item.Data.Expanded;
        Text = item.Data.DecodedText;

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
            BaseNote.Data.Done = value;
        }
    }

    private bool _expanded;
    public bool Expanded
    {
        get { return _expanded; }
        set
        {
            SetProperty(ref _expanded, value);
            BaseNote.Data.Expanded = value;
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
            var children = FlattenedNote.OriginalNote.RecursiveSubNotes().Where(x => x.Note != FlattenedNote.OriginalNote && !string.IsNullOrWhiteSpace(x.Note.Data.DecodedText));
            var undoneChildren = children.Where(x => !x.Note.Data.Done);

            var childCount = children.Count();
            var undoneChildCount = undoneChildren.Count();

            if (childCount <= 0 || FlattenedNote.OriginalNote.Data.Expanded)
                return "";
            if (undoneChildCount >= 10)
                return "✹";
            return undoneChildCount.ToString();
        }
    }

    private bool _done;
    public bool Done
    {
        get { return FlattenedNote.OriginalNote.Data.Done; }
        set
        {
            FlattenedNote.OriginalNote.Data.Done = value;
            if (mainView != null)
                Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = FlattenedNote.OriginalNote.Id,
                    Data = FlattenedNote.OriginalNote.Data
                });
            SetProperty(ref _done, value);
        }
    }

    private bool _hidden;
    public bool Hidden
    {
        get { return FlattenedNote.OriginalNote.Data.Hidden; }
        set
        {
            FlattenedNote.OriginalNote.Data.Hidden = value;
            if (mainView != null)
                Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = FlattenedNote.OriginalNote.Id,
                    Data = FlattenedNote.OriginalNote.Data
                });
            SetProperty(ref _hidden, value);
        }
    }
    [ObservableProperty]
    private bool _notTemporarilyUnHidden = true;

    private bool _expanded;
    public bool Expanded
    {
        get { return FlattenedNote.OriginalNote.Data.Expanded; }
        set
        {
            FlattenedNote.OriginalNote.Data.Expanded = value;
            if (mainView != null)
                Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = FlattenedNote.OriginalNote.Id,
                    Data = FlattenedNote.OriginalNote.Data
                });
            SetProperty(ref _expanded, value);
        }
    }

    private string _text = "";
    public string Text
    {
        get { return FlattenedNote.OriginalNote.Data.DecodedText ?? ""; }
        set
        {
            FlattenedNote.OriginalNote.Data.DecodedText = value;
            if (mainView != null)
                Config.Data.CurrentUsersUnsyncedChanges?.Add(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = FlattenedNote.OriginalNote.Id,
                    Data = FlattenedNote.OriginalNote.Data
                });
            SetProperty(ref _text, value);
        }
    }
}