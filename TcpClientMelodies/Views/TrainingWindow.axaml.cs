using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MelodyLibrary;
using TcpClientMelodies.Services;

namespace TcpClientMelodies.Views;

public partial class TrainingWindow : Window
{
    private readonly AudioService? _audioService;
    private readonly ClientService _clientService; // НОВОЕ

    public event Action? OnStartGame;// событие для перехода к игре
    
    // карта нот (такая же как в GameWindow)
    private readonly Dictionary<string, string> _notePaths = new()
    {
        {"C", "resources/sounds/C_do_long.mp3"},
        {"D", "resources/sounds/D_re_long.mp3"},
        {"E", "resources/sounds/E_mi_long.mp3"},
        {"F", "resources/sounds/F_fa_long.mp3"},
        {"G", "resources/sounds/G_sol_long.mp3"},
        {"A", "resources/sounds/A_la_long.mp3"},
        {"B", "resources/sounds/B_si_long.mp3"}
    };
    
    public TrainingWindow(AudioService audio, ClientService client)
    {
        InitializeComponent();
        _audioService = audio;
        _clientService = client;
        SetupUI();
    }

    private void SetupUI()
    {
        // привязываем все клавиши (звук всегда работает)
        foreach (var noteName in _notePaths.Keys)
        {
            var btn = this.FindControl<Button>(noteName);
            if (btn != null)
            {
                btn.Click += (_, _) => PlayNote(btn);
            }
        }

        StartGameButton.Click += OnReadyClicked;
    }
    
    private async void OnReadyClicked(object? sender, RoutedEventArgs e)
    {
        var readyButton = this.FindControl<Button>("ReadyButton");
        if (readyButton != null)
        {
            readyButton.IsEnabled = false;
            readyButton.Content = "Готов, ждем соперника...";
        }

        // отправляем серверу ClientReady
        var payload = Array.Empty<byte>(); // пустой payload
        var packet = new MelodyPackageBuilder(payload, PackageType.ClientReady).Build();
        await _clientService.SendPacketAsync(packet);

        // ждем RoundStarted от сервера 
        OnStartGame?.Invoke();
    }

    private void PlayNote(Button btn)
    {
        string noteName = btn.Name ?? "?";
        string path = _notePaths.ContainsKey(noteName) ? _notePaths[noteName] : "";

        if (!string.IsNullOrEmpty(path))
        {
            _audioService?.PlaySound(path);
        }
    }

    private void OnStartGameClicked()
    {
         OnStartGame?.Invoke(); // сообщаем App: "готов играть!"
    }
}