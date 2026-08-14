using RP.Sound.IO;

namespace RP.Sound.Music;

/// <summary>
/// A ProTracker module made audible: a deterministic replayer for <see cref="ModModule"/>,
/// reproducing the Amiga playback model the format was designed around.
/// <list type="bullet">
/// <item><b>Pitch is a period, not a frequency.</b> The Amiga's Paula chip replayed one sample
/// byte every <c>period</c> ticks of its 3.546895 MHz PAL clock (two clocks per tick), so a
/// note's playback rate is clock ÷ (2 × period) — period 428 (middle C) ≈ 8287 Hz. Halving the
/// period doubles the pitch (Amiga Hardware Reference Manual, Commodore 1989).</item>
/// <item><b>Time is ticks, not seconds.</b> A row lasts <c>speed</c> ticks (default 6); a tick
/// lasts 2.5 ÷ tempo seconds (default tempo 125 makes a tick exactly one PAL video frame, 0.02 s
/// — the tracker's heartbeat was the vertical blank). Effects re-fire every tick, which is why
/// tracker music shimmers at speeds a note grid cannot express.</item>
/// <item><b>Effects</b>: arpeggio (0), portamento up/down (1/2), tone portamento (3), vibrato
/// (4), the volume-slide pairings (5/6), tremolo (7), sample offset (9), volume slide (A),
/// position jump (B), set volume (C), pattern break (D), set speed/tempo (F), and the E-commands
/// for fine slides (E1/E2/EA/EB), pattern loop (E6), retrigger (E9), note cut (EC) and note
/// delay (ED). Panning (8/E8) is meaningless in a mono render, and the Amiga hardware filter
/// (E0), vibrato/tremolo waveform selects (E4/E7), set finetune (E5) and invert loop (EF) are
/// deliberately ignored — see "Future considerations" in the README.</item>
/// </list>
/// A module that runs off the end of its order list has a natural end; one that jumps backwards
/// loops forever and reports an infinite <see cref="Duration"/>, exactly like the ambience
/// generators — ask it for the length you want and it keeps playing round the loop.
/// </summary>
public sealed class ModSong : ISound
{
    /// <summary>Paula's PAL master clock in Hz; a note's sample rate is this ÷ (2 × period).</summary>
    public const double PaulaClockPal = 7_093_789.2;

    // ProTracker clamps slid periods to its three-octave period table's ends (B-3 to C-1).
    private const int MinPeriod = 113;
    private const int MaxPeriod = 856;

    /// <summary>ProTracker's 32-entry vibrato sine table (quarter wave stored as a half wave, amplitude 0–255).</summary>
    private static readonly int[] SineTable =
    {
        0, 24, 49, 74, 97, 120, 141, 161, 180, 197, 212, 224, 235, 244, 250, 253,
        255, 253, 250, 244, 235, 224, 212, 197, 180, 161, 141, 120, 97, 74, 49, 24,
    };

    public ModModule Module { get; }
    public Level Level { get; }

    /// <summary>Whether the song jumps back on itself (and so never ends) rather than running off the order list.</summary>
    public bool Loops { get; }

    /// <summary>One full pass of the song: to its end, or to the moment it first revisits a row.</summary>
    public double LoopDuration { get; }

    /// <summary>The natural length — infinite for a looping song, one pass otherwise.</summary>
    public double Duration => Loops ? double.PositiveInfinity : LoopDuration;

