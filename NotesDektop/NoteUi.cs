using Notes.Interface;
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
        public List<NoteUi> SubNotes;
        public Panel UiPanel;
        public FlowLayoutPanel SubNotesPanel;
        MainForm mainForm;

        readonly int depth;
        public readonly NoteUi Parent;
        public readonly Dictionary<Panel, NoteUi> UiToNote;

        public bool Expanded { get; private set; }
        float expandButtonAnimTime;

        /// <summary>
        /// Creates new NodeUi instance
        /// </summary>
        /// <param name="note"></param>
        /// <param name="rootPanel"></param>
        /// <param name="depth"></param>
        /// <param name="parent"></param>
        public NoteUi(Note note, Panel rootPanel, int depth = 0, NoteUi parent = null)
        {
            Note = note;
            this.depth = depth;
            Parent = parent;

            CreateFormsUi(rootPanel);

            foreach (Note subNote in note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, SubNotesPanel, depth + 1, this));
        }
        /// <summary>
        /// Constructor for empty root NoteUi Node
        /// </summary>
        /// <param name="subNotes"></param>
        /// <param name="subNotesPanel"></param>
        public NoteUi(List<Note> subNotes, FlowLayoutPanel subNotesPanel)
        {
            Note = new Note
            {
                SubNotes = subNotes
            };
            this.depth = 0;
            Parent = null;

            SubNotesPanel = subNotesPanel;

            foreach (Note subNote in Note.SubNotes)
                SubNotes.Add(new NoteUi(subNote, SubNotesPanel, depth + 1, this));
        }

        void CreateFormsUi(Panel rootPanel, int index = -1)
        {
            mainForm = (MainForm)rootPanel.FindForm();

            UiPanel = new()
            {
                Name = "notePanel",
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = rootPanel.BackColor,
                Width = rootPanel.Width - 17,
                Height = Globals.defaultPanelHeight,
            };

            // --- Main Note Panel

            FlowLayoutPanel mainNotePanel = new()
            {
                Name = "mainNotePanel",
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = rootPanel.BackColor,
                Width = rootPanel.Width - 17,
                Height = Globals.defaultPanelHeight,
                Location = new Point(0, 0),
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
            };

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
            orderButton.Font = new Font(orderButton.Font.FontFamily, 10f);
            mainNotePanel.Controls.Add(orderButton);

            PictureBox expandButton = new()
            {
                Name = "expandButton",
                BackColor = rootPanel.BackColor,
                ForeColor = Color.FromArgb(30, 30, 30),
                Width = 14,
                Height = mainNotePanel.Height
            };
            expandButton.Font = new Font(expandButton.Font.FontFamily, 10f);
            expandButton.Click += mainForm.ExpandButton_Click;
            expandButton.Paint += OnExpandButtonPaint;
            //expandButton.Location = new Point(0, 0);
            mainNotePanel.Controls.Add(expandButton);

            CheckBox doneCheckBox = new()
            {
                Name = "doneCheckBox",
                BackColor = rootPanel.BackColor,
                Width = 18,
                Height = mainNotePanel.Height
            };
            doneCheckBox.CheckedChanged += mainForm.DoneCheckBox_CheckedChanged;
            doneCheckBox.Location = new Point(0, 0);
            mainNotePanel.Controls.Add(doneCheckBox);

            TextBox noteTextBox = new()
            {
                Name = "noteTextBox",
                BackColor = rootPanel.BackColor,
                BorderStyle = BorderStyle.None,
                Height = mainNotePanel.Height
            };
            noteTextBox.KeyDown += mainForm.NoteTextBox_KeyDown;
            noteTextBox.Font = new Font(noteTextBox.Font.FontFamily, 10f);
            //noteTextBox.Location = new Point(0, 3);
            //noteTextBox.Multiline = true;
            //noteTextBox.ScrollBars = ScrollBars.None;
            //noteTextBox.WordWrap = true;
            //noteTextBox.AutoSize = true;
            mainNotePanel.Controls.Add(noteTextBox);

            // --- Sub Notes Panel

            SubNotesPanel = new()
            {
                Name = "subNotePanel",
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = rootPanel.BackColor,
                Width = rootPanel.Width - 17,
                //Height = Globals.defaultPanelHeight,
                Location = new Point(17, Globals.defaultPanelHeight),
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
            };

            doneCheckBox.Checked = Note.Done;
            noteTextBox.Text = Note.Text;

            UiPanel.Controls.Add(mainNotePanel);
            UiPanel.Controls.Add(SubNotesPanel);

            rootPanel.Controls.Add(UiPanel);
            if (index >= 0)
                rootPanel.Controls.SetChildIndex(mainNotePanel, index);

            UpdatePanelHeight(mainNotePanel);
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
        private void UpdatePanelHeight(Control Panel)
        {
            var textBox = (TextBox)Panel.Controls.Find("noteTextBox", true)[0];
            int textboxLines = (textBox.PreferredSize.Width - 17) / textBox.Width + 1;
            if (textboxLines <= 0)
                textboxLines = 1;
            textBox.Multiline = textboxLines > 1;
            textBox.Height = textboxLines * textBox.Font.Height + 5;

            int subNotesCount = Panel.Controls.Find("subNotePanel", true).Length;
            Panel.Height = textBox.Height + Globals.uiPadding + 
                (Expanded ? 
                    subNotesCount * Globals.defaultPanelHeight + Globals.uiPadding : 
                    0);
        }

        public void AddSubNoteAt(Note note, int index)
        {

        }
    }
}
