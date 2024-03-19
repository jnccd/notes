﻿using Notes.Interface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace NotesInterface.UiController
{
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
        private readonly Func<IUiWindow, int, IUiLayout> createUiNoteElement;

        // Expansion
        public bool Expanded { private set; get; }
        public bool Shown
        {
            get
            {
                return Parent == null || (Parent.Expanded && Parent.Shown);
            }
        }

        /// <summary>
        /// Creates new NoteUi instance
        /// </summary>
        public NoteUi(
            Note note,
            IUiWindow parentWindow,
            IUiLayout rootLayout,
            Func<IUiWindow, int, IUiLayout> CreateUiNoteElement,
            int depth = 0,
            NoteUi? parent = null,
            int index = -1)
        {
            Note = note;
            this.depth = depth;
            Parent = parent;
            this.parentWindow = parentWindow;
            this.rootLayout = rootLayout;
            Expanded = depth <= 0;

            createUiNoteElement = CreateUiNoteElement;

            UiLayout = CreateUiNoteElement(parentWindow, index);

            foreach (Note subNote in note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, parentWindow, rootLayout, CreateUiNoteElement, depth + 1, this, index >= 0 ? index + 1 : index));
        }
        /// <summary>
        /// Constructor for empty root NoteUi Node
        /// </summary>
        public NoteUi(
            List<Note> subNotes,
            IUiWindow parentWindow,
            IUiLayout rootLayout,
            Func<IUiWindow, int, IUiLayout> CreateUiNoteElement)
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
            Expanded = true;

            createUiNoteElement = CreateUiNoteElement;

            foreach (Note subNote in Note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, parentWindow, rootLayout, CreateUiNoteElement, depth + 1, this));
        }

        public void ToggleExpand()
        {
            Expanded = !Expanded;

            if (Expanded && SubNotes.Count == 0)
                AddSubNoteBefore(new Note(), parentWindow, rootLayout, createUiNoteElement, 0);
            else if (!Expanded && SubNotes.Count == 1 && string.IsNullOrWhiteSpace(SubNotes[0].Note.Text))
                RemoveSubNoteAt(0);

            parentWindow.Relayout();
        }
        bool AreAllParentsExpanded()
        {
            if (Parent == null)
                return true;
            else if (!Parent.Expanded)
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

        public NoteUi AddSubNoteBefore(
            Note note,
            IUiWindow parentWindow,
            IUiLayout rootLayout,
            Func<IUiWindow, int, IUiLayout> CreateUiNoteElement,
            int index)
        {
            var rootPanelIndex = SubNotes.Count == 0 ?
                rootLayout.IndexOfChild(UiLayout) + 1 :
                rootLayout.IndexOfChild(SubNotes[index].UiLayout);

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