    public ModSong(ModModule module, Level? level = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        Module = module;
        Level = level ?? Level.Unity;

        (List<RowStep> steps, bool loops) = BuildSchedule(maxSeconds: null);
        Loops = loops;
        double end = 0;
        foreach (RowStep step in steps) end += step.Ticks * step.TickSeconds;
        LoopDuration = end;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        (List<RowStep> steps, _) = BuildSchedule(maxSeconds: duration);

        var channels = new Channel[Module.ChannelCount];
        for (int c = 0; c < channels.Length; c++) channels[c] = new Channel();

        // Each Amiga channel spans ±1 at full volume; dividing by the channel count keeps the sum
        // inside ±1 without normalisation, preserving the module's own dynamics.
        double gain = Level.Linear / Module.ChannelCount;
        double time = 0;
        int cursor = 0;

        foreach (RowStep step in steps)
        {
            ModPattern pattern = Module.Patterns[Module.Order[step.Position]];
            for (int c = 0; c < channels.Length; c++)
                StartRow(channels[c], pattern[step.Row, c]);

            for (int tick = 0; tick < step.Ticks && cursor < samples.Length; tick++)
            {
                foreach (Channel channel in channels) OnTick(channel, tick);

                time += step.TickSeconds;
                int tickEnd = System.Math.Min((int)System.Math.Round(time * context.SampleRate), samples.Length);
                foreach (Channel channel in channels) MixTick(channel, tick, samples, cursor, tickEnd, context.SampleRate, gain);
                cursor = tickEnd;
            }

            if (cursor >= samples.Length) break;
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }

    public override string ToString() =>
        $"ModSong(\"{Module.Title}\", {(Loops ? $"loops every {LoopDuration:0.###} s" : $"{LoopDuration:0.###} s")})";

    // ---- The sequencer: which row plays when. Only tick-0 effects can bend time (speed, tempo,
    // jumps, breaks, pattern loop, pattern delay), so row order and length are decided here and
    // the per-tick engine never changes course mid-row. ----

    private readonly record struct RowStep(int Position, int Row, int Ticks, double TickSeconds);

    private (List<RowStep> Steps, bool Loops) BuildSchedule(double? maxSeconds)
    {
        var steps = new List<RowStep>();
        var entered = new HashSet<(int Position, int Row)> { (0, 0) };
        bool loops = false;
        int position = 0, row = 0;
        int speed = 6;
        double tempo = 125;
        int loopRow = 0, loopCount = 0;
        double time = 0;

        while (position < Module.Order.Count)
        {
            if (maxSeconds is double max && time >= max) break;

            ModPattern pattern = Module.Patterns[Module.Order[position]];
            int? jumpPosition = null, breakRow = null;
            int patternDelay = 0;
            bool loopJump = false, stop = false;

            for (int channel = 0; channel < pattern.ChannelCount; channel++)
            {
                ModCell cell = pattern[row, channel];
                int x = cell.Argument >> 4, y = cell.Argument & 0xF;
                switch (cell.Effect)
                {
                    case 0xB:
                        jumpPosition = cell.Argument;
                        break;
                    case 0xD:
                        // The break row is written in decimal on the tracker screen, so it is
                        // stored as two decimal digits packed in nibbles.
                        breakRow = System.Math.Min(x * 10 + y, ModPattern.Rows - 1);
                        break;
                    case 0xF when cell.Argument == 0:
                        stop = true; // F00 halts the song in ProTracker.
                        break;
                    case 0xF when cell.Argument < 0x20:
                        speed = cell.Argument;
                        break;
                    case 0xF:
                        tempo = cell.Argument;
                        break;
                    case 0xE when x == 0x6 && y == 0:
                        loopRow = row;
                        break;
                    case 0xE when x == 0x6:
                        if (loopCount == 0) { loopCount = y; loopJump = true; }
                        else if (--loopCount > 0) loopJump = true;
                        break;
                    case 0xE when x == 0xE:
                        patternDelay = y;
                        break;
                }
            }

            double tickSeconds = 2.5 / tempo;
            steps.Add(new RowStep(position, row, speed * (1 + patternDelay), tickSeconds));
            time += speed * (1 + patternDelay) * tickSeconds;

            if (stop) break;
            if (loopJump) { row = loopRow; continue; } // E6 replays within the pattern; no entry bookkeeping.

            if (jumpPosition is not null || breakRow is not null)
            {
                position = jumpPosition ?? position + 1;
                row = breakRow ?? 0;
                if (position >= Module.Order.Count) break;
            }
            else if (++row >= ModPattern.Rows)
            {
                position++;
                row = 0;
                if (position >= Module.Order.Count) break;
            }
            else
            {
                continue;
            }

            // A new pattern was entered (by jump, break or falling off the end): revisiting a
            // (position, row) already entered means the song has looped.
            loopRow = 0;
            loopCount = 0;
            if (!entered.Add((position, row)))
            {
                loops = true;
                if (maxSeconds is null) break;
                entered.Clear();
                entered.Add((position, row));
            }
        }

        return (steps, loops);
    }

    // ---- The channel engine: what one voice does inside a row. ----

    private sealed class Channel
    {
        public ModCell Cell;
        public ModSample? Sample;
        public bool Active;
        public double SamplePosition;
        public int Period;
        public int TargetPeriod;
        public int Finetune;
        public int Volume;
        public int DelayedPeriod;      // A note held back by EDx until its tick comes.
        public int TonePortaSpeed;     // 3xx remembers its speed.
        public int VibratoSpeed, VibratoDepth, VibratoPosition;
        public int TremoloSpeed, TremoloDepth, TremoloPosition;
        public int SampleOffset;       // 9xx remembers its offset.
    }

    private void StartRow(Channel ch, ModCell cell)
    {
        ch.Cell = cell;
        ch.DelayedPeriod = 0;
        int x = cell.Argument >> 4, y = cell.Argument & 0xF;

        // A sample number resets the instrument's volume and tuning even without a note.
        if (cell.SampleNumber > 0)
        {
            ch.Sample = Module.Samples[cell.SampleNumber - 1];
            ch.Volume = ch.Sample.Volume;
            ch.Finetune = ch.Sample.Finetune;
        }

        if (cell.Period > 0)
        {
            if (cell.Effect is 0x3 or 0x5)
                ch.TargetPeriod = cell.Period; // Tone portamento glides to the note instead of striking it.
            else if (cell.Effect == 0xE && x == 0xD && y > 0)
                ch.DelayedPeriod = cell.Period; // Note delay holds the strike for y ticks.
            else
                Trigger(ch, cell.Period);
        }

        // Tick-0 effects: the ones that act once, on the row's downbeat.
        switch (cell.Effect)
        {
            case 0x3 when cell.Argument > 0: ch.TonePortaSpeed = cell.Argument; break;
            case 0x4: if (x > 0) ch.VibratoSpeed = x; if (y > 0) ch.VibratoDepth = y; break;
            case 0x7: if (x > 0) ch.TremoloSpeed = x; if (y > 0) ch.TremoloDepth = y; break;
            case 0xC: ch.Volume = System.Math.Min(cell.Argument, 64); break;
            case 0xE:
                switch (x)
                {
                    case 0x1: ch.Period = System.Math.Max(MinPeriod, ch.Period - y); break; // Fine slides move once per row,
                    case 0x2: ch.Period = System.Math.Min(MaxPeriod, ch.Period + y); break; // not once per tick.
                    case 0xA: ch.Volume = System.Math.Min(64, ch.Volume + y); break;
                    case 0xB: ch.Volume = System.Math.Max(0, ch.Volume - y); break;
                    case 0xC when y == 0: ch.Volume = 0; break; // EC0 cuts on the downbeat itself.
                }

                break;
        }
    }

    private void Trigger(Channel ch, int period)
    {
        ch.Period = period;
        ch.VibratoPosition = 0;
        ch.TremoloPosition = 0;

        ModCell cell = ch.Cell;
        double offset = 0;
        if (cell.Effect == 0x9)
        {
            if (cell.Argument > 0) ch.SampleOffset = cell.Argument * 256;
            offset = ch.SampleOffset;
        }

        ch.Active = ch.Sample is { Length: > 0 };
        ch.SamplePosition = ch.Sample is null ? 0 : System.Math.Min(offset, ch.Sample.Length);
    }

    private void OnTick(Channel ch, int tick)
    {
        if (tick == 0) return; // Tick 0 was handled by StartRow.
        ModCell cell = ch.Cell;
        int x = cell.Argument >> 4, y = cell.Argument & 0xF;

        switch (cell.Effect)
        {
            case 0x1: ch.Period = System.Math.Max(MinPeriod, ch.Period - cell.Argument); break;
            case 0x2: ch.Period = System.Math.Min(MaxPeriod, ch.Period + cell.Argument); break;
            case 0x3: SlideTowardTarget(ch); break;
            case 0x4: ch.VibratoPosition += ch.VibratoSpeed; break;
            case 0x5: SlideTowardTarget(ch); VolumeSlide(ch, x, y); break;
            case 0x6: ch.VibratoPosition += ch.VibratoSpeed; VolumeSlide(ch, x, y); break;
            case 0x7: ch.TremoloPosition += ch.TremoloSpeed; break;
            case 0xA: VolumeSlide(ch, x, y); break;
            case 0xE:
                switch (x)
                {
                    case 0x9 when y > 0 && tick % y == 0: ch.SamplePosition = 0; ch.Active = ch.Sample is { Length: > 0 }; break;
                    case 0xC when tick == y: ch.Volume = 0; break;
                    case 0xD when tick == y && ch.DelayedPeriod > 0: Trigger(ch, ch.DelayedPeriod); break;
                }

                break;
        }
    }

    private static void SlideTowardTarget(Channel ch)
    {
        if (ch.TargetPeriod <= 0 || ch.Period == 0) return;
        ch.Period = ch.Period < ch.TargetPeriod
            ? System.Math.Min(ch.TargetPeriod, ch.Period + ch.TonePortaSpeed)
            : System.Math.Max(ch.TargetPeriod, ch.Period - ch.TonePortaSpeed);
    }

    private static void VolumeSlide(Channel ch, int up, int down) =>
        ch.Volume = up > 0 ? System.Math.Min(64, ch.Volume + up) : System.Math.Max(0, ch.Volume - down);

    /// <summary>The waveform value at a table position: the stored half wave, negated for the second half of the cycle.</summary>
    private static int Wave(int position) => (position & 0x20) == 0 ? SineTable[position & 0x1F] : -SineTable[position & 0x1F];

    private void MixTick(Channel ch, int tick, float[] samples, int from, int to, int sampleRate, double gain)
    {
        if (!ch.Active || ch.Sample is null || ch.Period <= 0) return;
        ModSample sample = ch.Sample;
        ModCell cell = ch.Cell;

        // The period this tick actually plays at: arpeggio substitutes a chord tone (root, +x,
        // +y semitones cycling every three ticks), vibrato adds the table wobble. Both are
        // per-tick decorations — the channel's stored period is untouched.
        double period = ch.Period;
        if (cell.Effect == 0x0 && cell.Argument > 0)
        {
            int semitones = (tick % 3) switch { 1 => cell.Argument >> 4, 2 => cell.Argument & 0xF, _ => 0 };
            if (semitones > 0) period /= System.Math.Pow(2, semitones / 12.0);
        }
        else if (cell.Effect is 0x4 or 0x6)
        {
            period += Wave(ch.VibratoPosition) * ch.VibratoDepth / 128.0;
        }

        if (period < 1) return;
        double frequency = PaulaClockPal / (2.0 * period) * System.Math.Pow(2, ch.Finetune / 96.0);
        double step = frequency / sampleRate;

        double volume = System.Math.Clamp(ch.Volume + TremoloOffset(ch), 0, 64) / 64.0;
        double amplitude = volume * gain;

        double loopEnd = sample.IsLooped ? sample.LoopStart + sample.LoopLength : sample.Length;
        double position = ch.SamplePosition;
        ReadOnlySpan<sbyte> data = sample.Data;

        for (int i = from; i < to; i++)
        {
            if (position >= loopEnd)
            {
                if (!sample.IsLooped) { ch.Active = false; break; }
                position = sample.LoopStart + (position - loopEnd) % sample.LoopLength;
            }

            int index = (int)position;
            int next = index + 1;
            if (next >= loopEnd) next = sample.IsLooped ? sample.LoopStart : index;
            double fraction = position - index;
            double value = (data[index] + (data[next] - data[index]) * fraction) / 128.0;
            samples[i] += (float)(value * amplitude);
            position += step;
        }

        ch.SamplePosition = position;
    }

    private static int TremoloOffset(Channel ch) =>
        ch.Cell.Effect == 0x7 ? Wave(ch.TremoloPosition) * ch.TremoloDepth / 64 : 0;
}
