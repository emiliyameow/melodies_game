## Melody Game With Sockets (TCP)

Игра “Музыкальная битва”: два игрока подключаются по TCP, создают/вступают в сессию по коду комнаты и соревнуются, повторяя мелодии на виртуальном пианино. 

Клиент — Avalonia-приложение с проигрыванием MP3 (через `ManagedBass`).
Сервер — простой TCP-сервер, который обменивается бинарными пакетами фиксированного формата и управляет раундами/начислением очков.

--- 

## Требования

- .NET 9 (`net9.0`)
- Для клиента: Avalonia UI (`Avalonia`, `Avalonia.Desktop`) и `ManagedBass` (`ManagedBass` NuGet)
- Звуковые ресурсы: `TcpClientMelodies/resources/sounds/*` (MP3)

--- 
## Как устроен проект

В solution `GameWithSockets.sln` входят проекты:

- `TcpServerMelodies` — TCP сервер игры
- `TcpClientMelodies` — клиент (UI + аудио + сетевой клиент)
- `MelodyLibrary` — общая “библиотека протокола” (формат пакетов и `PackageType`)

Основные классы:

- Сервер:
  - `MelodyGameServer` — принимает сокеты и читает пакеты
  - `MelodyPackageReader` / `MelodyPackageBuilder` (из `MelodyLibrary`) — низкоуровневый протокол пакетов
  - `CommandHandlerFactory` + `CommandAttribute` — маршрутизация входящих команд к обработчикам
  - `CreateSessionCommandHandler`, `JoinSessionCommandHandler`, `ClientReadyCommandHandler`, `ClientAnswerCommandHandler` — команды протокола
  - `RoundHelper` — генерация раундов и подсчёт очков
- Клиент:
  - `ClientService` — TCP-клиент, цикл чтения и события для UI
  - `GameWindow` — основная логика раундов (воспроизведение задания, запись ответа, отправка)
  - `LobbyWindow` — создание/вступление в комнату
  - `TrainingWindow` — “подготовка” игрока и отправка `ClientReady`
  - `AudioService` — воспроизведение звуков


--- 

## Запуск

### 1) Запустить сервер

Сервер слушает `127.0.0.1:8888`:

- `TcpServerMelodies/Program.cs`

Запуск (из корня репозитория):

```bash
dotnet build
dotnet run --project TcpServerMelodies/TcpServerMelodies.csproj
```

### 2) Запустить клиента (одновременно два раза)

Клиент — это GUI, поэтому обычно запускают два экземпляра приложения:

```bash
dotnet run --project TcpClientMelodies/TcpClientMelodies.csproj
```

### 3) Сценарий пользователя

1. В клиенте: `LoginWindow` -> кнопка `Подключиться`
2. В `LobbyWindow`:
   - один игрок нажимает `Создать игру` (получает код комнаты)
   - второй игрок вводит код в `Введите код комнаты` и нажимает `Подключиться`
3. Игроки переходят в `TrainingWindow` и нажимают `Готов к игре` — клиент отправляет пакет `ClientReady`
4. Сервер, когда оба готовы, отправляет `RoundStarted`, и клиенты переходят в `GameWindow`
5. В `GameWindow`:
   - `Воспроизвести задание` — играет задание (и подсвечивает ноты, если это предусмотрено раундом)
   - `Начать запись` + нажатия на клавиши — формирует ответ
   - `Воспроизвести ответ` — прослушать свой записанный ответ
   - `ОТПРАВИТЬ` — отправить ответ серверу
6. После `GameResult` показывается `ResultWindow`; кнопка `Выйти в лобби` отправляет `ClientLeave` и закрывает окно.

--- 

## Сетевой протокол

Клиент и сервер обмениваются пакетами бинарного формата.

### Формат пакета

`MelodyPackageBuilder` / `MelodyPackageReader` задают следующий layout:

- `Start` (1 байт): `0x02`
- `Command` (1 байт): тип команды (`PackageType`)
- `Length` (1 байт): длина `payload` в байтах (максимум `124`)
- `Payload` (0..124 байт): содержимое команды
- `End` (1 байт): `0x03`

Все текстовые payload-строки кодируются как `UTF-8`.

### Перечень команд (`PackageType`)

Команды перечислены в `MelodyLibrary/PackageType.cs`.

Коротко (порядок важен для понимания payload):

| Тип | Направление | Ожидаемый payload (строка) |
|---|---|---|
| `CreateSession` (0x10) | клиент -> сервер | payload пустой |
| `JoinSession` (0x11) | клиент -> сервер | `"ABCD"` |
| `ClientReady` (0x12) | клиент -> сервер | payload пустой |
| `ClientAnswer` (0x13) | клиент -> сервер | `"<round>|<replays>|C,E,G"` |
| `SessionCreated` (0x14) | сервер -> клиент | `"ABCD"` |
| `SessionJoined` (0x15) | сервер -> клиент | `"ABCD"` |
| `SessionError` (0x16) | сервер -> клиент | `"NOT_FOUND"` / `"FULL"` / `"ALREADY_IN_SESSION"` |
| `RoundStarted` (0x17) | сервер -> клиент | `"round|C,E,G|<highlight>|<mode>"` |
| `RoundResult` (0x18) | сервер -> клиент | `"round|OK/FAIL/PARTIAL|delta|total"` |
| `GameResult` (0x19) | сервер -> клиент | `"WIN/LOSE/DRAW|my:opponent"` |
| `ClientLeave` (0x19) | клиент -> сервер | (отправляется из UI) |

