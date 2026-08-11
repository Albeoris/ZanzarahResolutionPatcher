using System.Buffers.Binary;
using System.Text;
using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Services;

public sealed class PatchMetadataCodec
{
    public const uint CurrentVersion = 1;
    public const int ExpectedResolutionCount = 3;

    private const int TrailerSize = sizeof(ushort) + sizeof(uint) + sizeof(uint) + sizeof(uint);
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ZZRP");

    public PatchMetadata? Read(ReadOnlySpan<byte> fileBytes, out int executableLength)
    {
        executableLength = fileBytes.Length;

        if (fileBytes.Length < TrailerSize || !fileBytes[^Magic.Length..].SequenceEqual(Magic))
        {
            return null;
        }

        var trailerOffset = fileBytes.Length - TrailerSize;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(fileBytes[trailerOffset..]);
        var reserved = BinaryPrimitives.ReadUInt32LittleEndian(fileBytes[(trailerOffset + sizeof(ushort))..]);
        var version = BinaryPrimitives.ReadUInt32LittleEndian(
            fileBytes[(trailerOffset + sizeof(ushort) + sizeof(uint))..]);

        if (count != ExpectedResolutionCount)
        {
            throw new InvalidDataException(
                $"The patch metadata contains {count} resolutions; {ExpectedResolutionCount} were expected.");
        }

        if (reserved != 0)
        {
            throw new InvalidDataException("The patch metadata reserved field is not zero.");
        }

        if (version != CurrentVersion)
        {
            throw new InvalidDataException(
                $"Patch metadata version {version} is not supported. Supported version: {CurrentVersion}.");
        }

        var metadataSize = checked((count * Resolution.BinarySize) + TrailerSize);
        executableLength = fileBytes.Length - metadataSize;
        if (executableLength < 0)
        {
            throw new InvalidDataException("The patch metadata is truncated.");
        }

        var resolutions = new List<Resolution>(count);
        var offset = executableLength;
        for (var index = 0; index < count; index++)
        {
            var width = BinaryPrimitives.ReadUInt16LittleEndian(fileBytes[offset..]);
            var unused = BinaryPrimitives.ReadUInt16LittleEndian(fileBytes[(offset + sizeof(ushort))..]);
            var height = BinaryPrimitives.ReadUInt16LittleEndian(fileBytes[(offset + (sizeof(ushort) * 2))..]);

            if (width == 0 || height == 0 || unused != 0)
            {
                throw new InvalidDataException("The patch metadata contains an invalid resolution record.");
            }

            resolutions.Add(new Resolution(width, height));
            offset += Resolution.BinarySize;
        }

        if (resolutions.Distinct().Count() != resolutions.Count)
        {
            throw new InvalidDataException("The patch metadata contains duplicate resolution records.");
        }

        return new PatchMetadata(resolutions, version);
    }

    public byte[] Append(ReadOnlySpan<byte> executableBytes, IReadOnlyList<Resolution> resolutions)
    {
        ArgumentNullException.ThrowIfNull(resolutions);

        if (resolutions.Count != ExpectedResolutionCount)
        {
            throw new ArgumentException(
                $"Exactly {ExpectedResolutionCount} resolutions are required.", nameof(resolutions));
        }

        if (resolutions.Distinct().Count() != resolutions.Count)
        {
            throw new ArgumentException(
                "Resolution metadata records must be unique.", nameof(resolutions));
        }

        var result = new byte[checked(executableBytes.Length +
            (resolutions.Count * Resolution.BinarySize) + TrailerSize)];
        executableBytes.CopyTo(result);

        var offset = executableBytes.Length;
        foreach (var resolution in resolutions)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset), resolution.Width);
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + sizeof(ushort)), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(
                result.AsSpan(offset + (sizeof(ushort) * 2)), resolution.Height);
            offset += Resolution.BinarySize;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset), (ushort)resolutions.Count);
        offset += sizeof(ushort);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), 0);
        offset += sizeof(uint);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), CurrentVersion);
        offset += sizeof(uint);
        Magic.CopyTo(result, offset);

        return result;
    }
}
