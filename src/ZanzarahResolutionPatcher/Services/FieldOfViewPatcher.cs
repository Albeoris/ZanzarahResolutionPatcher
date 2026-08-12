using System.Buffers.Binary;
using AsmResolver;
using AsmResolver.PE.File;

namespace ZanzarahResolutionPatcher.Services;

public enum FieldOfViewPatchStatus
{
    NotAvailable,
    Available,
    AlreadyApplied,
}

public sealed class FieldOfViewPatcher
{
    public const int OriginalFunctionOffset = 0x0755F;

    // PE image section names are limited to eight bytes.
    public const string SectionName = ".fovfix";

    private const int PeHeaderPointerOffset = 0x3C;
    private const int PeSignatureSize = sizeof(uint);
    private const int MinimumVirtualSectionSize = 0x100;
    private const byte BreakpointInstruction = 0xCC;

    private static readonly byte[] OriginalFunction =
    [
        0x55, 0x8B, 0xEC, 0x51, 0x51, 0x8B, 0x45, 0x08,
        0x89, 0x45, 0xF8, 0x8B, 0x45, 0x0C, 0x56, 0x89,
        0x45, 0xFC, 0x8D, 0x45, 0xF8, 0x8B, 0xF1, 0x50,
        0xFF, 0x76, 0x18, 0xE8, 0x51, 0x2A, 0x0E, 0x00,
        0x59, 0x59, 0x89, 0x46, 0x18, 0x5E, 0xC9, 0xC2,
        0x08, 0x00,
    ];

    private static readonly byte[] PatchedFunction =
    [
        0x55, 0x89, 0xE5, 0x83, 0xEC, 0x08, 0x56, 0x89,
        0xCE, 0x8B, 0x45, 0x08, 0x8B, 0x55, 0x0C, 0x3D,
        0x00, 0x00, 0x80, 0x3F, 0x75, 0x27, 0x81, 0xFA,
        0x00, 0x00, 0x40, 0x3F, 0x75, 0x1F, 0x83, 0x3D,
        0x68, 0xAC, 0x61, 0x00, 0x00, 0x7E, 0x16, 0xDB,
        0x05, 0x6C, 0xAC, 0x61, 0x00, 0xDB, 0x05, 0x68,
        0xAC, 0x61, 0x00, 0xDE, 0xF9, 0xD8, 0x4D, 0x0C,
        0xD9, 0x5D, 0xF8, 0xEB, 0x03, 0x89, 0x45, 0xF8,
        0x89, 0x55, 0xFC, 0x8D, 0x45, 0xF8, 0x50, 0xFF,
        0x76, 0x18, 0xB8, 0xD0, 0x9F, 0x4E, 0x00, 0xFF,
        0xD0, 0x83, 0xC4, 0x08, 0x89, 0x46, 0x18, 0x5E,
        0xC9, 0xC2, 0x08, 0x00,
    ];

    public FieldOfViewPatchStatus Analyze(ReadOnlySpan<byte> executableBytes)
    {
        if (!LooksLikePortableExecutable(executableBytes))
        {
            return FieldOfViewPatchStatus.NotAvailable;
        }

        var file = ReadPeFile(executableBytes);
        if (HasFixSection(file))
        {
            return FieldOfViewPatchStatus.AlreadyApplied;
        }

        if (!ContainsOriginalFunction(executableBytes))
        {
            return FieldOfViewPatchStatus.NotAvailable;
        }

        EnsurePatchable(file);
        return FieldOfViewPatchStatus.Available;
    }

    public byte[] Apply(ReadOnlySpan<byte> executableBytes)
    {
        if (!LooksLikePortableExecutable(executableBytes))
        {
            throw new InvalidDataException("The FOV fix requires a valid PE executable.");
        }

        var file = ReadPeFile(executableBytes);
        EnsurePatchable(file);

        if (!ContainsOriginalFunction(executableBytes))
        {
            throw new InvalidDataException(
                $"The expected SetFOV function was not found at physical file offset 0x{OriginalFunctionOffset:X5}.");
        }

        ConsumeSectionHeaderPadding(file);

        var section = new PESection(
            SectionName,
            SectionFlags.ContentCode | SectionFlags.MemoryExecute | SectionFlags.MemoryRead,
            new VirtualSegment(new DataSegment(PatchedFunction), MinimumVirtualSectionSize));
        file.Sections.Add(section);
        file.AlignSections();
        file.UpdateHeaders();

        if (!file.TryGetSectionContainingOffset(OriginalFunctionOffset, out var originalSection))
        {
            throw new InvalidDataException(
                $"Physical file offset 0x{OriginalFunctionOffset:X5} is not mapped by a PE section.");
        }

        var originalFunctionRva = originalSection.FileOffsetToRva(OriginalFunctionOffset);
        var relativeTarget = CalculateRelativeJump(originalFunctionRva, section.Rva);

        using var output = new MemoryStream();
        file.Write(output);
        var result = output.ToArray();

        if (!ContainsOriginalFunction(result))
        {
            throw new InvalidDataException(
                "Adding the FOV section unexpectedly moved or changed the original SetFOV function. " +
                $"Original section offset: 0x{originalSection.Offset:X}; " +
                $"bytes at expected offset: {Convert.ToHexString(result.AsSpan(OriginalFunctionOffset, OriginalFunction.Length))}.");
        }

        WriteJump(result.AsSpan(OriginalFunctionOffset, OriginalFunction.Length), relativeTarget);
        return result;
    }

