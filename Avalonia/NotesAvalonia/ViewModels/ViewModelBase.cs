using CommunityToolkit.Mvvm.ComponentModel;
using NotesAvalonia.Views;

namespace NotesAvalonia.ViewModels;

/*   NOTE:
 *
 *   Please mind that this sample uses the CommunityToolkit.Mvvm package for the ViewModel. Feel free to use any other
 *   MVVM-Framework (like ReactiveUI or Prism) that suits your needs best.
 */

/// <summary>
/// A base class for all of our ViewModels.
/// </summary>
public class ViewModelBase : ObservableObject
{
    internal static MainView? mainView = null;
    public static MainView? MainView
    {
        get => mainView;
        set => mainView ??= value;
    }
}
