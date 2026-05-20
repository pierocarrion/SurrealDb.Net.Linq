using System.Runtime.CompilerServices;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;

namespace SurrealDb.Net.Linq.Cbor;

/// <summary>
/// Reads an arbitrary CBOR map into <see cref="Dictionary{TKey, TValue}"/>
/// (<c>string</c> → <c>object?</c>). Nested maps become Dictionary, arrays
/// become <see cref="List{T}"/>, primitives stay as-is.
///
/// <para><b>Read-only.</b> Intended for fields the consumer explicitly opts in
/// to (via <c>[CborConverter(typeof(CborMapToDictionaryConverter))]</c>) when
/// the wire shape is a free-form <c>object FLEXIBLE</c> sub-object. The
/// global default <see cref="Dictionary{TKey, TValue}"/> converter shipped by
/// Dahomey.Cbor handles writes correctly and is left in place — overriding
/// it would break <c>RawQuery</c> parameter serialization (whose dictionary
/// holds arbitrary runtime types we cannot enumerate here).</para>
/// </summary>
public sealed class CborMapToDictionaryConverter : CborConverterBase<Dictionary<string, object?>?>
{
    /// <inheritdoc />
    public override Dictionary<string, object?>? Read(ref CborReader reader) =>
        ReadNullableMap(ref reader);

    /// <inheritdoc />
    /// <remarks>
    /// Not supported on purpose — see class remarks. If you need a custom
    /// write path, register a different converter against the host
    /// <see cref="Dahomey.Cbor.CborOptions"/>.
    /// </remarks>
    public override void Write(ref CborWriter writer, Dictionary<string, object?>? value) =>
        throw new NotSupportedException(
            "CborMapToDictionaryConverter is read-only. Do not register it as the global converter " +
            "for Dictionary<string, object?> — Dahomey.Cbor's default Write path handles outbound " +
            "RawQuery parameter maps correctly. Apply this converter selectively via " +
            "`[CborConverter(typeof(CborMapToDictionaryConverter))]` on the specific row fields that " +
            "carry free-form `object FLEXIBLE` sub-objects.");

    internal static Dictionary<string, object?>? ReadNullableMap(ref CborReader reader)
    {
        if (reader.GetCurrentDataItemType() == CborDataItemType.Null)
        {
            reader.ReadNull();
            return null;
        }

        if (reader.GetCurrentDataItemType() != CborDataItemType.Map)
        {
            reader.SkipDataItem();
            return null;
        }

        reader.ReadBeginMap();

        int remainingItemCount = reader.ReadSize();
        var dict = new Dictionary<string, object?>(remainingItemCount);

        while (reader.MoveNextMapItem(ref remainingItemCount))
        {
            string? key = reader.ReadString();
            if (key is not null)
            {
                object? value = ReadCborValueIntoObject(ref reader);
                dict[key] = value;
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        return dict;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object? ReadCborValueIntoObject(ref CborReader reader)
    {
        var itemType = reader.GetCurrentDataItemType();

        return itemType switch
        {
            CborDataItemType.Null => reader.ReadNull(),
            CborDataItemType.Boolean => reader.ReadBoolean(),
            CborDataItemType.String => reader.ReadString(),
            CborDataItemType.Signed => reader.ReadInt64(),
            CborDataItemType.Unsigned => reader.ReadUInt64(),
            CborDataItemType.Single => reader.ReadSingle(),
            CborDataItemType.Double => reader.ReadDouble(),
            CborDataItemType.Map => ReadMapIntoDictionary(ref reader),
            CborDataItemType.Array => ReadArrayIntoList(ref reader),
            _ => SkipAndReturnNull(ref reader),
        };
    }

    private static Dictionary<string, object?> ReadMapIntoDictionary(ref CborReader reader)
    {
        reader.ReadBeginMap();

        int remainingItemCount = reader.ReadSize();
        var dict = new Dictionary<string, object?>(remainingItemCount);

        while (reader.MoveNextMapItem(ref remainingItemCount))
        {
            string? key = reader.ReadString();
            if (key is not null)
            {
                object? value = ReadCborValueIntoObject(ref reader);
                dict[key] = value;
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        return dict;
    }

    private static List<object?> ReadArrayIntoList(ref CborReader reader)
    {
        reader.ReadBeginArray();

        int size = reader.ReadSize();
        var list = new List<object?>(size);

        for (int i = 0; i < size; i++)
        {
            list.Add(ReadCborValueIntoObject(ref reader));
        }

        return list;
    }

    private static object? SkipAndReturnNull(ref CborReader reader)
    {
        reader.SkipDataItem();
        return null;
    }
}
