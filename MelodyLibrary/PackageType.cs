namespace MelodyLibrary;

public enum PackageType : byte
{
    // Клиент - сервер
    CreateSession = 0x10, // подключаемся к новой сессии "CREATE_SESSION:" "0x10:"
    JoinSession = 0x11, // подключаемся по коду "JOIN_SESSION:ABCD"
    ClientReady = 0x12, // клиент готов к началу раундов "CLIENT_READY:"
    ClientAnswer = 0x13, // клиент отправляет ответ "CLIENT_ANSWER:
    
    // Сервер - клиент
    SessionCreated = 0x14, // "SESSION_CREATED:ABCD"
    SessionJoined = 0x15, // "SESSION_JOINED:ABCD"
    SessionError = 0x16, // "SESSION_ERROR:Error"
    
    RoundStarted = 0x17, // "ROUND_START:1|C,E,G"
    RoundResult = 0x18, // "ROUND_RESULT:1|Fail|10|100" <round>|<status>|<delta>|<total>
    //  1, 2 или 3 | Fail, Partial или OK | количество очков в этом раунде | всего очков у него
    
    GameResult = 0x19, 
    ClientLeave = 0x19
}