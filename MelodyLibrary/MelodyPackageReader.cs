namespace MelodyLibrary;

using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public sealed class MelodyPackageReader
{
    private readonly Socket _socket;
    private readonly byte[] _buffer;
    
    public sealed record MelodyPacket(PackageType Type, byte[] Payload);


    public MelodyPackageReader(Socket socket)
    {
        _socket = socket;
        _buffer = new byte[4 + MelodyPackageMeta.PayloadMaxByte]; // максимум пакета
    }
    
    // читает следующий пакет. возвращает null, если клиент отключился.
    public async Task<MelodyPacket?> ReadNextAsync(CancellationToken token = default)
    {
        // читаем первые 3 байта: Start, Command, Length
        if (!await ReadExactAsync(_socket, _buffer, 0, 3, token))
            return null; // сокет закрыт

        if (_buffer[0] != MelodyPackageMeta.Start)
        {
            // можно бросить исключение или просто игнорировать
            throw new InvalidOperationException("Неверный стартовый байт пакета.");
        }

        var packetType = (PackageType)_buffer[MelodyPackageMeta.CommandByteIndex];
        byte length    = _buffer[MelodyPackageMeta.LengthByteIndex];

        if (length > MelodyPackageMeta.PayloadMaxByte)
            throw new InvalidOperationException($"Недопустимая длина payload: {length}");

        // читаем оставшуюся часть: payload + End
        int restSize = length + 1; // +1 байт конца
        if (!await ReadExactAsync(_socket, _buffer, 3, restSize, token))
        {
            return null;
        }

        // проверяем End
        if (_buffer[3 + length] != MelodyPackageMeta.End)
        {
            throw new InvalidOperationException("Неверный завершающий байт пакета.");
        }

        // 4. Копируем payload
        var payload = new byte[length];
        Array.Copy(_buffer, MelodyPackageMeta.PackagePayloadIndex, payload, 0, length);

        return new MelodyPacket(packetType, payload);
    }

    private static async Task<bool> ReadExactAsync(Socket socket, byte[] buffer,
        int offset, int count, CancellationToken token)
    {
        int totalRead = 0;

        while (totalRead < count)
        {
            int read = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer, offset + totalRead, count - totalRead),
                SocketFlags.None,
                token);

            if (read == 0)
            {
                // клиент закрыл соединение
                return false;
            }

            totalRead += read;
        }

        return true;
    }
}
