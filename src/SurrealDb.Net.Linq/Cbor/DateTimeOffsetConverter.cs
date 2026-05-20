using Dahomey.Cbor;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;

namespace SurrealDb.Net.Linq.Cbor;

/// <summary>
/// Reads / writes <see cref="DateTimeOffset"/> against SurrealDB's CBOR
/// datetime shape: <c>tag(12) [seconds, nanos]</c>.
///
/// <para>This converter exists because <c>SurrealDb.Net 0.10.x</c> only
/// registers a converter for <see cref="DateTime"/> — <see cref="DateTimeOffset"/>
/// falls back to Dahomey.Cbor's generic <c>ObjectConverter</c>, which expects
/// a CBOR Map and throws <c>"[XX] Expected major type Map (5)"</c> the
/// instant any row with a non-null <see cref="DateTimeOffset"/> field
/// (e.g. <c>created_at</c>, <c>updated_at</c>) is round-tripped through
/// <c>Select&lt;T&gt;(table)</c> or <c>Select&lt;T&gt;(RecordId)</c>.</para>
///
/// <para>Encoding mirrors SurrealDB's <c>datetime</c> wire format — the
/// same two-element array of <c>[seconds, nanos]</c> the built-in
/// <see cref="DateTime"/> converter uses — so values round-trip identically
/// between <see cref="DateTime"/> and <see cref="DateTimeOffset"/> typed
/// rows.</para>
/// </summary>
public sealed class DateTimeOffsetConverter : CborConverterBase<DateTimeOffset>
{
    private const ulong TagCustomDatetime = 12;
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
    private const double TicksPerNanosecond = TicksPerMicrosecond / 1000d;
    private const long NanosecondsPerTick = (long)(1 / TicksPerNanosecond);
    private const int NanosPerSecond = 1_000_000_000;

    /// <inheritdoc />
    public override DateTimeOffset Read(ref CborReader reader)
    {
        reader.ReadBeginArray();
        var size = reader.ReadSize();
        if (size > 2)
        {
            throw new CborException("Expected a CBOR array with at most 2 elements for a SurrealDB datetime");
        }
        var seconds = size >= 1 ? reader.ReadInt64() : 0;
        var nanos = size >= 2 ? reader.ReadInt32() : 0;

        // (seconds, nanos) since Unix epoch → DateTimeOffset (UTC). We add
        // the fractional component via Ticks so we don't lose precision below
        // the millisecond boundary the way `.AddSeconds(double)` would.
        var ticks = (long)Math.Round((double)nanos / NanosecondsPerTick);
        return DateTimeOffset.UnixEpoch.AddSeconds(seconds).AddTicks(ticks);
    }

    /// <inheritdoc />
    public override void Write(ref CborWriter writer, DateTimeOffset value)
    {
        writer.WriteSemanticTag(TagCustomDatetime);
        writer.WriteBeginArray(2);

        var utc = value.ToUniversalTime();
        var diff = utc - DateTimeOffset.UnixEpoch;
        var seconds = diff.Ticks / TimeSpan.TicksPerSecond;

        // Nanos = sub-second part expressed as nanoseconds.
        var subSecondTicks = utc.UtcDateTime.Ticks % TimeSpan.TicksPerSecond;
        var nanos = (int)(subSecondTicks * NanosecondsPerTick);

        // Sign-correction mirrors DateTimeFormatter from the vendored fork:
        // for instants before the Unix epoch, the truncated seconds count is
        // one too few when there's a positive nanos remainder.
        if (nanos > 0 && seconds < 0)
        {
            seconds--;
        }

        // Clamp nanos to the valid range just in case the multiplication
        // above produces NanosPerSecond exactly (rounding behaviour at the
        // boundary of `Ticks`).
        if (nanos >= NanosPerSecond)
        {
            seconds++;
            nanos -= NanosPerSecond;
        }
        if (nanos < 0)
        {
            seconds--;
            nanos += NanosPerSecond;
        }

        writer.WriteInt64(seconds);
        writer.WriteInt32(nanos);
        writer.WriteEndArray(2);
    }
}
