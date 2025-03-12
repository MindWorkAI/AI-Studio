namespace AIStudio.Tools;

/// <inheritdoc />
public sealed class ThreadSafeRandom : Random
{
    private static readonly Lock LOCK = new();

    #region Overrides of Random

    /// <inheritdoc />
    public override int Next()
    {
        lock (LOCK)
            return base.Next();
    }

    /// <inheritdoc />
    public override int Next(int maxValue)
    {
        lock (LOCK)
            return base.Next(maxValue);
    }

    /// <inheritdoc />
    public override int Next(int minValue, int maxValue)
    {
        lock (LOCK)
            return base.Next(minValue, maxValue);
    }

    /// <inheritdoc />
    public override void NextBytes(byte[] buffer)
    {
        lock (LOCK)
            base.NextBytes(buffer);
    }

    /// <inheritdoc />
    public override void NextBytes(Span<byte> buffer)
    {
        lock (LOCK)
            base.NextBytes(buffer);
    }

    /// <inheritdoc />
    public override double NextDouble()
    {
        lock (LOCK)
            return base.NextDouble();
    }

    /// <inheritdoc />
    public override long NextInt64()
    {
        lock (LOCK)
            return base.NextInt64();
    }

    /// <inheritdoc />
    public override long NextInt64(long maxValue)
    {
        lock (LOCK)
            return base.NextInt64(maxValue);
    }

    /// <inheritdoc />
    public override long NextInt64(long minValue, long maxValue)
    {
        lock (LOCK)
            return base.NextInt64(minValue, maxValue);
    }

    /// <inheritdoc />
    public override float NextSingle()
    {
        lock (LOCK)
            return base.NextSingle();
    }

    /// <inheritdoc />
    protected override double Sample()
    {
        lock (LOCK)
            return base.Sample();
    }

    #endregion
}