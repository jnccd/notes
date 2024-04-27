using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Text;
using Android.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Android.Graphics.Paint;
using Path = Android.Graphics.Path;

namespace NotesAndroid
{
    public class TriangleButton : Button
    {
        public TriangleButton(Context? context) : base(context)
        {
        }

        public TriangleButton(Context? context, IAttributeSet? attrs) : base(context, attrs)
        {
        }

        public TriangleButton(Context? context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
        }

        public TriangleButton(Context? context, IAttributeSet? attrs, int defStyleAttr, int defStyleRes) : base(context, attrs, defStyleAttr, defStyleRes)
        {
        }

        protected TriangleButton(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override void OnDraw(Canvas canvas)
        {
            Paint paint = new()
            {
                StrokeWidth = 4,
                AntiAlias = true,
                //Color = Color.Rgb(20, 20, 20),
                Color = Color.Rgb(  Color.GetRedComponent(this.TextColors.DefaultColor),
                                    Color.GetGreenComponent(this.TextColors.DefaultColor),
                                    Color.GetBlueComponent(this.TextColors.DefaultColor)),
            };
            paint.SetStyle(Style.FillAndStroke);

            int minAspect = Math.Min(Width, Height), halfMinAspect = minAspect / 2;
            Point center = new(Width / 2, Height / 2);
            int size = (int)(halfMinAspect * 0.9);
            int halfSize = size / 2;
            int weirdSize = size * 2 / 3;

            Path path = new();
            path.SetFillType(Path.FillType.EvenOdd);
            path.MoveTo(center.X - halfSize, center.Y - weirdSize);
            path.LineTo(center.X + size - halfSize, center.Y);
            path.LineTo(center.X - halfSize, center.Y + weirdSize);
            path.Close();
            canvas.DrawPath(path, paint);
        }
    }
}
