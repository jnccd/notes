using Notes.Desktop;
using Notes.Interface.UiController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notes.Desktop.UiInterface
{
    public class FormWrapper : IUiWindow
    {
        public readonly MainForm Form;

        public FormWrapper(MainForm form)
        {
            this.Form = form;
        }

        public void Relayout() => Form.LayoutNotePanels();

        // To make the dictionary work as expected
        public override int GetHashCode() => Form.GetHashCode();
        public override bool Equals(object obj) => obj.GetHashCode() == Form.GetHashCode();
    }
}
