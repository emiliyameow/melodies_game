namespace MelodyLibrary;

public class MelodyPackageBuilder
{
    byte[] _package;
    
    public byte[] Build() => _package;
    
    public MelodyPackageBuilder(byte[] content, PackageType packageType)
    {
        if (content.Length > MelodyPackageMeta.PayloadMaxByte)
        {
            throw new ArgumentException("Превышено максимальное количество контента в пакете");
        }

        _package = new byte[4 + content.Length];
        
        CreateBasePackage(content, packageType);
    }

    
    private void CreateBasePackage(byte[] content, PackageType packageType)
    {
        _package[0] = MelodyPackageMeta.Start;
        _package[^1] = MelodyPackageMeta.End;
        _package[MelodyPackageMeta.CommandByteIndex] = (byte) packageType;
        _package[MelodyPackageMeta.LengthByteIndex] = (byte) content.Length;
        Array.Copy(content, 0, _package, MelodyPackageMeta.PackagePayloadIndex, content.Length);
    }
}