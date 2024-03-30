using Android.Views;
using Notes.Interface.UiController;

namespace NotesAndroid.UiInterface
{
    public class LayoutWrapper(ViewGroup layout) : IUiLayout
    {
        public readonly ViewGroup Layout = layout;

        public int ChildCount() => layout.ChildCount;
        public int IndexOfChild(IUiLayout child) => Layout.IndexOfChild(((LayoutWrapper)child).Layout);
        public void RemoveSelf() => ((ViewGroup)Layout.Parent).RemoveView(Layout);

        // To make the dictionary work as expected
        public override int GetHashCode() => Layout.GetHashCode();
        public override bool Equals(object obj) => obj.GetHashCode() == Layout.GetHashCode();
    }
}
