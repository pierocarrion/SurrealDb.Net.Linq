using System.Runtime.CompilerServices;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;

namespace SurrealDb.Net.Linq.Cbor;

/// <summary>
/// Reads an arbitrary CBOR map into <see cref="Dictionary{TKey, TValue}"/>
/// (<c>string</c> → <c>object?</c>), or <c>null</c> if the wire value is null.
/// Nested maps become <see cref="Dictionary{TKey, TValue}"/>, arrays become
/// <see cref="List{T}"/>, primitives stay as-is.
///
/// <para>Required because Dahomey.Cbor's default object converter trips on
/// SurrealDB row responses that mix typed fields with free-form sub-objects
/// (e.g. <c>country_catalog.working_hours</c> as <c>object FLEXIBLE</c>): the
/// default path expects a CLR <see cref="Type"/> hint per field, can't pick
/// one for a dynamic map, and throws <c>CborException: Expected major type
/// Map (5)</c>. This converter binds the type directly so any incoming map
/// shape is accepted.</para>
///
/// <para>Write is intentionally not supported — callers that need to send a
/// dynamic dictionary should pass it through a typed parameter instead.</para>
/// </summary>
public sealed class CborMapToDictionaryConverter : CborConverterBase<Dictionary<string, object?>?>
{
    /// <inheritdoc />
    public override Dictionary<string, object?>? Read(ref CborReader reader) =>
        ReadNullableMap(ref reader);

    /// <inheritdoc />
    public override void Write(ref CborWriter writer, Dictionary<string, object?>? value) =>
        throw new NotSupportedException("Cannot write Dictionary<string, object?> back to CBOR.");

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
