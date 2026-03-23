using Avalonia;
using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TcpClientMelodies.Services;
using TcpClientMelodies.Views;

namespace TcpClientMelodies;

public partial class App : Application
{
    // сервисы живут глобально пока работает приложение
    private ClientService _clientService;
    private AudioService _audioService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // инициализируем сервисы один раз при старте
            _clientService = new ClientService();
            _audioService = new AudioService();
            
            // пробуем инициализировать бас (аудио) сразу
            if (!_audioService.Initialize())
            {
                Console.WriteLine("ошибка: не удалось запустить аудио драйвер");
            }

            // подписываемся на выход из приложения, чтобы почистить ресурсы
            desktop.Exit += OnExit;

            // запускаем сценарий: сначала показываем окно входа
            ShowLoginWindow(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // логика открытия окна входа
    private void ShowLoginWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // создаем окно и передаем ему только нужный сервис
        var loginWindow = new LoginWindow(_clientService);

        // подписываемся на событие успеха
        loginWindow.OnLoginSuccess += () =>
        {
            // если вход успешен -> открываем игру
            ShowLobbyWindow(desktop);
            loginWindow.Close();
        };

        // назначаем главным и показываем
        desktop.MainWindow = loginWindow;
        loginWindow.Show();
    }
    
    private void ShowLobbyWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var lobbyWindow = new LobbyWindow(_clientService);

        lobbyWindow.OnSessionReady += () =>
        {
            // после успешного присоединения к сессии -> тренировка
            ShowTrainingWindow(desktop);
            lobbyWindow.Close();
        };

        desktop.MainWindow = lobbyWindow;
        lobbyWindow.Show();
    }

    // логика открытия окна игры
    private void ShowGameWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // создаем окно игры, передаем оба сервиса
        var gameWindow = new GameWindow(_clientService, _audioService);
        
        gameWindow.Closed += (_, _) =>
        {
            // различаем, почему окно закрыто
            if (gameWindow.ClosedByDisconnect)
            {
                // тут открываем только LoginWindow
                ShowLoginWindow(desktop);
            }
            else
            {
                // обычное закрытие -> лобби
                ShowLobbyWindow(desktop);
            }
        };
        
        // обновляем MainWindow, чтобы приложение не закрылось при закрытии логина
        desktop.MainWindow = gameWindow;
        gameWindow.Show();
    }
    
    private void ShowTrainingWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var trainingWindow = new TrainingWindow(_audioService, _clientService);

        // когда игрок нажмет "я готов"
        trainingWindow.OnStartGame += () =>
        {
            ShowGameWindow(desktop);
            trainingWindow.Close();
        };

        desktop.MainWindow = trainingWindow;
        trainingWindow.Show();
    }
    /*
    private void ShowResultWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var resultWindow = new ResultWindow(_clientService);
        
        resultWindow.OnExit += () =>
        {
            // если вход успешен -> открываем игру
            ShowLobbyWindow(desktop);
            resultWindow.Close();
        };
        
        resultWindow.OnStart += () =>
        {
            // если вход успешен -> открываем игру
            ShowLobbyWindow(desktop);
            resultWindow.Close();
        };

        // создаем окно игры, передаем оба сервиса
        //var gameWindow = new GameWindow(_clientService, _audioService);
        
        // обновляем MainWindow, чтобы приложение не закрылось при закрытии логина
        desktop.MainWindow = resultWindow;
        resultWindow.Show();
    }
*/
    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        // чистим ресурсы перед смертью игры
        _audioService.Dispose();
        _clientService.Dispose();
    }
}
