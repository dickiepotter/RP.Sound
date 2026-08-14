namespace RP.Sound.IO;

/// <summary>
/// One of a module's 31 instrument slots: a raw signed 8-bit PCM recording plus the header fields
/// ProTracker keeps beside it. Lengths and loop points are in bytes here (the file stores them in
/// 16-bit words, so they are always even). A loop length of 2 bytes or less means "no loop" —
/// ProTracker's convention, because the hardware needed a minimum repeat and trackers wrote 1 word
/// to mean off. Immutable: the sample data is copied in and exposed read-only.
/// </summary>
public sealed class ModSample
{
    /// <summary>An unused instrument slot: no name, no data.</summary>
    public static readonly ModSample Empty = new(string.Empty, ReadOnlySpan<sbyte>.Empty);

    private readonly sbyte[] data;

    /// <summary>The sample's name — 22 bytes in the file, historically used as scrolling graffiti as much as labelling.</summary>
    public string Name { get; }

    /// <summary>Tuning correction in eighths of a semitone, −8…+7, applied whenever a note plays this sample.</summary>
    public int Finetune { get; }

    /// <summary>Default volume 0–64, set each time a note triggers the sample.</summary>
    public int Volume { get; }

    /// <summary>Where the loop begins, in bytes into the sample.</summary>
    public int LoopStart { get; }

    /// <summary>The loop's length in bytes; 2 or less means the sample plays once and stops.</summary>
    public int LoopLength { get; }

    public ModSample(string name, ReadOnlySpan<sbyte> data, int finetune = 0, int volume = 64, int loopStart = 0, int loopLength = 0)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > 22) throw new ArgumentOutOfRangeException(nameof(name), name, "A sample name is at most 22 characters in the file format.");
        if (data.Length % 2 != 0 || data.Length > 0xFFFF * 2)
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, "Sample data must be an even number of bytes (the file stores lengths in words) and at most 131 070 bytes.");
        if (finetune is < -8 or > 7) throw new ArgumentOutOfRangeException(nameof(finetune), finetune, "Finetune is a signed nibble: −8…+7 eighths of a semitone.");
        if (volume is < 0 or > 64) throw new ArgumentOutOfRangeException(nameof(volume), volume, "A sample volume is 0–64 (the Amiga hardware range).");
        if (loopStart < 0 || loopStart % 2 != 0 || loopStart > data.Length)
            throw new ArgumentOutOfRangeException(nameof(loopStart), loopStart, "A loop start must be an even byte offset inside the sample.");
        if (loopLength < 0 || loopLength % 2 != 0 || (loopLength > 2 && loopStart + loopLength > data.Length))
            throw new ArgumentOutOfRangeException(nameof(loopLength), loopLength, "A loop must be an even number of bytes and fit inside the sample.");

        Name = name;
        this.data = data.ToArray();
        Finetune = finetune;
        Volume = volume;
        LoopStart = loopStart;
        LoopLength = loopLength;
    }

    /// <summary>The raw signed 8-bit samples, recorded at whatever rate the note's period will play them back at.</summary>
    public ReadOnlySpan<sbyte> Data => this.data;

    /// <summary>The sample's length in bytes.</summary>
    public int Length => this.data.Length;

    /// <summary>Whether a note keeps sounding until told to stop (loop length above ProTracker's 2-byte "off" sentinel).</summary>
    public bool IsLooped => LoopLength > 2;

    public override string ToString() => $"ModSample(\"{Name}\", {Length} bytes{(IsLooped ? $", loop {LoopStart}+{LoopLength}" : string.Empty)}, vol {Volume})";
}

/// <summary>
/// One cell of a pattern: what a channel is told at one row. Everything is optional — a cell can
/// name a sample, a period (the note, as the Amiga counted it: clock ticks between output samples,
/// so <em>lower</em> period = <em>higher</em> pitch), an effect, or any mix of them. Period 0
/// means "no new note", sample 0 means "keep the current sample".
/// </summary>
public readonly record struct ModCell(int SampleNumber = 0, int Period = 0, int Effect = 0, int Argument = 0)
{
    /// <summary>Builds a cell, validating each field's file-format range.</summary>
    public static ModCell Create(int sampleNumber = 0, int period = 0, int effect = 0, int argument = 0)
    {
        if (sampleNumber is < 0 or > 31) throw new ArgumentOutOfRangeException(nameof(sampleNumber), sampleNumber, "A sample number is 0 (none) or 1–31.");
        if (period is < 0 or > 0xFFF) throw new ArgumentOutOfRangeException(nameof(period), period, "A period is 0 (no note) or up to 12 bits.");
        if (effect is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(effect), effect, "An effect command is one nibble, 0–15.");
        if (argument is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(argument), argument, "An effect argument is one byte, 0–255.");
        return new ModCell(sampleNumber, period, effect, argument);
    }

    /// <summary>Whether the cell does anything at all.</summary>
    public bool IsEmpty => SampleNumber == 0 && Period == 0 && Effect == 0 && Argument == 0;

    public override string ToString() => IsEmpty ? "···" : $"s{SampleNumber:00} p{Period:000} {Effect:X}{Argument:X2}";
}

