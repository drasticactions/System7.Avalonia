using System.Buffers.Binary;

namespace System7.Avalonia.Rendering;

/// <summary>
/// A System 7 alert sound: the named 'snd ' resources of the System 7.0.1 System file, as 8-bit offset-binary mono samples.
/// Simple Beep, which the System plays on the square-wave synthesizer, is rendered from its commands.
/// </summary>
public sealed class System7AlertSound
{
    private static readonly IReadOnlyList<System7AlertSound> Loaded = Load();
    private readonly byte[] samples;

    private System7AlertSound(string name, double sampleRate, byte[] samples)
    {
        Name = name;
        SampleRate = sampleRate;
        this.samples = samples;
    }

    /// <summary>Every alert sound, with Simple Beep first.</summary>
    public static IReadOnlyList<System7AlertSound> All => Loaded;

    /// <summary>Simple Beep, the sound a new System 7 installation alerts with.</summary>
    public static System7AlertSound SimpleBeep => Loaded[0];

    public string Name { get; }

    /// <summary>The sample rate in hertz, 22254.54 or 11127.27 as the resource gives it.</summary>
    public double SampleRate { get; }

    /// <summary>The samples, 8-bit offset binary, one channel.</summary>
    public ReadOnlySpan<byte> Samples => samples;

    public static System7AlertSound? Find(string name)
    {
        foreach (var sound in Loaded)
            if (string.Equals(sound.Name, name, StringComparison.OrdinalIgnoreCase)) return sound;
        return null;
    }

    /// <summary>The sound as a RIFF WAVE file: 8-bit unsigned PCM, one channel, at the sample rate rounded to whole hertz.</summary>
    public byte[] ToWav()
    {
        var rate = (int)Math.Round(SampleRate);
        var wav = new byte[44 + samples.Length];
        var span = wav.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + samples.Length);
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], rate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], rate);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 8);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], samples.Length);
        samples.CopyTo(span[44..]);
        return wav;
    }

    private static IReadOnlyList<System7AlertSound> Load()
    {
        using var stream = typeof(System7AlertSound).Assembly.GetManifestResourceStream("System7.Avalonia.Assets.AlertSounds.bin")!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray().AsSpan();
        var count = BinaryPrimitives.ReadUInt16BigEndian(data);
        var offset = 2;
        var result = new System7AlertSound[count];
        for (var i = 0; i < count; i++)
        {
            var name = System.Text.Encoding.ASCII.GetString(data.Slice(offset + 1, data[offset]));
            offset += 1 + data[offset];
            var rate = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) / 65536.0;
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 4)..]);
            offset += 8;
            result[i] = new System7AlertSound(name, rate, data.Slice(offset, length).ToArray());
            offset += length;
        }
        return result;
    }
}
