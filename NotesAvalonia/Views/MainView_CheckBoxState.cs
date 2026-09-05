using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public partial class MainView : UserControl
{
    // Primary click on a note's checkbox: open <-> done. Clicking a canceled note reopens it.
    // (The CheckBox itself only displays the state - IsChecked mirrors Done - so the click decides
    // what the state means instead of relying on the built-in two-state toggle.)
    private void CheckBox_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: FlattenedNoteViewModel nvm } checkBox)
        {
            nvm.ToggleDone();

            // The platform CheckBox toggles its own local IsChecked before raising Click. Under
            // the one-way binding that local value survives whenever the source did not change
            // (e.g. clicking a canceled note clears Canceled but leaves Done=false, so no update
            // is pushed) and the box would show a stray checkmark. Re-assert the visual so it
            // always mirrors the actual state.
            checkBox.IsChecked = nvm.Done;
        }
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
