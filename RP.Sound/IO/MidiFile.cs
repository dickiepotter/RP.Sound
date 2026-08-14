namespace RP.Sound.IO;

/// <summary>
/// Reads and writes Standard MIDI Files (SMF, the .mid format defined by the MIDI Manufacturers
/// Association, 1988). A MIDI file is not audio — it is a list of <em>instructions</em> (press
/// this key now, release it later, change tempo here) with times counted in ticks of a musical
/// beat. Reading resolves all of that — variable-length delta times, running status, the tempo
/// map, note-on/note-off pairing — into a <see cref="MidiSequence"/> of notes in plain seconds.
/// Writing does the reverse, producing a format-0 file any sequencer or player accepts.
/// <para>
/// Supported on read: formats 0 and 1 (format 1's parallel tracks are merged, which is exactly
/// what format 1 means), tempo changes anywhere, running status, note-on with velocity 0 as
/// note-off. Deliberately rejected with a clear error: format 2 (independent songs in one file)
/// and SMPTE time division (frames, not beats — film sync, not music). See "Future
/// considerations" in the README.
/// </para>
/// </summary>
public static class MidiFile
{
    /// <summary>Ticks per quarter note used when writing. 480 is the common sequencer default; at 120 BPM one tick is ~1 ms.</summary>
    private const int WriteDivision = 480;

    /// <summary>The SMF default tempo, microseconds per quarter note (500 000 µs = 120 BPM), in force until a Set Tempo event says otherwise.</summary>
    private const double DefaultMicrosecondsPerBeat = 500_000;

    public static MidiSequence Load(string path) => Read(File.ReadAllBytes(path));

    public static void Save(MidiSequence sequence, string path) => File.WriteAllBytes(path, ToBytes(sequence));

    /// <summary>Parses a Standard MIDI File into a sequence of notes with absolute times in seconds.</summary>
    public static MidiSequence Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var reader = new BigEndianReader(bytes);

        if (!reader.ReadTag("MThd")) throw new FormatException("Not a Standard MIDI File: the MThd header is missing.");
        int headerLength = reader.ReadInt32();
        int format = reader.ReadInt16();
        int trackCount = reader.ReadInt16();
        int division = reader.ReadInt16();
        reader.Skip(headerLength - 6); // A longer header is legal; the spec says skip what we don't know.

        if (format == 2) throw new FormatException("SMF format 2 holds independent songs, not one performance; split the file instead.");
        if (format is not (0 or 1)) throw new FormatException($"Unknown SMF format {format} (expected 0 or 1).");
        if ((division & 0x8000) != 0) throw new FormatException("SMPTE time division (frames per second) is film sync, not beats; only musical division is supported.");
        if (division <= 0) throw new FormatException("The file declares zero ticks per quarter note, so no event has a time.");

        // Pass 1: parse every track into raw channel events and collect the tempo map. Tempo
        // events govern the whole file no matter which track holds them (in format 1 they
        // conventionally live in track 0, but the spec does not require it).
        var tracks = new List<(List<RawEvent> Events, long EndTick)>();
        var tempoChanges = new List<(long Tick, double MicrosecondsPerBeat)>();
        for (int t = 0; t < trackCount; t++)
        {
            if (!reader.ReadTag("MTrk")) throw new FormatException($"Track {t} is missing its MTrk header.");
            int length = reader.ReadInt32();
            tracks.Add(ParseTrack(reader.Slice(length), tempoChanges));
        }

        tempoChanges.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        var tempoMap = new TempoMap(tempoChanges, division);

        // Pass 2: pair note-ons with note-offs, per channel and key, first-on-first-off. A
        // note-on with velocity 0 is a note-off — the spec allows it so running status can keep
        // one status byte across a whole stream of ons and offs.
        var notes = new List<MidiNote>();
        foreach ((List<RawEvent> events, long endTick) in tracks)
        {
            var programs = new int[16];
            var open = new Dictionary<(int Channel, int Key), Queue<(long Tick, int Velocity, int Program)>>();

            foreach (RawEvent e in events)
            {
                int kind = e.Status & 0xF0;
                int channel = e.Status & 0x0F;
                if (kind == 0xC0)
                {
                    programs[channel] = e.Data1;
                }
                else if (kind == 0x90 && e.Data2 > 0)
                {
                    if (!open.TryGetValue((channel, e.Data1), out var queue)) open[(channel, e.Data1)] = queue = new Queue<(long, int, int)>();
                    queue.Enqueue((e.Tick, e.Data2, programs[channel]));
                }
                else if (kind == 0x80 || (kind == 0x90 && e.Data2 == 0))
                {
                    if (open.TryGetValue((channel, e.Data1), out var queue) && queue.Count > 0)
                    {
                        (long onTick, int velocity, int program) = queue.Dequeue();
                        AddNote(notes, tempoMap, onTick, e.Tick, e.Data1, velocity, channel, program);
                    }
                    // An off with no matching on is legal noise (a file sliced mid-note); ignore it.
                }
            }

            // Notes still held at end of track get released there — the honest reading of a
            // truncated file, and better than dropping them silently.
            foreach (((int channel, int key), var queue) in open)
                foreach ((long onTick, int velocity, int program) in queue)
                    AddNote(notes, tempoMap, onTick, endTick, key, velocity, channel, program);
        }