Важно: в текущей версии `ClientLeave` и `GameResult` имеют одинаковое значение (`0x19`), а серверный хендлер для `ClientLeave` отсутствует (см. раздел “Замечания/ограничения”).

### Примеры payload

#### `JoinSession` (`0x11`)

- payload: `ABCD` (4 символа, например `AG5J`)

#### `RoundStarted` (`0x17`)

- payload: `"<round>|<notesString>|<highlight>|<mode>"`
- `notesString`: список нот через запятую, например `C,D,E`
- `highlight`: `1` (подсвечивать ноты при проигрывании) или `0`
- `mode`:
  - `0` — обычное повторение по позициям
  - `1` — повторить мелодию на ноту выше
  - `2` — повторить в обратном порядке

Клиент ожидает именно эту структуру (см. `GameWindow.HandleRoundStarted`).

#### `ClientAnswer` (`0x13`)

- payload: `"<round>|<replays>|C,E,G"`
- `replays` — сколько раз игрок нажал `Воспроизвести задание` в текущем раунде
- ноты разделяются запятыми

#### `RoundResult` (`0x18`)

- payload: `"<round>|<status>|<delta>|<total>"`
- `status`: `OK` / `FAIL` / `PARTIAL`
- `delta`: сколько очков прибавлено за раунд
- `total`: общий счёт игрока

--- 

## Игровая логика (раунды и очки)

Реализация в `TcpServerMelodies/Helpers/RoundHelper.cs`.

### Ноты

Используются 7 нот:

- `C, D, E, F, G, A, B`

### Длина мелодий по раундам

- Раунды 1..6: длина меняется (от 3 до 5)
- Раунды 7..10: длина фиксирована `3` (`baseLength`)

### Типы раундов

- Раунды `1..6`: обычное сравнение по позициям (`mode=0`)
- Раунды `7..8`: “повыше” (`mode=1`) — каждая нота задачи заменяется на соседнюю выше (например `C -> D`)
- Раунды `9..10`: “в обратном порядке” (`mode=2`) — сравнение делается после разворота задания

### Подсветка при проигрывании задания

Клиент рисует подсветку ноты при проигрывании, если в `RoundStarted` `highlight=1`.

В `RoundHelper.StartRoundAsync` highlight включается для:

- `round <= 3`
- `round == 7`
- `round == 9`

### Начисление очков

1. За каждую правильно угаданную ноту игрок получает `+20`.
2. Штраф за прослушивания (повторы):
   - `-5` за каждое прослушивание сверх первого
   - считается по полю `<replays>`: `penalty = max(0, replays - 1) * 5`
3. Итог за раунд:
   - `delta = max(0, base - penalty)` (для случаев, когда base=0 итог также будет 0)
4. После `round==10` сервер завершает матч и отправляет `GameResult`.

--- 

## Архитектура и потоки выполнения (коротко)

### Сервер

1. `MelodyGameServer.StartServer` принимает TCP подключения.
2. Для каждого подключения запускается `HandleConnection` в отдельном `Task`.
3. В цикле:
   - читается следующий пакет `MelodyPackageReader.ReadNextAsync`
   - по `packet.Type` ищется обработчик `CommandHandlerFactory.GetHandler`
   - вызывается `handler.Invoke(player, sessions, payload)`

### Клиент

1. `ClientService.ReceiveLoop` читает пакеты и поднимает событие:
   - `PacketReceived(PackageType type, string payloadText)`
2. UI (`LoginWindow/LobbyWindow/GameWindow`) подписывается на события и переводит обработку в UI-поток:
   - `Dispatcher.UIThread.Post(...)`
3. Аудио:
   - `AudioService.Initialize()` вызывает `Bass.Init()`
   - `AudioService.PlaySound(path)` создаёт stream и запускает воспроизведение

--- 

## Замечания/ограничения (важно)

1. **Переход “хост -> TrainingWindow” может не происходить.**  
   В `LobbyWindow` событие `OnSessionReady` вызывается на `SessionJoined`, но не вызывается на `SessionCreated`.  
   Это может приводить к ситуации, когда игрок-хост не отправляет `ClientReady`, а сервер не запускает раунд.  
   (Технически это выглядит как зависание сценария “создал -> жду напарника -> не могу отправить готовность”.)

2. **Команды `ClientLeave` и `GameResult` имеют одинаковый код `0x19`.**  
   В `MelodyLibrary.PackageType`:
   - `GameResult = 0x19`
   - `ClientLeave = 0x19`
   При отправке `ClientLeave` сервер может попытаться обработать команду как “неизвестную” и разорвать соединение (у серверного кода нет handler’а для `ClientLeave`).

3. **Отключение клиента не “чистит” сессию.**  
   `MelodyGameServer` удаляет игрока из `_players`, но объект `GameSession` остаётся в `_sessionsByCode`. Это может приводить к зависанию комнаты или ошибкам при попытках отправлять пакеты отключившемуся игроку.

--- 

## Где смотреть код

- Протокол: `MelodyLibrary/MelodyPackageReader.cs`, `MelodyLibrary/MelodyPackageBuilder.cs`, `MelodyLibrary/PackageType.cs`
- Серверная логика: `TcpServerMelodies/Core/*`, `TcpServerMelodies/Handlers/*`, `TcpServerMelodies/Helpers/*`
- UI и игровой клиент: `TcpClientMelodies/Views/*` и `TcpClientMelodies/Services/*`

