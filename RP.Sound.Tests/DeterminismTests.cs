using RP.Sound.Ambience;
using RP.Sound.Music;
using RP.Sound.Physics;

namespace RP.Sound.Tests;

/// <summary>
/// Determinism is a library-wide convention: (description, context) ⇒ identical samples, always.
/// These tests hold every stochastic generator to it.
/// </summary>
public class DeterminismTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 42);

    public static IEnumerable<object[]> StochasticSounds()
    {
        yield return new object[] { new Impact(new ModalBody(Material.Glass, 0.3), 3) };
        yield return new object[] { BounceSequence.FromDrop(new ModalBody(Material.Wood, 0.3), 1.5) };
        yield return new object[] { new Scrape(new ModalBody(Material.Steel, 0.5), 1.5, 0.5) };
        yield return new object[] { ParticleSurface.Gravel() };
        yield return new object[] { new Whoosh(15, 0.1, 0.5) };
        yield return new object[] { new Wind(0.6, 0.7) };
        yield return new object[] { new Rain(0.5) };
        yield return new object[] { new Fire(0.5) };
        yield return new object[] { new Thunder(1500) };
        yield return new object[] { new Riser(1, 0.8) };
        yield return new object[] { new Stinger(Mood.Horror, duration: 1) };
    }

    [Theory]
    [MemberData(nameof(StochasticSounds))]
    public void SameSeed_RendersIdenticalSamples(ISound sound)
    {
        AudioBuffer first = sound.Render(Context, 0.5);
        AudioBuffer second = sound.Render(Context, 0.5);
        Assert.Equal(first.Samples.ToArray(), second.Samples.ToArray());
    }

    [Theory]
    [MemberData(nameof(StochasticSounds))]
    public void DifferentSeed_RendersDifferentSamples(ISound sound)
    {
        AudioBuffer first = sound.Render(Context, 0.5);
        AudioBuffer second = sound.Render(Context with { Seed = 43 }, 0.5);
        Assert.NotEqual(first.Samples.ToArray(), second.Samples.ToArray());
    }

    [Fact]
    public void GenerativeScene_IsDeterministicToo()
    {
        var scene = new GenerativeScene(Mood.Horror);
        var left1 = scene.Render(Context, 1).Left.Samples.ToArray();
        var left2 = scene.Render(Context, 1).Left.Samples.ToArray();
        Assert.Equal(left1, left2);
    }

    [Fact]
    public void RandomStreams_AreIndependentPerName()
    {
        var a = Context.CreateRandom("stream-a");
        var b = Context.CreateRandom("stream-b");
        Assert.NotEqual(a.NextDouble(), b.NextDouble());

        // And re-creating the same stream restarts it identically.
        Assert.Equal(Context.CreateRandom("stream-a").NextDouble(), Context.CreateRandom("stream-a").NextDouble());
    }
}
