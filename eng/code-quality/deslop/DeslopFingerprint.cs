namespace Proscenium.CodeQuality.Deslop;

internal sealed record DeslopFingerprint(byte[] Hash, int FileId, int Start, int End, int NodeCount)
{
    public string HashKey => Convert.ToHexStringLower(Hash);
}
