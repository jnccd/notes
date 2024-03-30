using Notes.Interface.UiController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Java.Text.Normalizer;

namespace NotesAndroid.UiInterface
{
    public class ActivityWrapper(MainActivity activity) : IUiWindow
    {
        public readonly MainActivity Activity = activity;

        public void Relayout() => Activity.Relayout();

        // To make the dictionary work as expected
        public override int GetHashCode() => Activity.GetHashCode();
        public override bool Equals(object? obj) => obj?.GetHashCode() == Activity.GetHashCode();
    }
}
