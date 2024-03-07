﻿using Notes.Interface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Notes.Desktop
{
    public class NoteUi
    {
        public Note Note;
        public List<NoteUi> SubNotes = new();
        public Panel UiPanel;
        private Panel rootPanel;
        private MainForm parentForm;

        readonly int depth;
        public readonly NoteUi Parent;
        public static readonly Dictionary<Panel, NoteUi> UiToNote = new();

        public bool Shown = false;
        float expandButtonAnimTime;

        const float fontSize = 10f;
        const int treeDepthPadding = 20;

        /// <summary>
        /// Creates new NodeUi instance
        /// </summary>
        /// <param name="note"></param>
        /// <param name="rootPanel"></param>
        /// <param name="depth"></param>
        /// <param name="parent"></param>
        public NoteUi(Note note, MainForm mainForm, int depth = 0, NoteUi parent = null, int index = -1)
        {
            Note = note;
            this.depth = depth;
            Parent = parent;
            parentForm = mainForm;
            Shown = depth <= 1;

            CreateFormsUi(mainForm, index);

            foreach (Note subNote in note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, mainForm, depth + 1, this));
        }
        /// <summary>
        /// Constructor for empty root NoteUi Node
        /// </summary>
        /// <param name="subNotes"></param>
        /// <param name="subNotesPanel"></param>
        public NoteUi(List<Note> subNotes, MainForm mainForm)
        {
            Note = new Note
            {
                SubNotes = subNotes
            };
            this.depth = 0;
            Parent = null;
            parentForm = mainForm;
            Shown = true;

            foreach (Note subNote in Note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, mainForm, depth + 1, this));
        }

        void CreateFormsUi(MainForm mainForm, int index = -1)
        {
            rootPanel = (Panel)mainForm.Controls.Find("rootPanel", true).First();

            Panel mainNotePanel = new()
            {
                Name = "mainNotePanel",
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = rootPanel.BackColor,
                Width = rootPanel.Width,
                Height = Globals.defaultPanelHeight,
                Location = new Point((depth - 1) * treeDepthPadding, 0),
                //AutoSize = true,
                //WrapContents = false,
                //FlowDirection = FlowDirection.LeftToRight,
            };
            UiPanel = mainNotePanel;
            UiToNote.Add(mainNotePanel, this);

            Label orderButton = new()
            {
                Name = "orderButton",
                Text = "⠿",
                BackColor = rootPanel.BackColor,
                ForeColor = Color.FromArgb(30, 30, 30),
                Location = new Point(0, 3),
                Width = 14,
                Height = mainNotePanel.Height
            };
            orderButton.MouseDown += mainForm.OrderButton_MouseDown;
            orderButton.MouseMove += mainForm.OrderButton_MouseMove;
            orderButton.MouseUp += mainForm.OrderButton_MouseUp;
            orderButton.Font = new Font(orderButton.Font.FontFamily, fontSize);
            mainNotePanel.Controls.Add(orderButton);

            Label expandButton = new()
            {
                Name = "expandButton",
                Text = "🞂",
                BackColor = rootPanel.BackColor,
                ForeColor = Color.FromArgb(30, 30, 30),
                Location = new Point(0, 3),
                Width = 14,
                Height = mainNotePanel.Height,
            };
            expandButton.Font = new Font(expandButton.Font.FontFamily, fontSize);
            expandButton.Click += mainForm.ExpandButton_Click;
            //expandButton.Paint += OnExpandButtonPaint;
            mainNotePanel.Controls.Add(expandButton);

            CheckBox doneCheckBox = new()
            {
                Name = "doneCheckBox",
                //BackColor = rootPanel.BackColor,
                Width = 18,
                Height = mainNotePanel.Height,
                Checked = Note.Done,
            };
            doneCheckBox.CheckedChanged += mainForm.DoneCheckBox_CheckedChanged;
            doneCheckBox.Location = new Point(0, 0);
            mainNotePanel.Controls.Add(doneCheckBox);

            TextBox noteTextBox = new()
            {
                Name = "noteTextBox",
                BackColor = rootPanel.BackColor,
                BorderStyle = BorderStyle.None,
                Location = new Point(0, 3),
                Width = rootPanel.Width - mainNotePanel.Padding.Left,
                Height = mainNotePanel.Height,
                Text = Note.Text,
            };
            noteTextBox.KeyDown += mainForm.NoteTextBox_KeyDown;
            noteTextBox.Font = new Font(noteTextBox.Font.FontFamily, fontSize);
            mainNotePanel.Controls.Add(noteTextBox);

            rootPanel.Controls.Add(mainNotePanel);
            if (index >= 0)
                rootPanel.Controls.SetChildIndex(mainNotePanel, index);
        }
        public void UpdatePanelHeight()
        {
            if (!Shown)
                UiPanel.Height = 0;
            else
            {
                var textBox = (TextBox)UiPanel.Controls.Find("noteTextBox", true)[0];
                int textboxLines = textBox.PreferredSize.Width / textBox.Width + 1;
                if (textboxLines <= 0)
                    textboxLines = 1;
                textBox.Multiline = textboxLines > 1;
                textBox.Height = textboxLines * (textBox.Font.Height - 2) + 5;

                UiPanel.Height = textBox.Height + Globals.uiPadding;
            }
        }

        private void OnExpandButtonPaint(object sender, PaintEventArgs e)
        {
            int polySize = (int)(Math.Min(((Control)sender).Width, ((Control)sender).Height) * 0.7);
            int xPos = ((Control)sender).Width / 2 - polySize / 2;
            int yPos = ((Control)sender).Height / 2 - polySize / 2;
            Vector2 polyTranslation = new(xPos, yPos);
            Vector2[] poly = new Vector2[] {
                new(xPos, yPos),
                new(xPos + polySize, yPos + polySize / 2),
                new(xPos, yPos + polySize) };
            Matrix3x2 rotMat = Matrix3x2.CreateRotation(expandButtonAnimTime * (float)Math.PI * 2 / Globals.maxExpandAnimTime,
                new Vector2(xPos + polySize, yPos + polySize));

            e.Graphics.FillPolygon(Globals.expandButtonBrush,
                poly.Select(x =>
                    rotMat.Multiply(x).
                           ToPoint()).
                    ToArray());
        }

        public NoteUi AddSubNoteAt(Note note, MainForm mainForm, int index)
        {
            var newNoteUi = new NoteUi(note, mainForm, depth + 1, this, rootPanel.Controls.IndexOf(UiPanel) + 1 + index);
            SubNotes.Insert(index, newNoteUi);
            parentForm.LayoutNotePanels();

            return newNoteUi;
        }
        public void RemoveSubNoteAt(int index)
        {
            SubNotes[index].UiPanel.Parent.Controls.RemoveAt(index);
            UiToNote.Remove(SubNotes[index].UiPanel);
            SubNotes.RemoveAt(index);
            parentForm.LayoutNotePanels();
        }
    }
}
