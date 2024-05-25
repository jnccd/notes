
namespace Notes.Desktop
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            rootPanel = new System.Windows.Forms.Panel();
            barThingy = new System.Windows.Forms.PictureBox();
            labelConnectionStatus = new System.Windows.Forms.Label();
            labelConnectionStatusUnderline = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)barThingy).BeginInit();
            SuspendLayout();
            // 
            // rootPanel
            // 
            rootPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            rootPanel.AutoScroll = true;
            rootPanel.Location = new System.Drawing.Point(10, 27);
            rootPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            rootPanel.Name = "rootPanel";
            rootPanel.Size = new System.Drawing.Size(416, 311);
            rootPanel.TabIndex = 1;
            // 
            // barThingy
            // 
            barThingy.BackColor = System.Drawing.Color.DimGray;
            barThingy.Location = new System.Drawing.Point(28, 0);
            barThingy.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            barThingy.Name = "barThingy";
            barThingy.Size = new System.Drawing.Size(219, 4);
            barThingy.TabIndex = 2;
            barThingy.TabStop = false;
            barThingy.Visible = false;
            // 
            // labelConnectionStatus
            // 
            labelConnectionStatus.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            labelConnectionStatus.BackColor = System.Drawing.Color.FromArgb(231, 222, 103);
            labelConnectionStatus.Location = new System.Drawing.Point(10, 9);
            labelConnectionStatus.Name = "labelConnectionStatus";
            labelConnectionStatus.Size = new System.Drawing.Size(416, 14);
            labelConnectionStatus.TabIndex = 3;
            labelConnectionStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelConnectionStatusUnderline
            // 
            labelConnectionStatusUnderline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            labelConnectionStatusUnderline.BackColor = System.Drawing.Color.FromArgb(192, 0, 0);
            labelConnectionStatusUnderline.Location = new System.Drawing.Point(10, 23);
            labelConnectionStatusUnderline.Name = "labelConnectionStatusUnderline";
            labelConnectionStatusUnderline.Size = new System.Drawing.Size(416, 3);
            labelConnectionStatusUnderline.TabIndex = 4;
            labelConnectionStatusUnderline.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(231, 222, 103);
            ClientSize = new System.Drawing.Size(438, 347);
            Controls.Add(labelConnectionStatusUnderline);
            Controls.Add(labelConnectionStatus);
            Controls.Add(barThingy);
            Controls.Add(rootPanel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            MinimumSize = new System.Drawing.Size(175, 112);
            Name = "MainForm";
            ShowInTaskbar = false;
            Text = "Notes";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            MouseDown += MainForm_MouseDown;
            MouseMove += MainForm_MouseMove;
            MouseUp += MainForm_MouseUp;
            ((System.ComponentModel.ISupportInitialize)barThingy).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.PictureBox barThingy;
        private System.Windows.Forms.Panel rootPanel;
        private System.Windows.Forms.Label labelConnectionStatus;
        private System.Windows.Forms.Label labelConnectionStatusUnderline;
    }
}

