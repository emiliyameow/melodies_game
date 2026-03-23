namespace MelodyLibrary;

public class MelodyPackageMeta
{
    public const int Start = 0x2;
    public const int End = 0x3;
    public const int PayloadMaxByte = 124;
    public const int CommandByteIndex = 1;
    public const int LengthByteIndex = 2;
    public const int PackagePayloadIndex = 3;
}

//start_index / command / length / payload / end - всего 128 байт