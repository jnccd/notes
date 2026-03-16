using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using EzKeycloak;
using Notes.Interface;
using NotesAvalonia.Configuration;
using NotesAvalonia.ViewModels;

namespace NotesAvalonia.Views;

public record OpenUrlActionOnSystem(bool IsCurrentOperatingSystem, Action<string> OpenUrl);

public partial class MainView : UserControl
{
    public Communicator? communicator { get; private set; } = null;
    DateTime lastSaveTime = DateTime.MinValue;
    public bool unsavedChanges = false;
    public List<OpenUrlActionOnSystem> OpenUrlActionsOnSystem { get; private set; } = [
        new(OperatingSystem.IsWindows(), (url) =>
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            })),
        new(OperatingSystem.IsLinux(), (url) =>
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
                UseShellExecute = true
            }))
    ];

    private void InitCommunicatorBasedOnConfig(string? password = null)
    {
        if (Config.Data.ServerUri != null && Config.Data.Username != null)
        {
            if (communicator != null)
                communicator.Dispose();
            communicator = new Communicator(
                Config.Data.ServerUri,
                Config.Data.KeycloakRefreshToken, (string newKeycloakRefreshToken) =>
                {
                    Config.Data.KeycloakRefreshToken = newKeycloakRefreshToken;
                    Config.Save();
                },
                stateChanged: state =>
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
                            viewModel.ConnectionState = state == CommsState.Disconnected ? "Disconnected" : $"Connected to {Config.Data.Username}@{Config.Data.ServerUri.Split("//").Last()}";
                    });
                },
                onPayloadRequestError: e =>
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        popupManager?.Show("Error Connecting to Server!", e.Message);
                    });
                }
            );
            if (password != null)
            {
                communicator.DoNewLogIn(Config.Data.Username, password);
                Config.Data.KeycloakRefreshTokenForAndroidWidget = communicator.GetSeparateSessionRefreshToken(Config.Data.Username, password);
            }
            communicator.RequestLoopInterval = 5000;
            communicator.StartRequestLoop(OnPayloadReceived);
        }
    }

    private void Handle_Communicator_On_MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        LoadConfig();

        Payload GetNewPayload() => new(Config.Data.CurrentUsersNotePayload()?.SaveTime ?? DateTime.Now, Config.Data.CurrentUsersNotePayload()?.Notes ?? []);
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

    private void PasswordTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginButton_Click(sender, e);
        }
    }

    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (viewModel != null)
                viewModel.AddDebugText($"LoginButton_PointerPressed");
            var parent = (sender as Button)?.Parent;
            var server = viewModel?.LoginServerUri;
            var username = viewModel?.LoginServerUsername;
            var password = viewModel?.LoginPassword;
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                popupManager?.Show("Login Error", "Please fill in all fields.");
                return;
            }

            Config.Data.ServerUri = server;
            Config.Data.Username = username;

            InitCommunicatorBasedOnConfig(password);

            Config.Save();
        }
        catch (Exception ex)
        {
            if (viewModel != null)
                viewModel.AddDebugText(ex.ToString());
            popupManager?.Show("Login Error", ex.Message);
        }
    }

    private void RegisterButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var server = viewModel?.LoginServerUri;
            if (string.IsNullOrWhiteSpace(server))
                throw new Exception("You need to set the Connect URL of the note server first!");
            var keyCloakAddress = Communicator.GetKeyCloakAddress(server!, new System.Net.Http.HttpClient());
            var url = keyCloakAddress.KeycloakRealmUrl + "/account";

            var action = OpenUrlActionsOnSystem.FirstOrDefault(x => x.IsCurrentOperatingSystem);

            if (action != null)
            {
                action.OpenUrl(url);
            }
            else
            {
                popupManager?.Show("Platform not supported", "This platform cant show links :(\nPlease open " + url);
            }
        }
        catch (Exception ex)
        {
            popupManager?.Show("Registration Error", ex.Message);
        }
    }
    private void ShowLogsTextBlock_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var lines = File.ReadAllLines(Config.PersonalPath + "log.txt");
            lines.Reverse();
            popupManager?.Show("Logs", string.Join(Environment.NewLine, lines));
        }
        catch (Exception ex)
        {
            if (viewModel != null)
                viewModel.AddDebugText(ex.ToString());
            popupManager?.Show("Error Showing Logs", "Could not read log file: " + ex.Message);
        }
    }

    void OnPayloadReceived(string receivedText, Payload? payload)
    {
        bool validPayload = false;
        lock (Config.Data)
        {
            validPayload = payload != null &&
                payload.Checksum == payload.GenerateChecksum() &&
                (Config.Data.CurrentUsersNotePayload == null ||
                    Config.Data.CurrentUsersNotePayload()?.SaveTime < payload.SaveTime);

            if (validPayload)
            {
                Config.Data.CurrentUsersNotePayload()?.Notes = payload!.Notes;
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
                Config.Data.CurrentUsersNotePayload()?.SaveTime = DateTime.Now;

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
            if (Config.Data.CurrentUsersNotePayload()?.Notes == null || Config.Data.CurrentUsersNotePayload()?.Notes.Count == 0)
                Config.Data.CurrentUsersNotePayload()?.Notes = [Note.EmptyNote()];
            viewModel.LoadNew(Config.Data.CurrentUsersNotePayload()?.Notes ?? [Note.EmptyNote()]);
        }
    }
}