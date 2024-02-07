using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Configuration;
using Notes;
using Notes.Interface;

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
        bool dragging = false;
        DragType dragType = DragType.Normal;
        Point dragSauce, globalDragSauce, globalDragLocationSauce;
        Size dragSizeSauce;
        DateTime lastSaveTime = DateTime.Now;
        bool unsavedEdits = false;
        Communicator? comms = null;
        Panel draggedPanel = null;
        const int uiPadding = 6;
        const int defaultPanelHeight = 24;
        const int subNoteIntend = 45;

        public MainForm()
        {
            InitializeComponent();
            this.components = new Container();
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
            if (Config.Data.Notes.Count != 0)
            {
                LoadConfig();
            }
            else
            {
                AddNewNotePanel();
                LayoutNotePanels();
            }
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
                    Task.Delay(500).Wait();
                    if (unsavedEdits)
                    {
                        SaveConfig();
                        comms?.SendString(GetNewPayload().ToString());
                        this.InvokeIfRequired(() => this.Refresh());
                    }
                }
            });
        }

        void SaveConfig(bool updateSaveTime = true)
        {
            lock (Config.Data)
            {
                Config.Data.Notes.Clear();

                foreach (Panel p in rootPanel.Controls.OfType<Panel>())
                {
                    string tmp;
                    var subNotes = new List<SubNote>();
                    foreach (Panel sub in p.Controls.OfType<Panel>())
                        if (sub.Name.StartsWith("sub") && !string.IsNullOrWhiteSpace(tmp = ((TextBox)sub.Controls.Find("subNoteTextBox", true)[0]).Text))
                            subNotes.Add(new SubNote() { Done = ((CheckBox)sub.Controls.Find("subNoteDoneCheckBox", true)[0]).Checked, Text = tmp });

                    Note add = new()
                    {
                        Done = ((CheckBox)p.Controls.Find("doneCheckBox", true)[0]).Checked,
                        Prio = NotePriority.Meduim, // TODO: Add GUI for this
                        Text = ((TextBox)p.Controls.Find("noteTextBox", true)[0]).Text,
                        SubNotes = subNotes
                    };
                    Config.Data.Notes.Add(add);
                }

                Config.Data.Pos = this.Location;
                Config.Data.Size = this.Size;
                if (updateSaveTime)
                    Config.Data.SaveTime = DateTime.Now;

                Config.Save();

                unsavedEdits = false;
                lastSaveTime = DateTime.Now;
            }
        }
        void LoadConfig()
        {
            lock (Config.Data)
            {
                // Remove everything except the start panel
                rootPanel.Controls.Clear();

                foreach (Note n in Config.Data.Notes)
                    AddNewNotePanel(-1, n);

                LayoutNotePanels();
            }
        }

        Panel AddNewNotePanel(int index = -1, Note n = null)
        {
            Panel notePanel = new Panel();
            notePanel.Name = "notePanel";
            notePanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            notePanel.BackColor = rootPanel.BackColor;
            notePanel.Width = rootPanel.Width;
            notePanel.Height = defaultPanelHeight;

            Label orderButton = new Label();
            orderButton.Name = "orderButton";
            orderButton.Text = "⠿";
            orderButton.Font = new Font(orderButton.Font.FontFamily, 10f);
            orderButton.BackColor = rootPanel.BackColor;
            orderButton.ForeColor = Color.FromArgb(30, 30, 30);
            orderButton.Width = 14;
            orderButton.Height = notePanel.Height;
            orderButton.MouseDown += OrderButton_MouseDown;
            orderButton.MouseMove += OrderButton_MouseMove;
            orderButton.MouseUp += OrderButton_MouseUp;
            orderButton.Location = new Point(0, 3);
            notePanel.Controls.Add(orderButton);

            Label expandButton = new Label();
            expandButton.Name = "expandButton";
            expandButton.Text = "🞂";
            expandButton.Font = new Font(expandButton.Font.FontFamily, 10f);
            expandButton.BackColor = rootPanel.BackColor;
            expandButton.ForeColor = Color.FromArgb(30, 30, 30);
            expandButton.Width = 14;
            expandButton.Height = notePanel.Height;
            expandButton.Click += ExpandButton_Click;
            expandButton.Location = new Point(0, 3);
            notePanel.Controls.Add(expandButton);

            CheckBox doneCheckBox = new CheckBox();
            doneCheckBox.Name = "doneCheckBox";
            doneCheckBox.BackColor = rootPanel.BackColor;
            doneCheckBox.Width = 18;
            doneCheckBox.Height = notePanel.Height;
            doneCheckBox.CheckedChanged += DoneCheckBox_CheckedChanged;
            doneCheckBox.Location = new Point(0, 0);
            notePanel.Controls.Add(doneCheckBox);

            TextBox noteTextBox = new TextBox();
            noteTextBox.Name = "noteTextBox";
            noteTextBox.BackColor = rootPanel.BackColor;
            noteTextBox.BorderStyle = BorderStyle.None;
            noteTextBox.Height = notePanel.Height;
            noteTextBox.KeyDown += NoteTextBox_KeyDown;
            noteTextBox.Font = new Font(noteTextBox.Font.FontFamily, 10f);
            noteTextBox.Location = new Point(0, 3);
            notePanel.Controls.Add(noteTextBox);

            if (n != null)
            {
                doneCheckBox.Checked = n.Done;
                noteTextBox.Text = n.Text;

                if (n.SubNotes != null && n.SubNotes.Count > 0)
                    foreach (SubNote s in n.SubNotes)
                        AddNewSubNotePanel(notePanel, -1, s);
                else
                    AddNewSubNotePanel(notePanel);
            }
            else
                AddNewSubNotePanel(notePanel);

            rootPanel.Controls.Add(notePanel);
            if (index >= 0)
                rootPanel.Controls.SetChildIndex(notePanel, index);

            return notePanel;
        }
        Panel AddNewSubNotePanel(Panel parent, int index = -1, SubNote s = null)
        {
            Panel subNotePanel = new Panel();
            subNotePanel.Name = "subNotePanel";
            subNotePanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            subNotePanel.BackColor = rootPanel.BackColor;
            subNotePanel.Width = rootPanel.Width;
            subNotePanel.Height = defaultPanelHeight;

            CheckBox subNoteDoneCheckBox = new CheckBox();
            subNoteDoneCheckBox.Name = "subNoteDoneCheckBox";
            subNoteDoneCheckBox.BackColor = rootPanel.BackColor;
            subNoteDoneCheckBox.Width = 18;
            subNoteDoneCheckBox.Height = subNotePanel.Height;
            if (s != null)
                subNoteDoneCheckBox.Checked = s.Done;
            subNoteDoneCheckBox.CheckedChanged += SubNoteDoneCheckBox_CheckedChanged;
            subNoteDoneCheckBox.Location = new Point(0, 0);
            subNotePanel.Controls.Add(subNoteDoneCheckBox);

            TextBox subNoteTextBox = new TextBox();
            subNoteTextBox.Name = "subNoteTextBox";
            subNoteTextBox.BackColor = rootPanel.BackColor;
            subNoteTextBox.BorderStyle = BorderStyle.None;
            subNoteTextBox.Height = defaultPanelHeight;
            if (s != null)
                subNoteTextBox.Text = s.Text;
            subNoteTextBox.KeyDown += SubNoteTextBox_KeyDown;
            subNoteTextBox.DoubleClick += SubNoteTextBox_DoubleClick;
            subNoteTextBox.Font = new Font(subNoteTextBox.Font.FontFamily, 9f);
            subNoteTextBox.Location = new Point(0, 4);
            subNotePanel.Controls.Add(subNoteTextBox);

            if (subNoteDoneCheckBox.Checked)
                subNoteTextBox.Font = new Font(subNoteTextBox.Font, FontStyle.Strikeout);

            parent.Controls.Add(subNotePanel);
            if (index >= 0)
                parent.Controls.SetChildIndex(subNotePanel, index);

            LayoutNotePanels();

            return subNotePanel;
        }

        void LayoutNotePanels()
        {
            int scroll = rootPanel.VerticalScroll.Value;
            rootPanel.VerticalScroll.Value = 0;

            int curY = 0;
            foreach (Panel p in rootPanel.Controls.OfType<Panel>())
            {
                p.Location = new Point(0, curY);
                curY += p.Height;

                int curX = 0, curSubY = defaultPanelHeight + uiPadding;
                foreach (Control c in p.Controls)
                {
                    if (c.Name == "subNotePanel")
                    {
                        c.Location = new Point(25, curSubY);
                        curSubY += c.Height;

                        int curSubX = subNoteIntend;
                        foreach (Control s in c.Controls)
                        {
                            s.Location = new Point(curSubX, s.Location.Y);
                            curSubX += s.Width + uiPadding;
                        }
                        var subTextBox = c.Controls.Find("subNoteTextBox", true).First();
                        subTextBox.Width = c.Width - subTextBox.Location.X;
                    }
                    else
                    {
                        c.Location = new Point(curX, c.Location.Y);
                        curX += c.Width + uiPadding;
                    }
                }
                var textBox = p.Controls.Find("noteTextBox", true).First();
                textBox.Width = p.Width - textBox.Location.X;
            }

            rootPanel.VerticalScroll.Value = scroll;
        }
        private void UpdatePanelHeight(Control Panel, int lenOffset = 0)
        {
            try
            {
                int len = Panel.Controls.Find("subNotePanel", true).Count();
                Panel.Height = defaultPanelHeight + len * (defaultPanelHeight) + uiPadding * 2;
            }
            catch { }
        }

        Payload GetNewPayload() => new Payload(Config.Data.SaveTime, Config.Data.Notes);

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

        private void SubNoteTextBox_DoubleClick(object sender, EventArgs e)
        {
            var origin = (TextBox)sender;
            if (origin.Text.Contains("://"))
                try { Process.Start(new ProcessStartInfo(origin.Text) { UseShellExecute = true }); }
                catch (Exception ex) { Logger.Write(ex); }
        }
        private void NoteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var origin = (TextBox)sender;
            if (e.KeyCode == Keys.Enter)
            {
                int index = rootPanel.Controls.IndexOf(origin.Parent);
                var insertionIndex = origin.SelectionStart == 0 ? index : index + 1;
                var p = AddNewNotePanel(insertionIndex);
                LayoutNotePanels();

                p.Controls.Find("noteTextBox", true).First().Focus();
            }
            else
            {
                if (e.KeyCode == Keys.Back && origin.Text.Length == 0 && rootPanel.Controls.Count > 1)
                {
                    int scroll = rootPanel.VerticalScroll.Value;
                    rootPanel.VerticalScroll.Value = 0;

                    int index = rootPanel.Controls.IndexOf(origin.Parent);
                    rootPanel.Controls.RemoveAt(index);
                    if (index > 0)
                        rootPanel.Controls[index - 1].Controls.Find("noteTextBox", true).First().Focus();
                    else
                        rootPanel.Controls.Find("noteTextBox", true).First().Focus();
                    LayoutNotePanels();

                    rootPanel.VerticalScroll.Value = scroll;
                }
            }
            unsavedEdits = true;
        }
        private void SubNoteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var origin = (TextBox)sender;
            var root = (Panel)origin.Parent.Parent;
            if (e.KeyCode == Keys.Enter)
            {
                int index = origin.Parent.Parent.Controls.IndexOf(origin.Parent);
                var insertionIndex = origin.SelectionStart == 0 ? index : index + 1;
                var p = AddNewSubNotePanel(root, insertionIndex);
                UpdatePanelHeight(root, 1);
                LayoutNotePanels();

                p.Controls.Find("subNoteTextBox", true).First().Focus();
            }
            else
            {
                if (e.KeyCode == Keys.Back && origin.Text.Length == 0 && root.Controls.Find("subNotePanel", true).Count() > 1)
                {
                    int index = root.Controls.IndexOf(origin.Parent);
                    root.Controls.RemoveAt(index);
                    if (index > 4)
                        root.Controls[index - 1].Controls.Find("subNoteTextBox", true).First().Focus();
                    else
                        root.Controls.Find("subNoteTextBox", true).First().Focus();
                    UpdatePanelHeight(root);
                    LayoutNotePanels();
                }
            }
            unsavedEdits = true;
        }
        private void DoneCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var origin = (CheckBox)sender;
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
            unsavedEdits = true;
        }
        private void SubNoteDoneCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var origin = (CheckBox)sender;
            if (origin.Checked)
            {
                var textBox = origin.Parent.Controls.Find("subNoteTextBox", true).First();
                textBox.Font = new Font(textBox.Font, FontStyle.Strikeout);
            }
            else
            {
                var textBox = origin.Parent.Controls.Find("subNoteTextBox", true).First();
                textBox.Font = new Font(textBox.Font, FontStyle.Regular);
            }
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

                int mousePosY = e.Y + origin.Location.Y + origin.Parent.Location.Y;

                int minVal = int.MaxValue; Panel minPanel = null;
                foreach (Panel x in rootPanel.Controls.OfType<Panel>())
                    if (Math.Abs(mousePosY - x.Location.Y) < minVal)
                    {
                        minVal = Math.Abs(mousePosY - x.Location.Y);
                        minPanel = x;
                    }
                int panelIndex = rootPanel.Controls.IndexOf(minPanel);

                if (panelIndex > rootPanel.Controls.IndexOf(origin.Parent))
                    panelIndex--;

                rootPanel.Controls.SetChildIndex(draggedPanel, panelIndex);
                LayoutNotePanels();
                unsavedEdits = true;
                barThingy.Visible = false;
            }
            draggedPanel = null;
        }
        private void OrderButton_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedPanel != null)
            {
                var origin = (Label)sender;

                int mousePosY = e.Y + origin.Location.Y + origin.Parent.Location.Y;

                int minVal = int.MaxValue; Panel minPanel = null;
                foreach (Panel x in rootPanel.Controls.OfType<Panel>())
                    if (Math.Abs(mousePosY - x.Location.Y) < minVal)
                    {
                        minVal = Math.Abs(mousePosY - x.Location.Y);
                        minPanel = x;
                    }
                int panelIndex = rootPanel.Controls.IndexOf(minPanel);

                barThingy.Location = new Point(barThingy.Location.X, panelIndex * defaultPanelHeight + rootPanel.Location.Y);
            }
        }
        private void ExpandButton_Click(object sender, EventArgs e)
        {
            var origin = (Label)sender;
            if (origin.Parent.Height > defaultPanelHeight)
            {
                // Already expanded
                origin.Parent.Height = defaultPanelHeight;
            }
            else
            {
                // Closed
                UpdatePanelHeight(origin.Parent);
            }
            LayoutNotePanels();
        }

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
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            LayoutNotePanels();
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveConfig();
            comms?.Dispose();
        }
    }
}
