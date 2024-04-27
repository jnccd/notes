using Notes.Interface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Notes.Interface.UiController
{
    // Args: Window, Note, InsertionIndex, Depth -> Layout
    using CreateNoteUiElementFunc = Func<IUiWindow, Note, int, int, IUiLayout>;

    /// <summary>
    /// Acts as a connector between the Note object and its UI elements
    /// </summary>
    public class NoteUi
    {
        // Represented Note object
        public Note Note;
        public List<NoteUi> SubNotes = new();

        // NoteUi Tree
        public readonly NoteUi? Parent;
        readonly int depth;

        // Ui Interfaces
        private readonly IUiWindow parentWindow;
        private readonly IUiLayout rootLayout;
        public IUiLayout UiLayout;
        public static readonly Dictionary<IUiLayout, NoteUi> UiToNote = new();

        // Events
        private readonly CreateNoteUiElementFunc createNoteUiElement;

        // Additional Variables
        public readonly Dictionary<string, object> UiProperties = new();

        // Expansion
        public bool Shown
        {
            get
            {
                return Parent == null || (Parent.Note.Expanded && Parent.Shown);
            }
        }

        /// <summary>
        /// Creates new NoteUi instance
        /// </summary>
        public NoteUi(
            Note note,
            IUiWindow parentWindow,
            IUiLayout rootLayout,
            CreateNoteUiElementFunc CreateNoteUiElement,
            int depth = 0,
            NoteUi? parent = null,
            int index = -1)
        {
            Note = note;
            this.depth = depth;
            Parent = parent;
            this.parentWindow = parentWindow;
            this.rootLayout = rootLayout;

            createNoteUiElement = CreateNoteUiElement;

            UiLayout = CreateNoteUiElement(parentWindow, note, index, depth);
            UiToNote.Add(UiLayout, this);

            foreach (Note subNote in note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, parentWindow, rootLayout, CreateNoteUiElement, depth + 1, this, index >= 0 ? ++index : index));
        }
        /// <summary>
        /// Constructor for empty root NoteUi Node
        /// </summary>
        public NoteUi(
            List<Note> subNotes,
            IUiWindow parentWindow,
            IUiLayout rootLayout,
            CreateNoteUiElementFunc CreateNoteUiElement)
        {
            Note = new Note
            {
                SubNotes = subNotes
            };
            this.depth = 0;
            Parent = null;
            this.parentWindow = parentWindow;
            this.rootLayout = rootLayout;
            this.UiLayout = rootLayout;
            Note.Expanded = true;

            createNoteUiElement = CreateNoteUiElement;

            foreach (Note subNote in Note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, parentWindow, rootLayout, CreateNoteUiElement, depth + 1, this));
        }

        public void ToggleExpand()
        {
            Note.Expanded = !Note.Expanded;

            if (Note.Expanded && SubNotes.Count == 0)
                AddSubNoteBefore(new Note(), parentWindow, rootLayout, createNoteUiElement, 0);
            else if (!Note.Expanded && SubNotes.Count == 1 && string.IsNullOrWhiteSpace(SubNotes[0].Note.Text))
                RemoveSubNoteAt(0);

            parentWindow.Relayout();
        }
        public bool AreAllParentsExpanded()
        {
            if (Parent == null)
                return true;
            else if (!Parent.Note.Expanded)
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
        public List<NoteUi> FlattenNotesInUiOrder()
        {
            var re = new List<NoteUi> { this };
            foreach (var child in SubNotes)
                re.AddRange(child.FlattenNotesInUiOrder());
            return re;
        }
        public NoteUi GetRootNoteUi()
        {
            if (Parent == null)
                return this;
            else
                return Parent.GetRootNoteUi();
        }

        public NoteUi AddSubNoteBefore(
            Note note,
            IUiWindow parentWindow,
            IUiLayout rootLayout,
            CreateNoteUiElementFunc CreateUiNoteElement,
            int index)
        {
            var rootPanelIndex = index >= SubNotes.Count ?
                GetRootNoteUi().FlattenNotesInUiOrder().IndexOf(this) + SubNotes.Count :
                GetRootNoteUi().FlattenNotesInUiOrder().IndexOf(SubNotes[index]) - 1;

            var newNoteUi = new NoteUi(note, parentWindow, rootLayout, CreateUiNoteElement, depth + 1, this, rootPanelIndex);
            Note.SubNotes.Insert(index, note);
            SubNotes.Insert(index, newNoteUi);

            parentWindow.Relayout();

            return newNoteUi;
        }
        public void RemoveSubNoteAt(int index) => RemoveSubNote(SubNotes[index]);
        public void RemoveSubNote(NoteUi subNote)
        {
            RemoveSubNoteUi(subNote);

            Note.SubNotes.Remove(subNote.Note);
            UiToNote.Remove(subNote.UiLayout);
            SubNotes.Remove(subNote);

            parentWindow.Relayout();
        }
        private static void RemoveSubNoteUi(NoteUi subNote, int depth = 0)
        {
            foreach (var subSubNote in subNote.SubNotes)
                RemoveSubNoteUi(subSubNote, depth + 1);

            subNote.UiLayout.RemoveSelf();
        }
    }
}
