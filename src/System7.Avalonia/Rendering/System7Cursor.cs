namespace System7.Avalonia.Rendering;

/// <summary>A System 7 cursor: the ROM arrow, the System's I-beam, crosshair and plus, and the Finder's animated watch.</summary>
public sealed class System7Cursor
{
    private static readonly Dictionary<string, System7Cursor> All = Load();

    private System7Cursor(string name, IReadOnlyList<System7CursorFrame> frames, TimeSpan interval)
    {
        Name = name;
        Frames = frames;
        FrameInterval = interval;
    }

    public string Name { get; }

    /// <summary>The images, in the order an animated cursor shows them. A still cursor has one.</summary>
    public IReadOnlyList<System7CursorFrame> Frames { get; }

    /// <summary>How long each frame of an animated cursor shows, or zero for a still cursor.</summary>
    public TimeSpan FrameInterval { get; }

    public static System7Cursor Arrow => All["arrow"];

    public static System7Cursor IBeam => All["ibeam"];

    public static System7Cursor Crosshair => All["crosshair"];

    public static System7Cursor Plus => All["plus"];

    public static System7Cursor Watch => All["watch"];

    private static Dictionary<string, System7Cursor> Load()
    {
        using var stream = typeof(System7Cursor).Assembly.GetManifestResourceStream("System7.Avalonia.Assets.Cursors.bin")!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray().AsSpan();
        var result = new Dictionary<string, System7Cursor>(StringComparer.Ordinal);
        var count = (data[0] << 8) | data[1];
        var offset = 2;
        for (var i = 0; i < count; i++)
        {
            var name = System.Text.Encoding.ASCII.GetString(data.Slice(offset + 1, data[offset]));
            offset += 1 + data[offset];
            var frames = (data[offset] << 8) | data[offset + 1];
            var interval = (data[offset + 2] << 8) | data[offset + 3];
            offset += 4;
            var list = new System7CursorFrame[frames];
            for (var f = 0; f < frames; f++, offset += 68) list[f] = new System7CursorFrame(data.Slice(offset, 68));
            result[name] = new System7Cursor(name, list, TimeSpan.FromMilliseconds(interval));
        }
        return result;
    }
}
