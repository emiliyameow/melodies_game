using System;
using Avalonia.Controls;
using ManagedBass;

namespace TcpClientMelodies.Services;

public class AudioService : IDisposable
{
    public bool Initialize()
    {
        return Bass.Init();
    }
    
    
    public void PlaySound(string filename)
    {
        // создаем поток для воспроизведения звука
        int stream = Bass.CreateStream(filename);
        if (stream != 0)
        {
            Bass.ChannelPlay(stream);
        }
    }
    
    public void Dispose()
    {
        // освобождаем ресурсы при закрытии, чтобы можно было воспроизвести звук сразу же при перезапуске
        Bass.Free();
    }
} 