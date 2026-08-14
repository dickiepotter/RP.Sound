using RP.Sound.Effects;
using RP.Sound.Physics;

namespace RP.Sound.Tests;

public class PhysicsTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 7);

    [Fact]
    public void ModalBody_SmallerObjectsRingHigher()
    {
        var large = new ModalBody(Material.Steel, 0.8);
        var small = new ModalBody(Material.Steel, 0.4);
        Assert.True(small.Modes[0].Frequency > large.Modes[0].Frequency);
    }

    [Fact]
    public void ModalBody_StifferMaterialRingsHigher_AtTheSameSize()
    {
        var steel = new ModalBody(Material.Steel, 0.5);
        var rubber = new ModalBody(Material.Rubber, 0.5);
        Assert.True(steel.Modes[0].Frequency > rubber.Modes[0].Frequency);
    }

    [Fact]
    public void ModalBody_HigherModesDieFaster()
    {
        var body = new ModalBody(Material.Wood, 0.5);
        for (int i = 1; i < body.Modes.Count; i++)
            Assert.True(body.Modes[i].DecaySeconds <= body.Modes[i - 1].DecaySeconds);
    }

    [Fact]
    public void ModalBody_LossierMaterialDiesFaster()
    {
        var steel = new ModalBody(Material.Steel, 0.5);
        var wood = new ModalBody(Material.Wood, 0.5);
        Assert.True(wood.Modes[0].DecaySeconds < steel.Modes[0].DecaySeconds);
    }

    [Fact]
    public void Impact_FasterIsLouder()
    {
        var body = new ModalBody(Material.Wood, 0.4);
        Level slow = new Impact(body, 0.5).Render(Context, 0.5).PeakLevel;
        Level fast = new Impact(body, 5).Render(Context, 0.5).PeakLevel;
        Assert.True(fast > slow);
    }

    [Fact]
    public void Impact_HarderStrikerIsBrighter()
    {
        // Brightness measured as the share of energy surviving a high-pass filter.
        var body = new ModalBody(Material.Steel, 0.5);
        static double HighShare(AudioBuffer b) =>
            b.HighPassed(2000).RmsLevel.Linear / System.Math.Max(1e-12, b.RmsLevel.Linear);

        AudioBuffer soft = new Impact(body, 2, strikerHardness: 0.05).Render(Context, 0.5);
        AudioBuffer hard = new Impact(body, 2, strikerHardness: 1.0).Render(Context, 0.5);
        Assert.True(HighShare(hard) > HighShare(soft));
    }

    [Fact]
    public void Impact_FromDrop_UsesEnergyConservation()
    {
        var body = new ModalBody(Material.Stone, 0.4);
        Impact dropped = Impact.FromDrop(body, height: 2, gravity: 10);
        Assert.Equal(System.Math.Sqrt(2 * 10 * 2), dropped.Velocity, 9);
    }

    [Fact]
    public void BounceSequence_IntervalsShrinkByTheRestitution()
    {
        var bounce = new BounceSequence(new ModalBody(Material.Wood, 0.3), initialVelocity: 4, restitution: 0.5, gravity: 10);
        IReadOnlyList<(double Time, double Velocity)> bounces = bounce.Bounces;
        Assert.True(bounces.Count >= 3);

        double firstInterval = bounces[1].Time - bounces[0].Time;
        double secondInterval = bounces[2].Time - bounces[1].Time;
        Assert.Equal(0.5, secondInterval / firstInterval, 6);   // intervals scale by e
        Assert.Equal(0.5, bounces[1].Velocity / bounces[0].Velocity, 6); // and so do speeds
    }

    [Fact]
    public void BounceSequence_ComesToRest()
    {
        var bounce = new BounceSequence(new ModalBody(Material.Rubber, 0.2), initialVelocity: 5, restitution: 0.8);
        Assert.True(double.IsFinite(bounce.Duration));
        Assert.True(bounce.Bounces[^1].Velocity <= 5 * System.Math.Pow(0.8, bounce.Bounces.Count - 1) + 1e-9);
    }

    [Fact]
    public void PerfectRestitution_CannotBeConstructed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BounceSequence(new ModalBody(Material.Rubber, 0.2), 1, restitution: 1));
    }

    [Fact]
    public void Scrape_FasterScrapeHasEnergyHigherUp()
    {
        var body = new ModalBody(Material.Stone, 0.5);
        static double Centroidish(AudioBuffer b) =>
            b.HighPassed(1200).RmsLevel.Linear / System.Math.Max(1e-12, b.RmsLevel.Linear);

        AudioBuffer slow = new Scrape(body, speed: 0.2, duration: 0.5, roughness: 0.5).Render(Context, 0.5);
        AudioBuffer fast = new Scrape(body, speed: 3, duration: 0.5, roughness: 0.5).Render(Context, 0.5);
        Assert.True(Centroidish(fast) > Centroidish(slow));
    }

    [Fact]
    public void Whoosh_CentreFollowsTheStrouhalLaw()
    {
        var whoosh = new Whoosh(speed: 10, size: 0.01, duration: 1);
        Assert.Equal(0.2 * 10 / 0.01, whoosh.SheddingFrequency.Hertz, 6);

        // Below the audible band the frequency clamps rather than disappearing.
        Assert.Equal(30, new Whoosh(speed: 10, size: 0.1, duration: 1).SheddingFrequency.Hertz, 6);
    }

    [Fact]
    public void ParticleSurface_ProducesSoundThatDiesAway()
    {
        AudioBuffer crunch = ParticleSurface.Gravel().Render(Context, 0.6);
        double early = crunch.FittedToDuration(0.1).RmsLevel.Linear;
        AudioBuffer lateHalf = AudioBuffer.FromSamples(crunch.Samples[(crunch.Length / 2)..], crunch.SampleRate);
        Assert.True(early > lateHalf.RmsLevel.Linear * 2);
        Assert.True(crunch.PeakLevel.Linear > 0);
    }

    [Fact]
    public void Footsteps_RunHasMoreStepsThanWalk()
    {
        var walk = new Footsteps(Material.Wood, speed: 1.0, duration: 3);
        var run = new Footsteps(Material.Wood, speed: 3.5, duration: 3);
        Assert.True(run.Cadence > walk.Cadence);
        Assert.True(walk.Render(Context, 3).PeakLevel.Linear > 0);
    }

    [Fact]
    public void MaterialPresets_AreAllValidAndFindable()
    {
        foreach (Material material in Material.Presets)
        {
            Assert.True(material.SoundSpeed > 0);
            Assert.Same(material, Material.FromName(material.Name.ToUpperInvariant()));
        }

        Assert.Throws<ArgumentException>(() => Material.FromName("unobtainium"));
    }
}
