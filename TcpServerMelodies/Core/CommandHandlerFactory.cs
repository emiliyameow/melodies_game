using MelodyLibrary;

namespace TcpServerMelodies;

using System.Reflection;

public static class CommandHandlerFactory
{
    private static readonly Lazy<Dictionary<PackageType, ICommandHandler>> CommandHandlers =
        new(BuildAllHandlers);

    private static Dictionary<PackageType, ICommandHandler> BuildAllHandlers()
    {
        var allHandlers = new Dictionary<PackageType, ICommandHandler>();

        var handlerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(ICommandHandler).IsAssignableFrom(t));

        foreach (var handlerType in handlerTypes)
        {
            var command = handlerType.GetCustomAttribute<CommandAttribute>()!.Command ;
            var handler = (ICommandHandler)Activator.CreateInstance(handlerType)!;
            
            allHandlers.Add(command, handler);
        }
        return allHandlers;
        
    }

    public static ICommandHandler? GetHandler(PackageType command)
        => CommandHandlers.Value.TryGetValue(command, out var handler)
            ? handler
            : throw new NotSupportedException($"Не найден handler для {command}");
}
