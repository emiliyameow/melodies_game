using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using TcpClientMelodies.Services;

namespace TcpClientMelodies.Views;

public partial class ResultWindow : Window
{
    public event Action? ExitRequested;
    public ResultWindow()
    {
        InitializeComponent();
        ExitButton.Click += OnExitClicked;
    }

    public void SetData(string status, int myScore, int opponentScore)
    {
        
        StatusText.Text = status switch
        {
            "WIN"  => "Победа!",
            "LOSE" => "Поражение!",
            "DRAW" => "Ничья",
            _ => $"Итог: {status}"
        };
        
        var winBrush  = Brushes.ForestGreen;
        var loseBrush = Brushes.IndianRed;
        var drawBrush = Brushes.SteelBlue;
        
        switch (status)
        {
            case "WIN":
                StatusText.Text = "Победа!";
                StatusText.Foreground        = winBrush;
                MyScoreText.Foreground       = winBrush;
                OpponentScoreText.Foreground = loseBrush;
                break;

            case "LOSE":
                StatusText.Text = "Поражение!";
                StatusText.Foreground        = loseBrush;
                MyScoreText.Foreground       = loseBrush;
                OpponentScoreText.Foreground = winBrush;
                break;

            case "DRAW":
                StatusText.Text = "Ничья!";
                StatusText.Foreground        = drawBrush;
                MyScoreText.Foreground       = drawBrush;
                OpponentScoreText.Foreground = drawBrush;
                break;

            default:
                StatusText.Foreground        = Brushes.Black;
                MyScoreText.Foreground       = Brushes.Black;
                OpponentScoreText.Foreground = Brushes.Black;
                break;
        }
        MyScoreText.Text = myScore.ToString();
        OpponentScoreText.Text = opponentScore.ToString();
    }
    
    private void OnExitClicked(object? s, RoutedEventArgs e) => ExitRequested?.Invoke();
}