    private static PEFile ReadPeFile(ReadOnlySpan<byte> executableBytes)
    {
        try
        {
            return PEFile.FromBytes(executableBytes.ToArray());
        }
        catch (Exception exception) when (exception is BadImageFormatException or EndOfStreamException)
        {
            throw new InvalidDataException("The executable contains an invalid PE image.", exception);
        }
    }

    private static bool LooksLikePortableExecutable(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < PeHeaderPointerOffset + sizeof(int) ||
            bytes[0] != (byte)'M' ||
            bytes[1] != (byte)'Z')
        {
            return false;
        }

        var peHeaderOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes[PeHeaderPointerOffset..]);
        return peHeaderOffset >= 0 &&
               peHeaderOffset <= bytes.Length - PeSignatureSize &&
               BinaryPrimitives.ReadUInt32LittleEndian(bytes[peHeaderOffset..]) == PEFile.ValidPESignature;
    }

    private static bool ContainsOriginalFunction(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= OriginalFunctionOffset + OriginalFunction.Length &&
        bytes.Slice(OriginalFunctionOffset, OriginalFunction.Length).SequenceEqual(OriginalFunction);

    private static bool HasFixSection(PEFile file) =>
        file.Sections.Any(section =>
            string.Equals(section.Name?.ToString(), SectionName, StringComparison.Ordinal));

    private static void EnsurePatchable(PEFile file)
    {
        if (HasFixSection(file))
        {
            throw new InvalidOperationException("The automatic FOV fix is already applied.");
        }

        if (file.FileHeader.Machine != MachineType.I386 ||
            file.OptionalHeader.Magic != OptionalHeaderMagic.PE32)
        {
            throw new InvalidDataException("The automatic FOV fix supports only x86 PE32 executables.");
        }

        EnsureSectionHeaderSpace(file);
    }

    private static void EnsureSectionHeaderSpace(PEFile file)
    {
        var firstRawDataOffset = file.Sections
            .Where(static section => section.Offset > 0 && section.GetPhysicalSize() > 0)
            .Select(static section => section.Offset)
            .DefaultIfEmpty(0UL)
            .Min();
        if (firstRawDataOffset == 0)
        {
            throw new InvalidDataException("The PE image does not contain a section with raw data.");
        }

        var newSectionHeaderOffset = checked(
            (ulong)file.DosHeader.NextHeaderOffset +
            PeSignatureSize +
            file.FileHeader.GetPhysicalSize() +
            file.OptionalHeader.GetPhysicalSize() +
            ((ulong)file.Sections.Count * SectionHeader.SectionHeaderSize));
        var newSectionHeaderEnd = checked(newSectionHeaderOffset + SectionHeader.SectionHeaderSize);

        if (newSectionHeaderEnd > firstRawDataOffset)
        {
            throw new InvalidDataException(
                $"The PE headers do not have the required {SectionHeader.SectionHeaderSize} free bytes " +
                "for the automatic FOV fix section.");
        }
    }

    private static void ConsumeSectionHeaderPadding(PEFile file)
    {
        if (file.ExtraSectionData is not IReadableSegment readablePadding)
        {
            throw new InvalidDataException(
                "The PE header padding cannot be read safely.");
        }

        var padding = readablePadding.ToArray();
        var sectionHeaderSize = checked((int)SectionHeader.SectionHeaderSize);
        if (padding.Length < sectionHeaderSize)
        {
            throw new InvalidDataException(
                "The PE header padding is smaller than the validated section-header space.");
        }

        if (padding.AsSpan(0, sectionHeaderSize).ContainsAnyExcept((byte)0))
        {
            throw new InvalidDataException(
                "The PE section-header space contains non-zero data and cannot be overwritten safely.");
        }

        file.ExtraSectionData = new DataSegment(padding[sectionHeaderSize..]);
    }

    private static int CalculateRelativeJump(uint sourceRva, uint targetRva)
    {
        var displacement = (long)targetRva - (sourceRva + 5L);
        if (displacement is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidDataException("The FOV fix section is outside the range of an x86 near jump.");
        }

        return (int)displacement;
    }

    private static void WriteJump(Span<byte> originalFunction, int relativeTarget)
    {
        if (originalFunction.Length != OriginalFunction.Length)
        {
            throw new ArgumentException(
                $"The original function must be exactly {OriginalFunction.Length} bytes long.",
                nameof(originalFunction));
        }

        originalFunction[0] = 0xE9;
        BinaryPrimitives.WriteInt32LittleEndian(originalFunction[1..], relativeTarget);
        originalFunction[5..].Fill(BreakpointInstruction);
    }
}
