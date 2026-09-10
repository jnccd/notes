using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

// Keeps the note TextBox the user is typing in focused, and keeps its caret where the user left it.
//
// The problem: a soft line wrap grows the row, the list re-measures, and VirtualizingStackPanel can
// recycle/re-realize that row's container. The focused TextBox is torn down with it, so focus is lost
// and the caret is reset - notably during the first wraps after a row was realized (startup, or after
// the row was scrolled out of view and back).
//
// Two layers deal with that:
//   * prevention - a generous realization cache, so the row being edited is not recycled;
//   * recovery  - the editing watchdog below re-asserts focus and caret after a layout pass that
//                  dropped them anyway.
//
// The TextBox height is deliberately NOT constrained (unlike the mobile freeze in OnNoteGotFocus),
// and the scroll position is deliberately never touched, so the user can scroll freely while writing.
//
// Caret sampling is the delicate part. TextChanged fires BEFORE Avalonia assigns the caret for that
// edit, and by the time a layout pass or a focus event runs, the box may already be recycled and
// reporting 0. So the caret is sampled from the box's own CaretIndex notifications (which fire after
// the caret is committed but while the box is still alive), plus one deferred sample posted above the
// render priority - after the input job, before the layout pass that can tear the row down.
public partial class MainView
{
    // A relayout-driven focus drop must not be confused with the user clicking somewhere else, so the
    // watchdog backs off for a moment after any pointer press.
    DateTime lastPointerPressedAt = DateTime.MinValue;

    Guid? editingNoteId;
    TextBox? editingTextBox;
    int editingCaret;
    bool editingCaretAtEnd;
    bool reassertScheduled;
    int reassertAttempts;

    // Wires up the focus/caret handling. Called once from the Loaded handler.
    void InitTextboxFocusFix()
    {
        // Remember what was last written when a note stops being edited (all platforms): a deferred
        // payload can lag the last keystrokes, and must not regress that content.
        this.AddHandler(InputElement.LostFocusEvent, OnNoteLostFocus, RoutingStrategies.Bubble);
        this.AddHandler(InputElement.GotFocusEvent, OnNoteGotFocus, RoutingStrategies.Bubble);

        if (!Globals.IsDesktop)
            return;

        this.AddHandler(TextBox.TextChangedEvent, OnNoteTextChanged, RoutingStrategies.Bubble);
        this.AddHandler(InputElement.KeyUpEvent, OnNoteKeyUp, RoutingStrategies.Bubble);
        this.AddHandler(InputElement.PointerReleasedEvent, OnNotePointerUp, RoutingStrategies.Bubble);

        bool panelCacheApplied = false;
        LayoutUpdated += (s, e) =>
        {
            if (!panelCacheApplied)
            {
                var panel = this.GetLogicalDescendants().OfType<VirtualizingStackPanel>().FirstOrDefault();
                if (panel != null)
                {
                    panel.CacheLength = 200;
                    panelCacheApplied = true;
                }
            }
            if (editingNoteId != null)
                ScheduleReassertEditing();
        };
    }

    // Called on every pointer press on the view (see MainView_PointerPressed).
    void NotifyPointerPressed() => lastPointerPressedAt = DateTime.Now;