        return new MidiSequence(notes, tempoMap.InitialBpm);
    }

    private static void AddNote(List<MidiNote> notes, TempoMap map, long onTick, long offTick, int key, int velocity, int channel, int program)
    {
        double start = map.Seconds(onTick);
        double duration = map.Seconds(offTick) - start;
        // A zero-length note (on and off in the same tick) carries no sound; skip it rather than
        // manufacture an arbitrary length.
        if (duration <= 0) return;
        notes.Add(new MidiNote(start, duration, key, velocity, channel, program));
    }

    /// <summary>
    /// Encodes the sequence as a format-0 Standard MIDI File. Times are spelled as ticks at
    /// <see cref="MidiSequence.TempoBpm"/> and 480 ticks per quarter note, so the worst
    /// quantisation error is half a tick — about half a millisecond at 120 BPM, far below the
    /// ~10 ms threshold at which listeners notice timing shifts.
    /// </summary>
    public static byte[] ToBytes(MidiSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        double ticksPerSecond = WriteDivision * sequence.TempoBpm / 60.0;

        // Build the event list: tempo first, then for each note a program change when its
        // channel's program differs from what is already in force, the note-on, and the note-off.
        // Offs sort before ons at the same tick so back-to-back repeats of a key release before
        // they re-strike.
        var events = new List<(long Tick, int Order, byte[] Bytes)>
        {
            (0, 0, TempoMeta(sequence.TempoBpm)),
        };

        var programs = new int[16];
        Array.Fill(programs, -1);
        foreach (MidiNote note in sequence.Notes)
        {
            long onTick = (long)System.Math.Round(note.Start * ticksPerSecond);
            long offTick = System.Math.Max(onTick + 1, (long)System.Math.Round(note.End * ticksPerSecond));
            if (programs[note.Channel] != note.Program)
            {
                programs[note.Channel] = note.Program;
                events.Add((onTick, 1, new[] { (byte)(0xC0 | note.Channel), (byte)note.Program }));
            }

            events.Add((onTick, 2, new[] { (byte)(0x90 | note.Channel), (byte)note.NoteNumber, (byte)note.Velocity }));
            events.Add((offTick, 1, new[] { (byte)(0x80 | note.Channel), (byte)note.NoteNumber, (byte)64 }));
        }

        events.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));

        using var track = new MemoryStream();
        long lastTick = 0;
        foreach ((long tick, _, byte[] payload) in events)
        {
            WriteVariableLength(track, tick - lastTick);
            track.Write(payload);
            lastTick = tick;
        }

        WriteVariableLength(track, 0);
        track.Write(new byte[] { 0xFF, 0x2F, 0x00 }); // End of track.

        using var file = new MemoryStream();
        using var writer = new BinaryWriter(file);
        writer.Write("MThd"u8);
        WriteInt32(file, 6);
        WriteInt16(file, 0);             // Format 0: everything in one track.
        WriteInt16(file, 1);
        WriteInt16(file, WriteDivision);
        writer.Write("MTrk"u8);
        WriteInt32(file, (int)track.Length);
        track.WriteTo(file);
        return file.ToArray();
    }

    // ---- Track parsing ----

    private readonly record struct RawEvent(long Tick, byte Status, byte Data1, byte Data2);

    private static (List<RawEvent> Events, long EndTick) ParseTrack(BigEndianReader reader, List<(long Tick, double MicrosecondsPerBeat)> tempoChanges)
    {
        var events = new List<RawEvent>();
        long tick = 0;
        byte runningStatus = 0;

        while (!reader.AtEnd)
        {
            tick += reader.ReadVariableLength();
            byte first = reader.PeekByte();

            if (first == 0xFF)
            {
                reader.Skip(1);
                byte type = reader.ReadByte();
                int length = (int)reader.ReadVariableLength();
                if (type == 0x51 && length == 3)
                {
                    int microseconds = (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte();
                    tempoChanges.Add((tick, microseconds));
                }
                else
                {
                    reader.Skip(length);
                    if (type == 0x2F) break; // End of track.
                }
            }
            else if (first is 0xF0 or 0xF7)
            {
                // SysEx: a length-prefixed blob of manufacturer data. Nothing in it is a note.
                reader.Skip(1);
                reader.Skip((int)reader.ReadVariableLength());
            }
            else
            {
                // A channel event. A leading data byte (< 0x80) means running status: reuse the
                // previous status byte — the format's one compression trick.
                byte status;
                if (first >= 0x80) { status = reader.ReadByte(); runningStatus = status; }
                else if (runningStatus >= 0x80) status = runningStatus;
                else throw new FormatException("A data byte appeared before any status byte — the track is corrupt.");

                int kind = status & 0xF0;
                byte data1 = reader.ReadByte();
                byte data2 = kind is 0xC0 or 0xD0 ? (byte)0 : reader.ReadByte(); // Program change and channel pressure carry one data byte, the rest two.
                events.Add(new RawEvent(tick, status, data1, data2));
            }
        }

        // The tick where parsing stopped (normally the End of Track meta) is where held notes get
        // released.
        return (events, tick);
    }

    /// <summary>
    /// The tempo map: piecewise-constant microseconds-per-beat over ticks. Seconds of any tick =
    /// sum of complete segments before it plus its own partial segment — precomputed cumulatively
    /// so lookup is a binary search.
    /// </summary>
    private sealed class TempoMap
    {
        private readonly long[] ticks;
        private readonly double[] seconds;
        private readonly double[] secondsPerTick;

        public TempoMap(List<(long Tick, double MicrosecondsPerBeat)> changes, int division)
        {
            var points = new List<(long Tick, double MicrosecondsPerBeat)> { (0, DefaultMicrosecondsPerBeat) };
            foreach ((long tick, double us) in changes)
            {
                if (tick == points[^1].Tick) points[^1] = (tick, us);
                else points.Add((tick, us));
            }

            this.ticks = new long[points.Count];
            this.seconds = new double[points.Count];
            this.secondsPerTick = new double[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                this.ticks[i] = points[i].Tick;
                this.secondsPerTick[i] = points[i].MicrosecondsPerBeat / 1_000_000.0 / division;
                if (i > 0) this.seconds[i] = this.seconds[i - 1] + (this.ticks[i] - this.ticks[i - 1]) * this.secondsPerTick[i - 1];
            }

            InitialBpm = 60_000_000.0 / points[0].MicrosecondsPerBeat;
        }

        public double InitialBpm { get; }

        public double Seconds(long tick)
        {
            int i = Array.BinarySearch(this.ticks, tick);
            if (i < 0) i = ~i - 1;
            return this.seconds[i] + (tick - this.ticks[i]) * this.secondsPerTick[i];
        }
    }

    // ---- Big-endian primitives (MIDI predates little-endian's victory) ----

    private sealed class BigEndianReader(byte[] bytes, int start = 0, int end = -1)
    {
        private readonly byte[] bytes = bytes;
        private readonly int end = end < 0 ? bytes.Length : end;
        private int position = start;

        public bool AtEnd => this.position >= this.end;

        public byte PeekByte() => this.position < this.end ? this.bytes[this.position] : throw Truncated();

        public byte ReadByte() => this.position < this.end ? this.bytes[this.position++] : throw Truncated();

        public int ReadInt16() => (ReadByte() << 8) | ReadByte();

        public int ReadInt32() => (ReadByte() << 24) | (ReadByte() << 16) | (ReadByte() << 8) | ReadByte();

        public void Skip(int count)
        {
            if (count < 0 || this.position + count > this.end) throw Truncated();
            this.position += count;
        }

        public bool ReadTag(string tag)
        {
            if (this.position + tag.Length > this.end) return false;
            for (int i = 0; i < tag.Length; i++)
                if (this.bytes[this.position + i] != (byte)tag[i]) return false;
            this.position += tag.Length;
            return true;
        }

        public BigEndianReader Slice(int length)
        {
            if (length < 0 || this.position + length > this.end) throw Truncated();
            var slice = new BigEndianReader(this.bytes, this.position, this.position + length);
            this.position += length;
            return slice;
        }

        /// <summary>
        /// SMF's variable-length quantity: seven data bits per byte, high bit set on every byte
        /// but the last — small numbers take one byte, and delta times are usually small.
        /// </summary>
        public long ReadVariableLength()
        {
            long value = 0;
            for (int i = 0; i < 5; i++)
            {
                byte b = ReadByte();
                value = (value << 7) | (uint)(b & 0x7F);
                if ((b & 0x80) == 0) return value;
            }

            throw new FormatException("A variable-length quantity ran past 5 bytes — the file is corrupt.");
        }

        private static FormatException Truncated() => new("The MIDI file ends mid-structure — it is truncated or corrupt.");
    }

    private static void WriteVariableLength(Stream stream, long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Delta times cannot be negative.");
        Span<byte> buffer = stackalloc byte[5];
        int count = 0;
        do
        {
            buffer[count++] = (byte)(value & 0x7F);
            value >>= 7;
        }
        while (value > 0);
        for (int i = count - 1; i >= 0; i--)
            stream.WriteByte((byte)(buffer[i] | (i > 0 ? 0x80 : 0)));
    }

    private static byte[] TempoMeta(double bpm)
    {
        int microseconds = (int)System.Math.Round(60_000_000.0 / bpm);
        return new byte[] { 0xFF, 0x51, 0x03, (byte)(microseconds >> 16), (byte)(microseconds >> 8), (byte)microseconds };
    }

    private static void WriteInt16(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}
