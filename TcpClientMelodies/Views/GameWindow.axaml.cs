using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MelodyLibrary;
using TcpClientMelodies.Services;

namespace TcpClientMelodies.Views;

public partial class GameWindow : Window
{
    private readonly ClientService? _clientService;
    private readonly AudioService? _audioService;
    
    private int _currentRound = 0;
    private bool _highlightDuringPlayback;

    private bool _closedByDisconnect;
    
    public bool ClosedByDisconnect => _closedByDisconnect;
    
    // состояния игры
    private enum GameState
    {
        Training,   // пианино доступно, звуки не сохраняются
        Waiting,      // пинино недоступно
        Recording  // пианино доступно, звуки сохраняются
    }

    // мелодия из раунда и количество нот в ней
    private readonly List<string> _currentRoundSequence = new(); // правильная мелодия
    private int _maxNotesInRound;
    
    private GameState _currentState = GameState.Waiting;
    private readonly List<string> _pressedKeys = new(); // текущий ответ игрока
    private int _replayCount = 0;

    // карта нот для воспроизведения (имя кнопки - файл)
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

    public GameWindow()
    {
        InitializeComponent();
    }

    public GameWindow(ClientService client, AudioService audio) : this()
    {
        _clientService = client;
        _audioService = audio;

        SetupUI();
        SubscribeToNetwork();
    }
    
    private void SubscribeToNetwork()
    {
        // подписываемся на события клиента
        _clientService.PacketReceived += OnPacketReceived;
        _clientService.Disconnected += OnDisconnected;
    }

