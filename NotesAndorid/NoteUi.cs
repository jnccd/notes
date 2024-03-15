using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Notes.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using static Android.Provider.Settings;
using static Android.Webkit.WebStorage;

namespace NotesAndroid
{
    public class NoteUi
    {
        public Note Note;
        public List<NoteUi> SubNotes = new List<NoteUi>();
        public ViewGroup UiPanel;
        private ViewGroup rootPanel;
        private Activity parentActivity;

        readonly int depth;
        public readonly NoteUi Parent;
        public static readonly Dictionary<ViewGroup, NoteUi> UiToNote = new Dictionary<ViewGroup, NoteUi>();

        private bool expanded;
        float expandButtonAnimTime;

        const float fontSize = 10f;
        const int treeDepthPadding = 20;

        /// <summary>
        /// Creates new NoteUi instance
        /// </summary>
        /// <param name="note"></param>
        /// <param name="rootPanel"></param>
        /// <param name="depth"></param>
        /// <param name="parent"></param>
        public NoteUi(Note note, Activity parentActivity, int depth = 0, NoteUi parent = null, int index = -1)
        {
            Note = note;
            this.depth = depth;
            Parent = parent;
            this.parentActivity = parentActivity;
            expanded = depth <= 0;

            CreateUi(parentActivity, depth, index);

            foreach (Note subNote in note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, parentActivity, depth + 1, this, index >= 0 ? index + 1 : index));
        }
        /// <summary>
        /// Constructor for empty root NoteUi Node
        /// </summary>
        /// <param name="subNotes"></param>
        /// <param name="subNotesPanel"></param>
        public NoteUi(List<Note> subNotes, Activity parentActivity)
        {
            Note = new Note
            {
                SubNotes = subNotes
            };
            this.depth = 0;
            Parent = null;
            this.parentActivity = parentActivity;
            expanded = true;

            foreach (Note subNote in Note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, parentActivity, depth + 1, this));
        }

        void CreateUi(Activity parentActivity, int depth = 0, int index = -1)
        {
            bool enabled = true;

            var parent = parentActivity.FindViewById<LinearLayout>(Resource.Id.noteLinearLayout);
            var newNoteLayout = parentActivity.LayoutInflater.Inflate(Resource.Layout.notebox, null);
            newNoteLayout.SetPadding(parentActivity.Dip2px(30) * depth, parentActivity.Dip2px(7), 0, 0);
            if (index < 0)
                index = parent.ChildCount;
            parent.AddView(newNoteLayout, index);

            var note = newNoteLayout.FindViewById<EditText>(Resource.Id.note);
            note.Enabled = enabled;
            note.Text = Note.Text;
            note.TextChanged += Note_TextChanged;

            var checkBox = newNoteLayout.FindViewById<CheckBox>(Resource.Id.noteDone);
            checkBox.Enabled = enabled;
            checkBox.Checked = Note.Done;
            checkBox.CheckedChange += OnNoteDone;
        }

        private void OnNoteDone(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            
        }

        private void Note_TextChanged(object sender, Android.Text.TextChangedEventArgs e)
        {
            
        }

        public void ToggleExpand()
        {
            if (SubNotes.Count == 0)
                AddSubNoteAt(new Note(), parentActivity, 0);

            expanded = !expanded;
        }
        bool AreAllParentsExpanded()
        {
            if (Parent == null)
                return true;
            else if (!Parent.expanded)
                return false;
            else
                return Parent.AreAllParentsExpanded();
        }
        public List<NoteUi> GetAllChildren()
        {
            var re = SubNotes.ToList(); // List to List to make a shallow copy
            foreach (var child in SubNotes)
                re.AddRange(child.GetAllChildren());
            return re;
        }

        public NoteUi AddSubNoteAt(Note note, Activity parentActivity, int index, int rootPanelIndex = -1)
        {
            if (rootPanelIndex == -1)
                rootPanelIndex = (UiPanel == null ? 0 : rootPanel.IndexOfChild(UiPanel)) + 1 + index;
            var newNoteUi = new NoteUi(note, parentActivity, depth + 1, this, rootPanelIndex);
            Note.SubNotes.Insert(index, note);
            SubNotes.Insert(index, newNoteUi);

            return newNoteUi;
        }
        public void RemoveSubNoteAt(int index)
        {
            RemoveSubNoteUi(SubNotes[index]);

            Note.SubNotes.RemoveAt(index);
            UiToNote.Remove(SubNotes[index].UiPanel);
            SubNotes.RemoveAt(index);
        }
        public void RemoveSubNote(NoteUi subNote)
        {
            RemoveSubNoteUi(subNote);

            Note.SubNotes.Remove(subNote.Note);
            UiToNote.Remove(subNote.UiPanel);
            SubNotes.Remove(subNote);
        }
        private void RemoveSubNoteUi(NoteUi subNote, int depth = 0)
        {
            foreach (var subSubNote in subNote.SubNotes)
                subNote.RemoveSubNoteUi(subSubNote, depth + 1);

            rootPanel.RemoveView(subNote.UiPanel);
        }
    }
}