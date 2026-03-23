using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TcpClientMelodies.Services;

namespace TcpClientMelodies.Views;

public partial class LoginWindow : Window
{
    private readonly ClientService _clientService;

    public event Action? OnLoginSuccess;

    public LoginWindow(ClientService clientService)
    {
        InitializeComponent();
        _clientService = clientService;

        ConnectButton.Click += OnConnectClicked;
    }

    private async void OnConnectClicked(object? sender, RoutedEventArgs e)
    {
        ConnectButton.IsEnabled = false;
        ConnectButton.Content = "Подключение...";
        StatusText.Text = "";

        string result = await _clientService.ConnectToServerAsync();

        if (_clientService.IsConnected)
        {
            // здесь можно вывести зеленый статус, если хотите
            StatusText.Text = "Успешное подключение";
            OnLoginSuccess?.Invoke();
        }
        else
        {
            StatusText.Text = $"Ошибка подключения: {result}";
            ConnectButton.IsEnabled = true;
            ConnectButton.Content = "Подключиться";
        }
    }
}