    private void OnPacketReceived(PackageType type, string payload)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            switch (type)
            {
                case PackageType.RoundStarted:
                    HandleRoundStarted(payload);       // payload: "round|C,E,G"
                    break;

                case PackageType.RoundResult:
                    HandleRoundEnded(payload);       // payload: "round|status|delta|total"
                    break;

                case PackageType.GameResult:
                    HandleResultGame(payload);         // payload: "WIN|10:5" и т.п.
                    break;
            }
        });
    }
    
    private void OnDisconnected()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _closedByDisconnect = true;
            Close();
        });
    }

    private void SetupUI()
    {
        // привязываем ноты
        foreach (var noteName in _notePaths.Keys)
        {
            var btn = this.FindControl<Button>(noteName);
            if (btn != null)
            {
                btn.Click += (_, _) => OnPianoKeyClick(btn);
            }
        }

        // кнопки управления
        PlayTaskButton.Click += OnPlayTaskClicked;
        StartRecordingButton.Click += OnStartRecordingClicked;
        ClearButton.Click += OnClearClicked;
        SendButton.Click += OnSendClicked;
        ListenRecordingButton.Click += OnListenClicked;
        RoundText.Text = "-";
        PlayTaskButton.IsEnabled = false;
            
        var cached = _clientService.ConsumeLastRoundStarted();
        if (!string.IsNullOrEmpty(cached))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => HandleRoundStarted(cached)); // в UI-потоке
        }
    }

    private async void OnPlayTaskClicked(object? sender, RoutedEventArgs e)
    {
        _currentState = GameState.Waiting;
        PlayTaskButton.IsEnabled = false;
        //Piano.IsEnabled = false;
        Piano.IsHitTestVisible = false;
        
        _replayCount += 1;
        ReplayCountText.Text = $"Прослушано: {_replayCount}";


        if (_pressedKeys.Count >= _maxNotesInRound)
        {
            Piano.IsEnabled = true;
        }
        foreach (var note in _currentRoundSequence)
        {
            var btn = this.FindControl<Button>(note);

            if (_highlightDuringPlayback && btn != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => btn.Classes.Add("lit"));
            }

            if (_notePaths.TryGetValue(note, out var path) && !string.IsNullOrEmpty(path))
            {
                _audioService?.PlaySound(path);
            }

            await Task.Delay(800);

            if (_highlightDuringPlayback && btn != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => btn.Classes.Remove("lit"));
            }
        }
        
        _currentState = GameState.Training;
        
        if (_pressedKeys.Count >= _maxNotesInRound)
        {
            Piano.IsEnabled = false;
        }
        
        //Piano.IsEnabled = true;
        Piano.IsHitTestVisible = true;
        PlayTaskButton.IsEnabled = true;
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        _pressedKeys.Clear();
  
        Piano.IsEnabled = true;
        StartRecordingButton.IsVisible = true;
        RecordingControlsPanel.IsVisible = false;
        _currentState = GameState.Training;
        UpdateLog("Ввод очищен. Начните запись заново.");
    }

    // обработчик нажатия на пианино
    private void OnPianoKeyClick(Button btn)
    {
        string noteName = btn.Name ?? "?";
        string path = _notePaths.ContainsKey(noteName) ? _notePaths[noteName] : "";
        
        if (!string.IsNullOrEmpty(path))
        {
            _audioService?.PlaySound(path);
        }

        // запись идет только в режиме recording
        if (_currentState == GameState.Recording)
        {
            _pressedKeys.Add(noteName);
            
            if (_pressedKeys.Count >= _maxNotesInRound)
            {
                Piano.IsEnabled = false;
                //Piano.IsHitTestVisible = false;
                _currentState = GameState.Waiting;
                UpdateLog("Лимит нот! Нажмите 'сброс' или 'отправить'.");
                return;
            }
        }
    }
    
    // "round|C,E,G|h|mode"
    private void HandleRoundStarted(string payload)
    {
        var parts = payload.Split('|');
        if (parts.Length < 2) return;

        if (!int.TryParse(parts[0], out _currentRound)) return;

        var notes = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
        _highlightDuringPlayback = parts[2] == "1";
        int _roundMode = (parts.Length >= 4 && int.TryParse(parts[3], out var mode)) ? mode : 0;
        
        _currentState = GameState.Training;
        _pressedKeys.Clear();
        _currentRoundSequence.Clear();
        _currentRoundSequence.AddRange(notes);
        _maxNotesInRound = _currentRoundSequence.Count;
        _replayCount = 0;

        RoundText.Text = $"{_currentRound} / 10 (Нот: {_maxNotesInRound})";
        ReplayCountText.Text = "Прослушано: 0";
        
        GameStatusText.Text = _roundMode switch
        {
            1 => "Раунд: повторите мелодию на ноту выше.",
            2 => "Раунд: повторите мелодию в обратном порядке.",
            _ => "Раунд начался. Повторите мелодию по нотам."
        };
        
        UpdateLog("Нажмите 'Воспроизвести задание', затем тренируйтесь или начинайте запись.");

        PlayTaskButton.IsEnabled = true;
        StartRecordingButton.IsVisible = true;
        RecordingControlsPanel.IsVisible = false;
        Piano.IsEnabled = true;
    }
    
    // "round|status|delta|total"
    private void HandleRoundEnded(string payload)
    {
        var parts = payload.Split('|');
        if (parts.Length < 4) return;

        if (!int.TryParse(parts[0], out var round)) return;
        var status = parts[1];                 // OK / FAIL / PARTIAL
        if (!int.TryParse(parts[2], out var delta)) delta = 0;
        if (!int.TryParse(parts[3], out var total)) total = 0;
        
        // гасим запись до следующего RoundStarted
        ScoreText.Text = total.ToString();
        _currentState = GameState.Waiting;
        StartRecordingButton.IsVisible = false;
        RecordingControlsPanel.IsVisible = false;
        PlayTaskButton.IsEnabled = false;
        Piano.IsEnabled = false;
    }
    
    // прослушивание собственного ответа
    private async void OnListenClicked(object? sender, RoutedEventArgs e)
    {
        if (_pressedKeys.Count == 0)
        {
            UpdateLog("Вы еще ничего не записали.");
            return;
        }
        var previousState = _currentState;
        UpdateLog("Воспроизведение вашего ответа...");

        _currentState = GameState.Waiting;
        
        
        Piano.IsEnabled = true;
        Piano.IsHitTestVisible = false;
        
        foreach (var note in _pressedKeys)
        {
            var btn = this.FindControl<Button>(note);

            if (btn != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => btn.Classes.Add("lit_player"));
            }

            if (_notePaths.TryGetValue(note, out var path) && !string.IsNullOrEmpty(path))
            {
                _audioService?.PlaySound(path);
            }

            await Task.Delay(800);

            if (btn != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => btn.Classes.Remove("lit_player"));
            }
        }

        // после проверки возвращаемся к записи
        _currentState = GameState.Recording;
        UpdateLog("Можете перезаписать или отправить ответ.");
        if (_pressedKeys.Count >= _maxNotesInRound)
        {
            Piano.IsEnabled = false;
        }
        
        Piano.IsHitTestVisible = true;
    }
    
    // "WIN|10:5"
    private async void HandleResultGame(string payload)
    {
        var parts = payload.Split('|', 2);
        if (parts.Length != 2)
        {
            return;
        }

        var status = parts[0];         // WIN / LOSE / DRAW
        var scoreParts = parts[1].Split(':');
        int my = int.Parse(scoreParts[0]);
        int opp = int.Parse(scoreParts[1]);

        // блокируем текущий UI
        _currentState = GameState.Waiting;
        PlayTaskButton.IsEnabled = false;
        StartRecordingButton.IsVisible = false;
        RecordingControlsPanel.IsVisible = false;
        Piano.IsEnabled = false;

        var dlg = new ResultWindow();
        dlg.SetData(status, my, opp);
        IsEnabled = false;

        dlg.ExitRequested += async () =>
        {
            // шлём выход из комнаты и закрываем игру
            var pkt = new MelodyPackageBuilder(Array.Empty<byte>(), PackageType.ClientLeave).Build();
            await _clientService!.SendPacketAsync(pkt);
            dlg.Close();
            Close();
        };

        // открыть из UI-потока (мы уже в UI)
        await dlg.ShowDialog(this);
    }


    private async void OnSendClicked(object? sender, RoutedEventArgs e)
    {
        if (_pressedKeys.Count == 0)
        {
            UpdateLog("Ответ пуст. Нечего отправлять.");
            return;
        }

        _currentState = GameState.Waiting;
        RecordingControlsPanel.IsVisible = false;
        Piano.IsEnabled = false;
        
        // payload: "<round>|<replays>|C,E,G"
        string payloadStr = $"{_currentRound}|{_replayCount}|{string.Join(",", _pressedKeys)}";
        var packet = new MelodyPackageBuilder(System.Text.Encoding.UTF8.GetBytes(payloadStr), PackageType.ClientAnswer).Build();
        await _clientService!.SendPacketAsync(packet);
        UpdateLog("Ответ отправлен. Ждем соперника...");
        _pressedKeys.Clear();
    }


    private void OnStartRecordingClicked(object? sender, RoutedEventArgs e)
    {
        _currentState = GameState.Recording;
        _pressedKeys.Clear();

        StartRecordingButton.IsVisible = false;
        RecordingControlsPanel.IsVisible = true;

        UpdateLog("Идет запись...");
    }

    // обновляет лог в нижней части экрана
    private void UpdateLog(string text)
    {
        LogText.Text = text;
    }
}
