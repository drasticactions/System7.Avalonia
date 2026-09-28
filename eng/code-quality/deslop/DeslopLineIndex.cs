namespace Proscenium.CodeQuality.Deslop;

// report_render.rs::LineIndex and report_metrics.rs::count_analysed_lines.
internal sealed class DeslopLineIndex
{
    private readonly int[] _newlines;

    public DeslopLineIndex(byte[] source)
    {
        SourceLength = source.Length;
        _newlines = source.Select((value, offset) => (value, offset))
            .Where(item => item.value == (byte)'\n').Select(item => item.offset).ToArray();
        PhysicalLines = (ulong)_newlines.Length + (source.Length != 0 && source[^1] != (byte)'\n' ? 1UL : 0UL);
    }

    public int SourceLength { get; }
    public ulong PhysicalLines { get; }

    public int LineForOffset(int offset)
    {
        var position = Array.BinarySearch(_newlines, Math.Min(offset, SourceLength));
        return (position >= 0 ? position : ~position) + 1;
    }
}
