using MelodyLibrary;

namespace TcpServerMelodies;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandAttribute(PackageType command) : Attribute
{
    public PackageType Command { get; set; } = command;
}
