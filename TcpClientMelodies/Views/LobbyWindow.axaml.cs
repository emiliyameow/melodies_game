using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TcpClientMelodies.Services;
using MelodyLibrary;

namespace TcpClientMelodies.Views;

public partial class LobbyWindow : Window
{
    private readonly ClientService _client;

    // событие, чтобы снаружи открыть GameWindow
    public event Action? OnSessionReady;
    
    public LobbyWindow(ClientService client)
    {
        InitializeComponent();
        _client = client;

        SetupUI();
        SubscribeToNetwork();
    }

    private void SetupUI()
    {
        CreateSessionButton.Click += OnCreateSessionClicked;
        JoinSessionButton.Click += OnJoinSessionClicked;
    }

    private void SubscribeToNetwork()
    {
        _client.PacketReceived += OnPacketReceived;
    }

    private async void OnCreateSessionClicked(object? sender, RoutedEventArgs e)
    {
        CreateSessionButton.IsEnabled = false;
        JoinSessionButton.IsEnabled = false;
        JoinErrorText.Text = string.Empty;
        StatusText.Text = "Создаем игру на сервере...";

        // payload пустой, нам нужен только тип CreateSession
        var payload = Array.Empty<byte>();
        var packet = new MelodyPackageBuilder(payload, PackageType.CreateSession).Build();

        await _client.SendPacketAsync(packet);
    }

    private async void OnJoinSessionClicked(object? sender, RoutedEventArgs e)
    {
        string code = JoinCodeTextBox.Text?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            JoinErrorText.Text = "Введите код комнаты.";
            return;
        }

        JoinErrorText.Text = string.Empty;
        StatusText.Text = $"Пробуем подключиться к коду {code}...";
        JoinSessionButton.IsEnabled = false;
        CreateSessionButton.IsEnabled = false;

        var payload = Encoding.UTF8.GetBytes(code);
        var packet = new MelodyPackageBuilder(payload, PackageType.JoinSession).Build();

        await _client.SendPacketAsync(packet);
    }

    private void OnPacketReceived(PackageType type, string payload)
    {
        // вызываться будет из фонового потока, нужно уйти в UI-поток
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            switch (type)
            {
                case PackageType.SessionCreated:
                    HandleSessionCreated(payload);
                    break;

                case PackageType.SessionJoined:
                    HandleSessionJoined(payload);
                    break;

                case PackageType.SessionError:
                    HandleSessionError(payload);
                    break;

                case PackageType.RoundStarted:
                    // получили старт раунда — можем закрывать лобби и открывать игру
                    OnSessionReady?.Invoke();
                    break;
            }
        });
    }

    private void HandleSessionCreated(string code)
    {
        SessionCodeText.Text = code;
        HostInfoText.Text = "Поделитесь этим кодом с другом и ждите, пока он подключится.";
        StatusText.Text = $"Комната создана. Код: {code}";
        // хосту не нужно ничего делать, он просто ждет SessionJoined
    }

    private void HandleSessionJoined(string code)
    {
        StatusText.Text = $"Успешно подключились к комнате {code}. Ожидаем старт раунда...";
        JoinErrorText.Text = string.Empty;
        
        OnSessionReady?.Invoke();
    }

    private void HandleSessionError(string error)
    {
        // error может быть: NOT_FOUND, FULL, ALREADY_IN_SESSION и т.д.
        StatusText.Text = "Ошибка: " + error;

        switch (error)
        {
            case "NOT_FOUND":
                JoinErrorText.Text = "Комната с таким кодом не найдена.";
                break;
            case "FULL":
                JoinErrorText.Text = "Комната уже заполнена.";
                break;
            case "ALREADY_IN_SESSION":
                JoinErrorText.Text = "Вы уже находитесь в другой комнате.";
                break;
            default:
                JoinErrorText.Text = "Не удалось подключиться.";
                break;
        }

        // возвращаем возможность нажимать кнопки
        CreateSessionButton.IsEnabled = true;
        JoinSessionButton.IsEnabled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _client.PacketReceived -= OnPacketReceived;
        base.OnClosed(e);
    }
}
