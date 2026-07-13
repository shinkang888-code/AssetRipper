using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.IO.Files.CompressedFiles.Pmke;

public sealed class PmkeBundleScheme : Scheme<PmkeBundleFile>
{
	public override bool CanRead(SmartStream stream)
	{
		return PmkeBundleFile.IsPmkeFile(stream);
	}
}