/// <summary>
/// A pattern: 64 rows of cells, one cell per channel per row — the tracker's unit of musical
/// repetition, played top to bottom. Immutable; the cells are copied in.
/// </summary>
public sealed class ModPattern
{
    /// <summary>Every pattern is exactly 64 rows — a format constant, 4 bars of 16 rows at the usual scan rate.</summary>
    public const int Rows = 64;

    private readonly ModCell[,] cells;

    public int ChannelCount { get; }

    /// <summary>Builds a pattern from cells indexed [row, channel]; the array must be 64 rows tall.</summary>
    public ModPattern(ModCell[,] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.GetLength(0) != Rows)
            throw new ArgumentOutOfRangeException(nameof(cells), cells.GetLength(0), "A ProTracker pattern is exactly 64 rows.");
        ChannelCount = cells.GetLength(1);
        if (ChannelCount is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(cells), ChannelCount, "A pattern has 1–8 channels.");
        this.cells = (ModCell[,])cells.Clone();
    }

    /// <summary>An empty pattern: 64 rows of silence.</summary>
    public static ModPattern Silent(int channelCount = 4) => new(new ModCell[Rows, channelCount]);

    public ModCell this[int row, int channel] => this.cells[row, channel];

    public override string ToString() => $"ModPattern({ChannelCount} channels)";
}

/// <summary>
/// A complete ProTracker module — the .mod format born on the Amiga (Ultimate Soundtracker 1987,
/// standardised by ProTracker 1990): up to 31 recorded samples, up to 64 patterns of note/effect
/// cells, and an order list saying which pattern plays when. Unlike MIDI, a module carries its own
/// sound — the samples ARE the instruments — which is why a .mod plays identically everywhere,
/// and why game music used it for a decade. Immutable like every description in the library.
/// </summary>
public sealed class ModModule
{
    /// <summary>The format's instrument slot count. Modules with fewer simply leave slots empty.</summary>
    public const int SampleSlots = 31;

    private readonly ModSample[] samples;
    private readonly ModPattern[] patterns;
    private readonly int[] order;

    /// <summary>The module's title, at most 20 characters in the file.</summary>
    public string Title { get; }

    /// <summary>Channels played simultaneously: 4 (the classic Amiga count, tag "M.K."), 6 or 8.</summary>
    public int ChannelCount { get; }

    public ModModule(string title, IEnumerable<ModSample> samples, IEnumerable<ModPattern> patterns, IEnumerable<int> order, int channelCount = 4)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (title.Length > 20) throw new ArgumentOutOfRangeException(nameof(title), title, "A module title is at most 20 characters in the file format.");
        if (channelCount is not (4 or 6 or 8))
            throw new ArgumentOutOfRangeException(nameof(channelCount), channelCount, "The tagged .mod variants carry 4, 6 or 8 channels.");

        this.samples = samples.ToArray();
        if (this.samples.Length > SampleSlots) throw new ArgumentOutOfRangeException(nameof(samples), this.samples.Length, "A module has at most 31 samples.");
        if (this.samples.Any(s => s is null)) throw new ArgumentNullException(nameof(samples), "A sample slot cannot be null; use ModSample.Empty.");
        if (this.samples.Length < SampleSlots)
            this.samples = this.samples.Concat(Enumerable.Repeat(ModSample.Empty, SampleSlots - this.samples.Length)).ToArray();

        this.patterns = patterns.ToArray();
        if (this.patterns.Length is 0 or > 128) throw new ArgumentOutOfRangeException(nameof(patterns), this.patterns.Length, "A module holds 1–128 patterns.");
        if (this.patterns.Any(p => p is null)) throw new ArgumentNullException(nameof(patterns), "A pattern cannot be null.");
        if (this.patterns.Any(p => p.ChannelCount != channelCount))
            throw new ArgumentException($"Every pattern must have the module's channel count ({channelCount}).", nameof(patterns));

        this.order = order.ToArray();
        if (this.order.Length is 0 or > 128) throw new ArgumentOutOfRangeException(nameof(order), this.order.Length, "The order list holds 1–128 positions.");
        foreach (int position in this.order)
            if (position < 0 || position >= this.patterns.Length)
                throw new ArgumentOutOfRangeException(nameof(order), position, $"An order entry must name an existing pattern (0–{this.patterns.Length - 1}).");

        Title = title;
        ChannelCount = channelCount;
    }

    /// <summary>The 31 instrument slots (1-based sample numbers in cells index this list at number − 1).</summary>
    public IReadOnlyList<ModSample> Samples => this.samples;

    /// <summary>The patterns, indexed by the order list.</summary>
    public IReadOnlyList<ModPattern> Patterns => this.patterns;

    /// <summary>The song: which pattern plays at each position.</summary>
    public IReadOnlyList<int> Order => this.order;

    public override string ToString() =>
        $"ModModule(\"{Title}\", {ChannelCount} channels, {this.order.Length} positions, {this.patterns.Length} patterns, {this.samples.Count(s => s.Length > 0)} samples)";
}
