using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;

namespace NotesAndroid
{
    public static class ExtensionsAndroid
    {
        public static void ShowAsAlert(this AppCompatActivity a, string title = "", string message = "")
        {
            AlertDialog.Builder dialog = new AlertDialog.Builder(a);
            AlertDialog alert = dialog.Create();
            alert.SetTitle(title);
            alert.SetMessage(message);
            alert.Show();
        }
        public static void ShowAsAlertPrompt(this AppCompatActivity a, string title = "", string message = "", Action<string> resultHandler = null)
        {
            LayoutInflater layoutInflater = LayoutInflater.From(a);
            View view = layoutInflater.Inflate(Resource.Layout.text_input_dialog, null);
            AlertDialog.Builder alertbuilder = new AlertDialog.Builder(a);
            alertbuilder.SetView(view);
            var titleLabel = view.FindViewById<TextView>(Resource.Id.textInputDialogTitle);
            var textBox = view.FindViewById<EditText>(Resource.Id.textInputDialogTextbox);
            titleLabel.Text = title;
            textBox.Text = message;
            alertbuilder.
                SetCancelable(false).
                SetPositiveButton("Submit", delegate
                {
                    resultHandler?.Invoke(textBox.Text);
                    //Toast.MakeText(a, "Submit Input: " + textBox.Text, ToastLength.Short).Show();
                })
                .SetNegativeButton("Cancel", delegate
                {
                    alertbuilder.Dispose();
                });
            AlertDialog dialog = alertbuilder.Create();
            dialog.Show();
        }
        public static List<View> GetChildren(this ViewGroup layout)
        {
            List<View> re = new List<View>();
            for (int i = 0; i < layout.ChildCount; i++)
                re.Add(layout.GetChildAt(i));
            return re;
        }
        public static List<View> GetChildrenR(this ViewGroup layout)
        {
            List<View> re = new List<View>();
            for (int i = 0; i < layout.ChildCount; i++)
                if (layout.GetChildAt(i) is ViewGroup)
                    re.AddRange(
                        GetChildrenR(layout.GetChildAt(i) as ViewGroup).
                        Concat(new View[] { layout.GetChildAt(i) }));
                else
                    re.Add(layout.GetChildAt(i));

            return re;
        }
    }
}