using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Configuration;
using Notes.Interface;
using Notes.Interface.UiController;
using Notes.Desktop.UiInterface;

namespace Notes.Desktop
{
    enum DragType
    {
        Normal,
        ResizeBottomRight,
        ResizeBottomLeft,
        ResizeTopRight,
        ResizeTopLeft,
    }

    public partial class MainForm : Form
    {
        // General
        DateTime lastSaveTime = DateTime.Now;
        bool unsavedEdits = false;
        Communicator comms = null;
        private NoteUi rootNode = null;

        // Interface wrappers
        readonly FormWrapper wrappedSelf;
        readonly LayoutWrapper rootLayout;

        // Drag
        bool dragging = false;
        DragType dragType = DragType.Normal;
        Point dragSauce, globalDragSauce, globalDragLocationSauce;
        Size dragSizeSauce;
        Panel draggedPanel = null;
        int minPanelCounter;

        // GUI magic numbers
        const float fontSize = 10f;
        const int treeDepthPadding = 20;

        // Init
        public MainForm()
        {
            InitializeComponent();
            this.components = new Container();

            wrappedSelf = new(this);
            rootLayout = new(rootPanel);

            //this.FormBorderStyle = FormBorderStyle.None;
            //this.DoubleBuffered = true;
            //this.SetStyle(ControlStyles.ResizeRedraw, true);
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            #region GUI Setup
            if (Config.Data.Pos.X != -1)
            {
                this.Location = Config.Data.Pos;
                this.Size = Config.Data.Size;
            }

            if (Config.Data.Notes.Count == 0)
                Config.Data.Notes.Add(new Note());
            LoadConfig();

            if (Config.Data.BackColor.A != 255)
                Config.Data.BackColor = Color.FromArgb(231, 222, 103);
            else
            {
                this.BackColor = Config.Data.BackColor;
                rootPanel.BackColor = Config.Data.BackColor;
                foreach (Panel p in rootPanel.Controls.OfType<Panel>())
                    p.BackColor = Config.Data.BackColor;
            }
            unsavedEdits = false;
            #endregion

            if (!string.IsNullOrWhiteSpace(Config.Data.ServerUri))
            {
                comms = new Communicator(Config.Data.ServerUri, Config.Data.ServerUsername, Config.Data.ServerPassword, GetNewPayload, Logger.logger);
                comms.StartRequestLoop(OnPayloadRecieved);
            }

            Task.Run(() =>
            {
                Thread.CurrentThread.Name = "Autosave Thread";
                while (true)
                {
                    Task.Delay(200).Wait();
                    if (unsavedEdits)
                    {
                        unsavedEdits = false;
                        SaveConfig();
                        comms?.SendString(GetNewPayload().ToString());
                        this.InvokeIfRequired(() => this.Refresh());
                    }
                }
            });
        }

        // IO
        void SaveConfig(bool updateSaveTime = true)
        {
            lock (Config.Data)
            {
                Config.Data.Pos = this.Location;
                Config.Data.Size = this.Size;
                if (updateSaveTime)
                    Config.Data.SaveTime = DateTime.Now;

                Config.Save();

                lastSaveTime = DateTime.Now;
            }
        }
        void LoadConfig()
        {
            lock (Config.Data)
            {
                rootPanel.Controls.Clear();
                NoteUi.UiToNote.Clear();

                rootNode = new NoteUi(Config.Data.Notes, wrappedSelf, rootLayout, CreateNoteUi);
                LayoutNotePanels();
            }
        }
        Payload GetNewPayload() => new(Config.Data.SaveTime, Config.Data.Notes);
        void OnPayloadRecieved(string receivedText, Payload payload)
        {
            Logger.WriteLine($"Recived {receivedText}", false);

            bool validPayload = false;
            lock (Config.Data)
            {
                validPayload = payload != null &&
                    payload.Checksum == payload.GenerateChecksum() &&
                    Config.Data.SaveTime < payload.SaveTime;

                if (validPayload)
                {
                    if (payload.Notes.Count + 2 < Config.Data.Notes.Count)
                    {
                        Logger.WriteLine("I think theres something missing...");
                        Logger.WriteLine($"Config has {Config.Data.Notes.Count} Notes and Paylaod {payload.Notes.Count}");
                        Logger.WriteLine($"Recived {receivedText}");
                        validPayload = false;
                        return;
                    }

                    Config.Data.Notes = payload.Notes;
                }
            }

            if (validPayload)
                this.InvokeIfRequired(() =>
                {
                    LoadConfig();
                    SaveConfig(false);
                });
        }

