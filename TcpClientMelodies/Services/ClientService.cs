using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MelodyLibrary;

namespace TcpClientMelodies.Services;

public class ClientService : IDisposable
{
    private TcpClient? _client; 
    private string? _lastRoundStarted; 
    
    // одноразовое чтение буфера (вернет null, если нечего)
    public string? ConsumeLastRoundStarted()
        => Interlocked.Exchange(ref _lastRoundStarted, null);

    public bool IsConnected => _client != null && _client.Connected;
    
    // событие для UI: пришел пакет определенного типа с текстовым payload
    public event Action<PackageType, string>? PacketReceived;
    public event Action? Disconnected;
    
    // метод отправки готового пакета
    public async Task SendPacketAsync(byte[] packet)
    {
        if (!IsConnected)
        {
            throw new Exception("Ошибка: Сначала подключитесь к серверу!");
        }
        
        try
        {
            if (packet.Length == 0)
            {
                throw new Exception("Пустой пакет.");
            }
            
            // отправляем через array segment
            var segment = new ArraySegment<byte>(packet);
            await _client.Client.SendAsync(segment, SocketFlags.None);
        }
        catch (Exception ex)
        {
            _client.Close();
            throw new Exception($"Ошибка отправки: {ex.Message}\n");
        }
    }
    
    public async Task<string> ConnectToServerAsync()
    {
        try
        {
            // пересоздаем клиент для нового подключения
            _client?.Close();
            _client = new TcpClient();

            await _client.ConnectAsync("127.0.0.1", 8888);
            // фоновый цикл чтения
            _ = Task.Run(ReceiveLoop);
            
            return "Успешно подключено к серверу!";
        }
        catch (Exception ex)
        {
            return $"{ex.Message}";
        }
    }
    
     // цикл приема пакетов
     private async Task ReceiveLoop()
     {
         if (_client == null) return;

         var socket = _client.Client;
         var reader = new MelodyPackageReader(socket);

         try
         {
             while (true)
             {
                 var packet = await reader.ReadNextAsync(); // можно передать токен, если нужен отменяемый цикл
                 if (packet is null)
                 {
                     break; // сервер закрыл соединение
                 }

                 var type = packet.Type;
                 string payloadText = Encoding.UTF8.GetString(packet.Payload);

                 if (type == PackageType.RoundStarted)
                 {
                     Interlocked.Exchange(ref _lastRoundStarted, payloadText);
                 }

                 PacketReceived?.Invoke(type, payloadText);
             }
         }
         catch (Exception)
         {
             // разрыв соединения / ошибка чтения — по желанию можно оповестить UI
         }
         finally
         {
             Disconnected?.Invoke();
         }
     }

    
    // чтобы мгновенно очистить порт, который выделен
    public void Dispose()
    {
        _client.Dispose();
    }
}