using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public partial class MainView : UserControl
{
    // Primary click on a note's checkbox: open <-> done. Clicking a canceled note reopens it.
    // (The CheckBox itself only displays the state - CheckedState - so the click decides what
    // the state means instead of relying on the built-in two-state toggle.)
    private void CheckBox_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: FlattenedNoteViewModel nvm })
            nvm.ToggleDone();
    }

    // Desktop middle click on a note's checkbox: toggle canceled. (On mobile the same action is
    // available through the note's "Toggle Canceled" context menu item.)
    private void CheckBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Globals.IsDesktop && e.Properties.IsMiddleButtonPressed
            && sender is CheckBox { DataContext: FlattenedNoteViewModel nvm })
        {
            e.Handled = true;
            nvm.ToggleCanceled();
        }
    }
}
