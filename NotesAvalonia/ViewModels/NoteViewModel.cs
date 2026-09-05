using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
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

    /// <summary>True when this row is a symlink to another note.</summary>
    public bool IsLink => FlattenedNote.OriginalNote.Data.LinkTargetId != null;

    /// <summary>
    /// The note whose content this row displays and edits. For symlink rows that is the resolved
    /// target (set during flatten); for regular notes it is the note itself. Structural operations
    /// (move/delete/insert sibling) still use <see cref="FlattenedNote"/>.OriginalNote - the link
    /// that lives in this position.
    /// </summary>
    public Note EffectiveNote => FlattenedNote.DereferencedNote ?? FlattenedNote.OriginalNote;

    // Text box left padding grows on symlink rows to make room for the 🔗 marker.
    public Thickness TextPadding => IsLink ? new Thickness(20, 5, 2, 5) : new Thickness(2, 5);

    // The same canonical note can be displayed in several rows at once (a note under an expanded
    // symlink and its own place in the backlog). Data changes are shared, but each row has its own
    // bindings - so after editing content, tell every other row showing the same note to re-read
    // its values. (Edits through a link must show up on the canonical row and vice versa.)
    void NotifySharedContentRows()
    {
        if (mainView?.DataContext is not MainViewModel model)
            return;
        var effective = EffectiveNote;
        foreach (var fnvm in model.FlattenedNoteVMs)
        {
            if (ReferenceEquals(fnvm, this))
                continue;
            if (fnvm.EffectiveNote == effective)
                fnvm.OnPropertyChanged(string.Empty); // refresh all bindings of the shared note
        }
    }

    public string NumRecursiveTodoChildren
    {
        get
        {
            var effectiveNote = EffectiveNote;
            var children = effectiveNote.RecursiveSubNotes().Where(x => x.Note != effectiveNote && !string.IsNullOrWhiteSpace(x.Note.Data.DecodedText));
            var undoneChildren = children.Where(x => !x.Note.Data.Done && !x.Note.Data.Canceled);

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
        get { return EffectiveNote.Data.Done; }
        set
        {
            EffectiveNote.Data.Done = value;
            if (mainView != null)
                Config.Data.AddNoteChange(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = EffectiveNote.Id,
                    Data = EffectiveNote.Data
                });
            SetProperty(ref _done, value);
            OnPropertyChanged(nameof(Closed));
            OnPropertyChanged(nameof(ShowCanceledGlyph));
            NotifySharedContentRows();
        }
    }

    private bool _canceled;
    public bool Canceled
    {
        get { return EffectiveNote.Data.Canceled; }
        set
        {
            EffectiveNote.Data.Canceled = value;
            if (mainView != null)
                Config.Data.AddNoteChange(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = EffectiveNote.Id,
                    Data = EffectiveNote.Data
                });
            SetProperty(ref _canceled, value);
            OnPropertyChanged(nameof(Closed));
            OnPropertyChanged(nameof(ShowCanceledGlyph));
            NotifySharedContentRows();
        }
    }

    // Canceled should behave exactly like Done everywhere in the UI (crossed through text, no
    // editing, ...), so bind the Done-based styling to this combined flag instead.
    public bool Closed => Done || Canceled;

    // Show the custom canceled checkbox (gray box with a dash) instead of the platform checkbox.
    // Done wins if both flags are somehow set, so never show the gray box over the done check.
    public bool ShowCanceledGlyph => Canceled && !Done;

    /// <summary>Primary click on the checkbox: open &lt;-&gt; done; clicking a canceled note reopens it.</summary>
    public void ToggleDone()
    {
        if (Done)
            Done = false;
        else if (Canceled)
            Canceled = false;
        else
            Done = true;
    }

    /// <summary>Middle click / menu: toggle canceled. Done takes precedence while both are set,
    /// so canceling a done note clears done first to make the canceled state visible.</summary>
    public void ToggleCanceled()
    {
        if (Canceled)
            Canceled = false;
        else
        {
            Done = false;
            Canceled = true;
        }
    }

    // Info-menu display: when this note was created. New notes are stamped at creation
    // (Note.EmptyNote), old notes without the value get backfilled on load (MainViewModel.LoadNew).
    public string CreatedInfo
    {
        get
        {
            var created = EffectiveNote.Data.Created;
            return $"Created: {(created.HasValue ? created.Value.ToString("yyyy-MM-dd HH:mm") : "—")}";
        }
    }

    private bool _hidden;
    public bool Hidden
    {
        get { return EffectiveNote.Data.Hidden; }
        set
        {
            EffectiveNote.Data.Hidden = value;
            if (mainView != null)
                Config.Data.AddNoteChange(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = EffectiveNote.Id,
                    Data = EffectiveNote.Data
                });
            SetProperty(ref _hidden, value);
            NotifySharedContentRows();
        }
    }
    [ObservableProperty]
    private bool _notTemporarilyUnHidden = true;

    // Expansion state stays on the row's own note (per-link for symlinks, shared for the note
    // itself), so it is NOT routed through EffectiveNote.
    private bool _expanded;
    public bool Expanded
    {
        get { return FlattenedNote.OriginalNote.Data.Expanded; }
        set
        {
            FlattenedNote.OriginalNote.Data.Expanded = value;
            if (mainView != null)
                Config.Data.AddNoteChange(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = FlattenedNote.OriginalNote.Id,
                    Data = FlattenedNote.OriginalNote.Data
                });
            SetProperty(ref _expanded, value);
            NotifySharedContentRows();
        }
    }

    private string _text = "";
    public string Text
    {
        get { return EffectiveNote.Data.DecodedText ?? ""; }
        set
        {
            EffectiveNote.Data.DecodedText = value;
            if (mainView != null)
                Config.Data.AddNoteChange(new NoteChange()
                {
                    Type = NoteChangeType.Update,
                    NoteId = EffectiveNote.Id,
                    Data = EffectiveNote.Data
                });
            SetProperty(ref _text, value);
            NotifySharedContentRows();
        }
    }
}