using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Notes.Desktop
{
    public static class Globals
    {
        public readonly static Brush expandButtonBrush = new SolidBrush(Color.FromArgb(50, 50, 50));
        public const float maxExpandAnimTime = 15;

        public const int uiPadding = 6;
        public const int defaultPanelHeight = 24;
        public const int subNoteIntend = 45;
    }
}
