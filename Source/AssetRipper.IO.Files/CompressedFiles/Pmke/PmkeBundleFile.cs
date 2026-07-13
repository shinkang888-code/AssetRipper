using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.Streams.Smart;
using System.Security.Cryptography;

namespace AssetRipper.IO.Files.CompressedFiles.Pmke;

/// <summary>
/// A custom-encrypted AssetBundle wrapper used by some Unity games (e.g. "Princess Maker
/// Children of Revelation"). The file begins with the ASCII magic "PMKE", followed by a
/// version byte and a 16-byte IV, then an AES-128-CTR encrypted standard UnityFS bundle.
/// </summary>
/// <remarks>
/// Format (little detail, all offsets from the start of the file):
/// <list type="bullet">
/// <item>[0..4)  magic "PMKE" (0x50 0x4D 0x4B 0x45)</item>
/// <item>[4]     format version (currently 1)</item>
/// <item>[5..21) 16-byte AES-CTR IV / initial counter</item>
/// <item>[21..]  AES-128-CTR encrypted payload (a normal "UnityFS" bundle once decrypted)</item>
/// </list>
/// The keystream is produced by AES-ECB encrypting a 128-bit big-endian counter that starts
/// at the IV and increments by one for every 16-byte block, then XORed with the payload.
/// The AES-128 key is embedded (obfuscated and split) in the game's il2cpp code and is
/// therefore game/build specific.
/// </remarks>
public sealed class PmkeBundleFile : CompressedFile
{
	private static ReadOnlySpan<byte> PmkeMagic => "PMKE"u8;

	private const int HeaderSize = 21;
	private const int IVSize = 16;
	private const int KeySize = 16;
	private const byte SupportedVersion = 1;

	/// <summary>
	/// Environment variable holding the AES-128 key as 32 hex characters.
	/// </summary>
	/// <remarks>
	/// The key is game/build specific and is intentionally not embedded in source. Provide it at
	/// runtime, e.g. <c>AR_PMKE_KEY=&lt;32 hex characters&gt;</c>. The in-game derivation is
	/// SHA256(k4 || k1 || k5 || k3)[0..16] over four obfuscated arrays; dump the reassembled key
	/// from the running process to obtain it.
	/// </remarks>
	public const string KeyEnvironmentVariable = "AR_PMKE_KEY";

	public override void Read(SmartStream stream)
	{
		try
		{
			byte[] key = ResolveKey();
			byte[] buffer = Decrypt(stream, key);
			UncompressedFile = new ResourceFile(buffer, FilePath, Name);
		}
		catch (Exception ex)
		{
			UncompressedFile = new FailedFile()
			{
				Name = Name,
				FilePath = FilePath,
				StackTrace = ex.ToString(),
			};
		}
	}

	private static byte[] ResolveKey()
	{
		string? hex = Environment.GetEnvironmentVariable(KeyEnvironmentVariable)?.Trim();
		if (string.IsNullOrEmpty(hex))
		{
			throw new InvalidOperationException(
				$"This file is PMKE-encrypted, but no decryption key is configured. " +
				$"Set the {KeyEnvironmentVariable} environment variable to the 32-character hex AES-128 key.");
		}

		if (hex.Length != KeySize * 2 || !TryParseHex(hex, out byte[] key))
		{
			throw new InvalidOperationException(
				$"{KeyEnvironmentVariable} must be exactly {KeySize * 2} hex characters ({KeySize} bytes).");
		}

		return key;
	}

	private static bool TryParseHex(string hex, out byte[] bytes)
	{
		bytes = new byte[hex.Length / 2];
		for (int i = 0; i < bytes.Length; i++)
		{
			if (!byte.TryParse(hex.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
			{
				return false;
			}
		}
		return true;
	}

	internal static bool IsPmkeFile(Stream stream)
	{
		long remaining = stream.Length - stream.Position;
		if (remaining < HeaderSize)
		{
			return false;
		}

		long position = stream.Position;
		Span<byte> magic = stackalloc byte[PmkeMagic.Length];
		stream.ReadExactly(magic);
		stream.Position = position;
		return magic.SequenceEqual(PmkeMagic);
	}

	private static byte[] Decrypt(Stream stream, ReadOnlySpan<byte> key)
	{
		long start = stream.Position;

		Span<byte> header = stackalloc byte[HeaderSize];
		stream.ReadExactly(header);
		if (header[4] != SupportedVersion)
		{
			throw new NotSupportedException($"Unsupported PMKE version: {header[4]}");
		}

		byte[] iv = header.Slice(5, IVSize).ToArray();

		long payloadLength = stream.Length - start - HeaderSize;
		if (payloadLength < 0)
		{
			throw new EndOfStreamException("PMKE payload is truncated.");
		}

		byte[] payload = new byte[payloadLength];
		stream.ReadExactly(payload);

		DecryptCtrInPlace(payload, key, iv);
		return payload;
	}

	/// <summary>
	/// AES-128-CTR decryption performed in place, using AES-ECB to encrypt the counter blocks
	/// (the classic "SeekableAesStream" construction the game uses).
	/// </summary>
	private static void DecryptCtrInPlace(Span<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		using Aes aes = Aes.Create();
		aes.Mode = CipherMode.ECB;
		aes.Padding = PaddingMode.None;
		aes.Key = key.ToArray();

		Span<byte> counter = stackalloc byte[IVSize];
		iv.CopyTo(counter);
		Span<byte> keystream = stackalloc byte[IVSize];

		for (int offset = 0; offset < data.Length; offset += IVSize)
		{
			aes.EncryptEcb(counter, keystream, PaddingMode.None);

			int blockLength = Math.Min(IVSize, data.Length - offset);
			Span<byte> block = data.Slice(offset, blockLength);
			for (int i = 0; i < blockLength; i++)
			{
				block[i] ^= keystream[i];
			}

			IncrementCounter(counter);
		}
	}

	/// <summary>Increments a 128-bit big-endian counter by one.</summary>
	private static void IncrementCounter(Span<byte> counter)
	{
		// Explicitly unchecked: the project enables CheckForOverflowUnderflow, and the
		// 0xFF -> 0x00 wrap-around must not throw.
		for (int i = counter.Length - 1; i >= 0; i--)
		{
			byte incremented = unchecked((byte)(counter[i] + 1));
			counter[i] = incremented;
			if (incremented != 0)
			{
				break;
			}
		}
	}

	public override void Write(Stream stream)
	{
		throw new NotImplementedException();
	}
}