        // Horrible custom GUI code
        public void LayoutNotePanels()
        {
            int scroll = rootPanel.VerticalScroll.Value;
            //rootPanel.VerticalScroll.Value = 0;

            int curY = -scroll;
            foreach (Panel p in rootPanel.Controls.OfType<Panel>())
            {
                // Panel layout
                p.Location = new Point(p.Location.X, curY);
                p.Width = rootPanel.Width - p.Location.X - 12;
                UpdatePanelHeight(NoteUi.UiToNote[new LayoutWrapper(p)]);
                curY += p.Height;

                // Layout of controls inside panel
                int curX = 0;
                foreach (Control c in p.Controls)
                {
                    c.Location = new Point(curX, c.Location.Y);
                    curX += c.Width + Globals.uiPadding;
                }
                var textBox = p.Controls.Find("noteTextBox", true).First();
                textBox.Width = p.Width - textBox.Location.X;
            }
        }
        static void UpdatePanelHeight(NoteUi noteUi)
        {
            var UiPanel = ((LayoutWrapper)noteUi.UiLayout).Layout;

            if (!noteUi.AreAllParentsExpanded())
                UiPanel.Height = 0;
            else
            {
                var textBox = (TextBox)UiPanel.Controls.Find("noteTextBox", true)[0];
                int textboxLines = (textBox.PreferredSize.Width - 10) / textBox.Width + 1;
                if (textboxLines <= 0)
                    textboxLines = 1;
                textBox.Multiline = textboxLines > 1;
                textBox.Height = textboxLines * (textBox.Font.Height - 2) + 5;

                UiPanel.Height = textBox.Height + Globals.uiPadding;
            }
        }
        IUiLayout CreateNoteUi(IUiWindow mainWindow, Note note, int index = -1, int depth = 0)
        {
            var mainForm = ((FormWrapper)mainWindow).Form;

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
                Checked = note.Done,
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
                Text = note.Text,
            };
            noteTextBox.KeyUp += mainForm.NoteTextBox_KeyUp;
            noteTextBox.KeyDown += mainForm.NoteTextBox_KeyDown;
            noteTextBox.DoubleClick += mainForm.NoteTextBox_DoubleClick;
            noteTextBox.Font = new Font(noteTextBox.Font.FontFamily, fontSize);
            mainNotePanel.Controls.Add(noteTextBox);

            rootPanel.Controls.Add(mainNotePanel);
            if (index >= 0)
                rootPanel.Controls.SetChildIndex(mainNotePanel, index);

            return new LayoutWrapper(mainNotePanel);
        }

