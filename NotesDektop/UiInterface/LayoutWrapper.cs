using Notes.Interface.UiController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Notes.Desktop.UiInterface
{
    public class LayoutWrapper : IUiLayout
    {
        public readonly Panel Layout;

        /// <summary>
        /// Creates a new wrapper object
        /// </summary>
        /// <param name="layout">Should be a panel, but is casted automatically for convenience</param>
        public LayoutWrapper(Control layout)
        {
            this.Layout = (Panel)layout;
        }

        // Interface impls
        public int ChildCount() => Layout.Controls.Count;
        public int IndexOfChild(IUiLayout child) => Layout.Controls.IndexOf(((LayoutWrapper)child).Layout);
        public void RemoveSelf() => ((Panel)this.Layout.Parent).Controls.Remove(this.Layout);

        // To make the dictionary work as expected
        public override int GetHashCode() => Layout.GetHashCode();
        public override bool Equals(object obj) => obj.GetHashCode() == Layout.GetHashCode();

    }
}
