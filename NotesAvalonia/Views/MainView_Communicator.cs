using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Notes.Interface;
using NotesAvalonia.Configuration;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public partial class MainView : UserControl
{
    public Communicator? communicator = null;
    DateTime lastSaveTime = DateTime.MinValue;
    public bool unsavedChanges = false;

    private void InitCommunicatorBasedOnConfig()
    {
        if (Config.Data.ServerUri != null && Config.Data.ServerUsername != null && Config.Data.ServerPassword != null)
        {
            if (communicator != null)
                communicator.Dispose();
            communicator = new Communicator(
                Config.Data.ServerUri,
                Config.Data.ServerUsername,
                Config.Data.ServerPassword, (CommsState state) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var connectionBar = this.GetLogicalDescendants()
                                .OfType<Rectangle>()
                                .FirstOrDefault(x => x.Name == "ConnectionBar");
                        if (connectionBar == null)
                            return;
                        if (state == CommsState.Connected)
                        {
                            connectionBar.Fill = Avalonia.Media.Brushes.Green;
                        }
                        else if (state == CommsState.Disconnected)
                        {
                            connectionBar.Fill = Avalonia.Media.Brushes.Red;
                        }
                        if (viewModel != null)
                            viewModel.ConnectionState = state == CommsState.Disconnected ? "Disconnected" : $"Connected to {Config.Data.ServerUsername}@{Config.Data.ServerUri.Split("//").Last()}";
                    });
                }
            );
            communicator.RequestLoopInterval = 5000;
            communicator.StartRequestLoop(OnPayloadReceived);
        }
    }

    private void Handle_Communicator_On_MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        LoadConfig();

        Payload GetNewPayload() => new(Config.Data.SaveTime, Config.Data.Notes);
        Task.Run(() =>
            {
                Thread.CurrentThread.Name = "Autosave Thread";
                while (true)
                {
                    Task.Delay(500).Wait();
                    if (unsavedChanges)
                    {
                        unsavedChanges = false;
                        SaveConfig();
                        communicator?.SendString(GetNewPayload().ToString());
                    }
                }
            });
    }

    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        if (viewModel != null)
            viewModel.AddDebugText($"LoginButton_PointerPressed");
        var parent = (sender as Button)?.Parent;
        var hostBox = parent?.GetLogicalDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(x => x.Name == "UrlTextBox");
        var usernameBox = parent?.GetLogicalDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(x => x.Name == "UsernameTextBox");
        var passwordBox = parent?.GetLogicalDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(x => x.Name == "PasswordTextBox");
        if (string.IsNullOrWhiteSpace(hostBox?.Text) || string.IsNullOrWhiteSpace(usernameBox?.Text) || string.IsNullOrWhiteSpace(passwordBox?.Text))
            return;

        Config.Data.ServerUri = hostBox.Text;
        Config.Data.ServerUsername = usernameBox.Text;
        Config.Data.ServerPassword = passwordBox.Text;
        Config.Save();

        InitCommunicatorBasedOnConfig();
    }

    void OnPayloadReceived(string receivedText, Payload? payload)
    {
        bool validPayload = false;
        lock (Config.Data)
        {
            validPayload = payload != null &&
                payload.Checksum == payload.GenerateChecksum() &&
                Config.Data.SaveTime < payload.SaveTime;

            if (validPayload)
            {
                Config.Data.Notes = payload!.Notes;
            }
        }

        if (validPayload)
            Dispatcher.UIThread.Post(() =>
            {
                LoadConfig();
                SaveConfig(false);
            });
    }

    void SaveConfig(bool updateSaveTime = true)
    {
        lock (Config.Data)
        {
            var window = this.Parent as Window;
            var windowPos = window?.Position;
            if (windowPos != null)
                Config.Data.Pos = window!.Position;
            if (window != null && window.FrameSize != null)
            {
                Config.Data.Width = window.FrameSize.Value.Width;
                Config.Data.Height = window.FrameSize.Value.Height;
            }

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
            if (viewModel == null)
                return;
            viewModel.LoadNew(Config.Data.Notes);
        }
    }
}