        // Note GUI Events
        private void NoteTextBox_DoubleClick(object sender, EventArgs e)
        {
            var origin = (TextBox)sender;
            if (origin.Text.Contains("://"))
                try { Process.Start(new ProcessStartInfo(origin.Text) { UseShellExecute = true }); }
                catch (Exception ex) { Logger.Write(ex); }
        }
        private void NoteTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            var origin = (TextBox)sender;
            var noteUiOrigin = NoteUi.UiToNote[new LayoutWrapper(origin.Parent)];
            if (e.KeyCode == Keys.Enter)
            {
                int insertionIndex = noteUiOrigin.Parent.SubNotes.IndexOf(noteUiOrigin);

                if (origin.SelectionStart != 0)
                    insertionIndex++;

                var p = noteUiOrigin.Parent.AddSubNoteBefore(new Note(), wrappedSelf, rootLayout, CreateNoteUi, insertionIndex);
                LayoutNotePanels();

                ((LayoutWrapper)p.UiLayout).Layout.Controls.Find("noteTextBox", true).First().Focus();
            }
            else
            {
                noteUiOrigin.Note.Text = origin.Text;
            }
            unsavedEdits = true;
        }
        private void NoteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var origin = (TextBox)sender;
            var noteUiOrigin = NoteUi.UiToNote[new LayoutWrapper(origin.Parent)];
            if (e.KeyCode == Keys.Back && origin.Text.Length == 0 && rootPanel.Controls.Count > 1)
            {
                int index = rootPanel.Controls.IndexOf(origin.Parent);
                noteUiOrigin.Parent.RemoveSubNote(noteUiOrigin);
                if (index > 0)
                    rootPanel.Controls[index - 1].Controls.Find("noteTextBox", true).First().Focus();
                else
                    rootPanel.Controls.Find("noteTextBox", true).First().Focus();
            }
            unsavedEdits = true;
        }
        private void DoneCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var origin = (CheckBox)sender;
            var noteUiOrigin = NoteUi.UiToNote[new LayoutWrapper(origin.Parent)];
            if (origin.Checked)
            {
                var textBox = origin.Parent.Controls.Find("noteTextBox", true).First();
                textBox.Font = new Font(textBox.Font, FontStyle.Strikeout);
            }
            else
            {
                var textBox = origin.Parent.Controls.Find("noteTextBox", true).First();
                textBox.Font = new Font(textBox.Font, FontStyle.Regular);
            }
            noteUiOrigin.Note.Done = origin.Checked;
            unsavedEdits = true;
        }
        private void OrderButton_MouseDown(object sender, MouseEventArgs e)
        {
            var origin = (Label)sender;
            draggedPanel = (Panel)origin.Parent;
            barThingy.Visible = true;
        }
        private void OrderButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (draggedPanel != null)
            {
                var origin = (Label)sender;
                var draggedNoteUi = NoteUi.UiToNote[new LayoutWrapper(draggedPanel)];
                var draggedNoteSubNotePanels = draggedNoteUi.GetAllChildren().Select(x => (Panel)x.UiLayout).ToList();

                int mousePosY = e.Y + origin.Location.Y + origin.Parent.Location.Y;

                int minVal = int.MaxValue; Panel minPanel = null;
                foreach (Panel x in rootPanel.Controls.OfType<Panel>())
                    if (x.Height > 5 && 
                        !draggedNoteSubNotePanels.Contains(x) && 
                        Math.Abs(mousePosY - x.Location.Y) < minVal)
                    {
                        minVal = Math.Abs(mousePosY - x.Location.Y);
                        minPanel = x;
                    }
                if (minPanel == null)
                    return;
                int oldDraggedNoteUiPanelIndex = rootPanel.Controls.IndexOf((Panel)draggedNoteUi.UiLayout);
                int panelIndex = rootPanel.Controls.IndexOf(minPanel);
                var noteUiMinPanel = NoteUi.UiToNote[new LayoutWrapper(minPanel)];

                // Remove and reinsert
                draggedNoteUi.Parent.RemoveSubNote(draggedNoteUi);
                int minPanelNoteUiIndex = noteUiMinPanel.Parent.SubNotes.IndexOf(noteUiMinPanel);
                noteUiMinPanel.Parent.AddSubNoteBefore(draggedNoteUi.Note, wrappedSelf, rootLayout, CreateNoteUi, minPanelNoteUiIndex);

                //rootPanel.Controls.SetChildIndex(draggedPanel, panelIndex);
                barThingy.Visible = false;
            }
            draggedPanel = null;
        }
        private void OrderButton_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedPanel != null)
            {
                var origin = (Label)sender;
                var draggedNoteUi = NoteUi.UiToNote[new LayoutWrapper(draggedPanel)];
                var draggedNoteSubNotePanels = draggedNoteUi.GetAllChildren().Select(x => (Panel)x.UiLayout).ToList();

                int mousePosY = e.Y + origin.Location.Y + origin.Parent.Location.Y;

                int minVal = int.MaxValue, counter = 0; 
                minPanelCounter = 0; 
                Panel minPanel = null;
                foreach (Panel x in rootPanel.Controls.OfType<Panel>())
                    if (x.Height > 5 && 
                        !draggedNoteSubNotePanels.Contains(x))
                    {
                        counter++;
                        if (Math.Abs(mousePosY - x.Location.Y) < minVal)
                        {
                            minVal = Math.Abs(mousePosY - x.Location.Y);
                            minPanel = x;
                            minPanelCounter = counter;
                        }
                    }
                    
                int panelIndex = rootPanel.Controls.IndexOf(minPanel);

                barThingy.Location = new Point(barThingy.Location.X, (minPanelCounter-1) * Globals.defaultPanelHeight + rootPanel.Location.Y);
            }
        }
        private void ExpandButton_Click(object sender, EventArgs e)
        {
            var origin = (Control)sender;
            var noteUiOrigin = NoteUi.UiToNote[new LayoutWrapper(origin.Parent)];

            noteUiOrigin.ToggleExpand();
        }

        // Form Events
        protected override void OnPaint(PaintEventArgs e)
        {
            int cGrip = 16;
            ControlPaint.DrawSizeGrip(e.Graphics, this.BackColor, new Rectangle(this.ClientSize.Width - cGrip, this.ClientSize.Height - cGrip, cGrip, cGrip));

            //if (unsavedEdits)
            //{
            //    Rectangle r = ClientRectangle;
            //    for (int i = 0; i < 3; i++)
            //    {
            //        ControlPaint.DrawFocusRectangle(e.Graphics, r);
            //        r.Inflate(-1, -1);
            //    }
            //}
        }
        private void MainForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragSauce = e.Location;
                globalDragSauce = this.PointToScreen(e.Location);
                globalDragLocationSauce = this.Location;
                dragSizeSauce = this.Size;

                if (dragSauce.X >= this.Width - 16 && dragSauce.Y >= this.Height - 16)
                    dragType = DragType.ResizeBottomRight;
                else if (dragSauce.X <= 16 && dragSauce.Y >= this.Height - 16)
                    dragType = DragType.ResizeBottomLeft;
                else if (dragSauce.X >= this.Width - 16 && dragSauce.Y <= 16)
                    dragType = DragType.ResizeTopRight;
                else if (dragSauce.X <= 16 && dragSauce.Y <= 16)
                    dragType = DragType.ResizeTopLeft;
                else
                    dragType = DragType.Normal;

                dragging = true;
            }
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip m = new ContextMenuStrip(this.components);
                m.Items.Add(new ToolStripMenuItem("Change Back Color", null, (object sender, EventArgs e) =>
                {

                    ColorDialog cd = new ColorDialog()
                    {
                        AllowFullOpen = true,
                        AnyColor = true
                    };

                    DialogResult dr = cd.ShowDialog();
                    if (dr == DialogResult.OK)
                    {
                        this.GetChildrenRecursively().ForEach(x => x.BackColor = cd.Color);
                        Config.Data.BackColor = cd.Color;
                        Config.Save();
                    }
                }));
                m.Items.Add(new ToolStripMenuItem($"Set Upstream (Current: {comms?.serverUri})", null, (object sender, EventArgs e) =>
                {
                    string newServerUri = "";
                    StringDialog serverUriDia = new("What Server Url should I use?", Config.Data.ServerUri);
                    while (string.IsNullOrWhiteSpace(newServerUri) && serverUriDia.DialogResult != DialogResult.OK)
                    {
                        serverUriDia.ShowDialog();
                        Debug.WriteLine(serverUriDia.DialogResult);
                        newServerUri = serverUriDia.result;
                    }

                    string newServerUsername = "";
                    StringDialog serverUsernameDia = new("What Server Username should I use?", Config.Data.ServerUsername);
                    while (string.IsNullOrWhiteSpace(newServerUsername) && serverUsernameDia.DialogResult != DialogResult.OK)
                    {
                        serverUsernameDia.ShowDialog();
                        newServerUsername = serverUsernameDia.result;
                    }

                    string newServerPassword = "";
                    StringDialog serverPasswordDia = new("What Server Password should I use?", Config.Data.ServerPassword);
                    while (string.IsNullOrWhiteSpace(newServerPassword) && serverPasswordDia.DialogResult != DialogResult.OK)
                    {
                        serverPasswordDia.ShowDialog();
                        newServerPassword = serverPasswordDia.result;
                    }

                    //Config.Data.ServerUri
                    if (comms != null)
                        comms.Dispose();
                    Config.Data.ServerUri = newServerUri;
                    Config.Data.ServerUsername = newServerUsername;
                    Config.Data.ServerPassword = newServerPassword;
                    comms = new Communicator(Config.Data.ServerUri, Config.Data.ServerUsername, Config.Data.ServerPassword, GetNewPayload, Logger.logger);
                    comms.StartRequestLoop(OnPayloadRecieved);
                }));
                //m.Items.Add(new ToolStripMenuItem("Send sync data", null, (object sender, EventArgs e) => {
                //    Task.Run(() =>
                //    {
                //        comms.Send(Communicator.reqMessage);

                //        for (int i = 0; i < 500; i++)
                //        {
                //            comms.Send(GetNewPayload().ToString());

                //            Thread.Sleep(20);
                //        }
                //    });
                //}));
                m.Items.Add(new ToolStripMenuItem("Close", null, (object sender, EventArgs e) => Close()));

                m.Show(this.PointToScreen(e.Location));
                m.Width = 300;
            }
        }
        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point globalLoc = this.PointToScreen(e.Location);
                Point diff = new Point(globalLoc.X - globalDragSauce.X, globalLoc.Y - globalDragSauce.Y);

                if (dragType == DragType.ResizeBottomRight)
                    this.Size = new Size(dragSizeSauce.Width + diff.X, dragSizeSauce.Height + diff.Y);
                else if (dragType == DragType.ResizeBottomLeft)
                {
                    this.Location = new Point(globalDragLocationSauce.X + diff.X, globalDragLocationSauce.Y);
                    this.Size = new Size(dragSizeSauce.Width - diff.X, dragSizeSauce.Height + diff.Y);
                }
                else if (dragType == DragType.ResizeTopLeft)
                {
                    this.Location = new Point(globalDragLocationSauce.X + diff.X, globalDragLocationSauce.Y + diff.Y);
                    this.Size = new Size(dragSizeSauce.Width - diff.X, dragSizeSauce.Height - diff.Y);
                }
                else if (dragType == DragType.ResizeTopRight)
                {
                    this.Location = new Point(globalDragLocationSauce.X, globalDragLocationSauce.Y + diff.Y);
                    this.Size = new Size(dragSizeSauce.Width + diff.X, dragSizeSauce.Height - diff.Y);
                }
                else if (dragType == DragType.Normal)
                    this.Location = new Point(globalLoc.X - dragSauce.X, globalLoc.Y - dragSauce.Y);
            }
        }
        private void MainForm_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;

            if (dragType != DragType.Normal)
            {
                this.Refresh();
                LayoutNotePanels();
            }
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveConfig();
            comms?.Dispose();
        }
    }
}
