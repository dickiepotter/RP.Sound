namespace RP.Sound;

/// <summary>
/// Thrown by the strict <c>Normalized</c> methods when asked to normalize silence — there is no
/// gain that turns a peak of zero into a peak of one. The <c>NormalizedOrDefault</c> variants
/// return the buffer unchanged instead, following the library-wide strict/OrDefault convention.
/// </summary>
public sealed class NormalizeSilentBufferException : InvalidOperationException
{
    public NormalizeSilentBufferException()
        : base("Cannot normalize a silent buffer: it has no peak to scale to full level. Use NormalizedOrDefault to receive the buffer unchanged instead.")
    {
    }
}