    void OnNoteGotFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not TextBox { DataContext: FlattenedNoteViewModel nvm } textBox)
            return;

        // Mobile: freeze the editing height so a soft line wrap cannot relayout the row and make
        // Android drop the input connection.
        if (!Globals.IsDesktop && textBox.Bounds.Height > 0)
        {
            textBox.Tag = textBox.Bounds.Height;
            textBox.Height = textBox.Bounds.Height;
        }

        // Desktop: start/refresh the editing watchdog session.
        if (Globals.IsDesktop)
        {
            if (editingNoteId != nvm.EffectiveNote.Id)
            {
                // A different note: the caret starts from whatever that note has now - never the
                // previous note's position.
                editingNoteId = nvm.EffectiveNote.Id;
                editingCaret = 0;
                editingCaretAtEnd = false;
            }
            SubscribeEditableTextBox(textBox);
            RememberCaret(textBox);
            reassertAttempts = 0;
        }
    }

    void OnNoteLostFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not TextBox { DataContext: FlattenedNoteViewModel textBoxVm } textBox)
            return;

        // Restore auto-sizing (mobile freeze from OnNoteGotFocus).
        if (textBox.Tag is double frozenHeight)
        {
            textBox.Height = double.NaN;
            textBox.Tag = null;
        }

        // Capture the exact content/revision at the moment the user stopped editing so an older
        // payload cannot regress it (see ApplyReceivedPayload).
        lastEditedNoteId = nvmIdOf(textBox);
        lastEditedNoteData = textBoxVm is null ? null : CloneNoteData(textBoxVm.EffectiveNote.Data);

        if (!Globals.IsDesktop)
            return;

        // Desktop: distinguish a deliberate blur (clicked elsewhere / another field focused) from a
        // relayout-driven focus drop; only the latter is re-asserted by the watchdog.
        bool deliberateBlur = DateTime.Now - lastPointerPressedAt < TimeSpan.FromMilliseconds(400)
            || this.GetLogicalDescendants().OfType<TextBox>().Any(t => t.IsFocused);
        if (deliberateBlur)
        {
            EndEditingSession();
            return;
        }

        // The box may already be a recycled one here (reporting caret 0 for another note's text), so
        // the caret is only accepted when this really is the box of the note being edited.
        RememberCaret(textBox);
        ScheduleReassertEditing();
    }

    // Subscribes to the caret notifications of the box being edited; a recycled row hands us a new
    // instance, so the previous subscription is dropped first.
    void SubscribeEditableTextBox(TextBox textBox)
    {
        if (ReferenceEquals(editingTextBox, textBox))
            return;
        if (editingTextBox != null)
            editingTextBox.PropertyChanged -= OnEditingTextBoxPropertyChanged;
        editingTextBox = textBox;
        textBox.PropertyChanged += OnEditingTextBoxPropertyChanged;
    }

    void EndEditingSession()
    {
        if (editingTextBox != null)
        {
            editingTextBox.PropertyChanged -= OnEditingTextBoxPropertyChanged;
            editingTextBox = null;
        }
        editingNoteId = null;
    }

    // The caret moved on the box we are editing - this is the fresh position for the keystroke that
    // may have just caused a wrap-driven relayout.
    void OnEditingTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.CaretIndexProperty)
            return;
        if (sender is not TextBox { IsFocused: true } textBox)
            return;
        RememberCaret(textBox);
    }

    bool IsEditingNote(TextBox textBox) =>
        editingNoteId is Guid noteId
        && textBox.DataContext is FlattenedNoteViewModel vm
        && vm.EffectiveNote.Id == noteId;

    // A caret of 0 is what a box reports once it is torn down or freshly re-realized, so it is never
    // trusted; anything else is the user's real position. Whether the caret sat at the end of the text
    // is remembered too, so forward typing can follow the end as the text grows.
    void RememberCaret(TextBox textBox)
    {
        if (!IsEditingNote(textBox))
            return;
        int caret = textBox.CaretIndex;
        if (caret <= 0)
            return;
        editingCaret = caret;
        editingCaretAtEnd = caret >= (textBox.Text?.Length ?? 0);
    }

    TextBox? FindNoteTextBox(Guid noteId) =>
        this.GetLogicalDescendants().OfType<TextBox>()
            .FirstOrDefault(t => t.DataContext is FlattenedNoteViewModel vm &&
                (vm.EffectiveNote.Id == noteId || vm.FlattenedNote.OriginalNote.Id == noteId));

    // Secondary, cheap samples for caret moves that are not tied to typing (arrow keys, clicks).
    void TrackEditingCaret(object? source)
    {
        if (!Globals.IsDesktop)
            return;
        if (source is not TextBox { IsFocused: true } textBox)
            return;
        RememberCaret(textBox);
        reassertAttempts = 0;
    }

    void OnNoteKeyUp(object? sender, KeyEventArgs e) => TrackEditingCaret(e.Source);

    void OnNotePointerUp(object? sender, PointerReleasedEventArgs e) => TrackEditingCaret(e.Source);

    void OnNoteTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!Globals.IsDesktop || sender is not TextBox { DataContext: FlattenedNoteViewModel nvm } textBox)
            return;
        if (editingNoteId != nvm.EffectiveNote.Id)
            return; // not the note being edited (or no session)

        // TextChanged itself runs before Avalonia assigns the caret for this edit, and the layout pass
        // that can recycle the row runs after this job - so sample the caret in between, at a priority
        // above the render pass, where the new position is committed but the box is still alive.
        Dispatcher.UIThread.Post(() =>
        {
            if (textBox.IsFocused)
                RememberCaret(textBox);
        }, DispatcherPriority.Normal);

        reassertAttempts = 0;

        ScheduleReassertEditing();
    }

    void ScheduleReassertEditing()
    {
        if (reassertScheduled)
            return;
        reassertScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            reassertScheduled = false;
            ReassertEditingState();
        }, DispatcherPriority.Loaded);
    }

    // Makes sure the note being edited really has focus and the caret stays where the user is typing -
    // Avalonia drops both on the first height changes of a freshly realized row. The scroll position is
    // deliberately never touched so the user can scroll freely while writing.
    void ReassertEditingState()
    {
        if (!Globals.IsDesktop || editingNoteId is not Guid noteId)
            return;
        if (DateTime.Now - lastPointerPressedAt < TimeSpan.FromMilliseconds(400))
            return; // user is interacting (click/drag) - never fight that

        var target = FindNoteTextBox(noteId);
        if (target == null)
        {
            bool noteStillExists = (DataContext as MainViewModel)?.FlattenedNoteVMs
                .Any(vm => vm.EffectiveNote.Id == noteId) == true;
            if (!noteStillExists || reassertAttempts++ > 30)
                EndEditingSession(); // deleted or not materializing - stop watching
            return;
        }

        reassertAttempts = 0;

        if (target.IsFocused)
        {
            // Focus survived - only repair a caret that a recycle reset to 0.
            if (target.CaretIndex == 0 && editingCaret > 0)
                target.CaretIndex = DesiredCaret(target);
            SubscribeEditableTextBox(target);
            return;
        }

        if (this.GetLogicalDescendants().OfType<TextBox>()
                .Any(t => t.IsFocused && t.DataContext is FlattenedNoteViewModel vm && vm.EffectiveNote.Id != noteId))
            return; // another note is focused on purpose

        // Measure the control's own caret before focusing: focusing a text box that was just
        // re-realized resets it to 0, and it is the only caret this box still knows about.
        int caretBeforeFocus = target.CaretIndex;

        target.Focusable = true;
        target.Focus();

        // Restore the caret the user was typing at, then re-subscribe: focusing a recycled row hands us
        // a different TextBox instance than the one the session was tracking.
        int desired = DesiredCaret(target, caretBeforeFocus);
        if (desired > 0 && target.CaretIndex != desired)
            target.CaretIndex = desired;

        SubscribeEditableTextBox(target);
    }

    // Where the caret should be in `target`. "Was at the end" follows the end as the text grows, so
    // forward typing never lands behind the character the user just wrote; everything else is the last
    // position actually observed, clamped to the text.
    int DesiredCaret(TextBox target, int fallback = 0)
    {
        int length = target.Text?.Length ?? 0;
        if (editingCaretAtEnd)
            return length;
        int desired = editingCaret > 0 ? editingCaret : fallback;
        return Math.Min(desired, length);
    }

    static Guid? nvmIdOf(TextBox textBox) =>
        textBox.DataContext is FlattenedNoteViewModel vm ? vm.EffectiveNote.Id : null;
}
