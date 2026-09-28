namespace Proscenium.CodeQuality.Deslop;

internal static class DeslopCSharpSyntax
{
    internal static bool ValidRange(DeslopFile file, DeslopFingerprint member)
        => !file.Skipped && member.Start >= 0 && member.End > member.Start && member.End <= file.Source.Length
            && Boundary(file.Source, member.Start) && Boundary(file.Source, member.End);

    private static bool Boundary(byte[] source, int offset)
        => offset == source.Length || (source[offset] & 0xc0) != 0x80;
}
