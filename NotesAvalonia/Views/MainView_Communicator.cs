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
    public Communicator? communicator { get; private set; } = null;
    DateTime lastSaveTime = DateTime.MinValue;
    public bool unsavedChanges = false;

    private void InitCommunicatorBasedOnConfig(string? password = null)
    {
        if (Config.Data.ServerUri != null && Config.Data.Username != null)
        {
            if (communicator != null)
                communicator.Dispose();
            communicator = new Communicator(
                Config.Data.ServerUri,
                Config.Data.Username,
                Config.Data.KeycloakRefreshToken, (string newKeycloakRefreshToken) =>
                {
                    Config.Data.KeycloakRefreshToken = newKeycloakRefreshToken;
                    Config.Save();
                },
                (CommsState state) =>
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
                }
            );
            if (password != null)
                communicator.DoNewLogIn(Config.Data.Username, password);
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

    private void PasswordTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginButton_Click(sender, e);
        }
    }

    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        if (viewModel != null)
            viewModel.AddDebugText($"LoginButton_PointerPressed");
        var parent = (sender as Button)?.Parent;
        var server = viewModel?.LoginServerUri;
        var username = viewModel?.LoginServerUsername;
        var password = viewModel?.LoginPassword;
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowPopup("Login Error", "Please fill in all fields.");
            return;
        }

        Config.Data.ServerUri = server;
        Config.Data.Username = username;
        try
        {
            InitCommunicatorBasedOnConfig(password);
        }
        catch (Exception ex)
        {
            ShowPopup("Login Error", ex.ToString());
        }
        Config.Save();
    }

    private void RegisterButton_Click(object? sender, RoutedEventArgs e)
    {
        var parent = (sender as Button)?.Parent;
        var server = viewModel?.LoginServerUri;
        try
        {
            var keyCloakAddress = Communicator.GetKeyCloakAddress(server!, new System.Net.Http.HttpClient());
            Process.Start(new ProcessStartInfo
            {
                FileName = keyCloakAddress.KeycloakRealmUrl + "/account",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowPopup("Registration Error", ex.Message);
        }
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