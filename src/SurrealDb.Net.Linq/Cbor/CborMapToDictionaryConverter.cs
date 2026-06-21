using System.Runtime.CompilerServices;
using Dahomey.Cbor;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;

namespace SurrealDb.Net.Linq.Cbor;

/// <summary>
/// Bridges CBOR ↔ <see cref="Dictionary{TKey, TValue}"/>
/// (<c>string</c> → <c>object?</c>) end-to-end.
///
/// <para><b>Read:</b> any CBOR map is materialised to a Dictionary; nested
/// maps become Dictionary, arrays become <see cref="List{T}"/>, primitives
/// stay as-is. Required because Dahomey.Cbor 1.26.1's stock
/// <c>DictionaryConverter&lt;string, object&gt;</c> resolves the value
/// converter to <c>ObjectConverter&lt;object&gt;</c> at compile-time, which
/// expects a CBOR Map for every value — so SurrealDB rows that carry mixed
/// primitives inside an <c>object</c> column (e.g. <c>country_catalog.working_hours</c>,
/// <c>customer.document</c>) throw <c>CborException: Expected major type Map (5)</c>
/// the moment any value is not a map.</para>
///
/// <para><b>Write:</b> dispatches each value by runtime type. Primitives are
/// emitted directly; nested dictionaries and lists recurse; complex types
/// (<see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="Guid"/>,
/// <see cref="decimal"/>, SurrealDB's <c>RecordId</c>, …) delegate to the
/// typed converter registered in the host <see cref="CborOptions"/> via the
/// non-generic <c>ICborConverter.Write(ref CborWriter, object)</c>
/// surface. This matches what Dahomey would do natively for
/// <c>Dictionary&lt;string, object&gt;</c> if the read-side override were
/// not registered — making it safe to register the converter globally on
/// <see cref="CborOptions"/> without breaking <c>RawQuery</c> parameter
/// serialization.</para>
/// </summary>
public sealed class CborMapToDictionaryConverter : CborConverterBase<Dictionary<string, object?>?>
{
    private readonly CborOptions _options;

    /// <summary>
    /// Constructs the converter. <paramref name="options"/> is captured so the
    /// Write side can delegate non-primitive value writes to the host
    /// converter registry (RecordId, DateTime, DateTimeOffset, etc.).
    /// </summary>
    public CborMapToDictionaryConverter(CborOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public override Dictionary<string, object?>? Read(ref CborReader reader) =>
        ReadNullableMap(ref reader);

    /// <inheritdoc />
    public override void Write(ref CborWriter writer, Dictionary<string, object?>? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }
        writer.WriteBeginMap(value.Count);
        foreach (var kv in value)
        {
            writer.WriteString(kv.Key);
            WriteValue(ref writer, kv.Value);
        }
        writer.WriteEndMap(value.Count);
    }

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

        // TODO(0.8.0): detect tagged values (DateTimeOffset tag 12, UUID tag)
        // y delegar al converter registrado en el host CborOptions. Hoy estos
        // valores dentro de `object FLEXIBLE` columns se materializan como
        // primitivas sueltas (array [int64, int32] para datetime) o null,
        // dependiendo del itemType reportado por Dahomey. La mejora requiere
        // acceso al SemanticTag del reader, que Dahomey no expone de forma
        // pública estable.
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

    private void WriteValue(ref CborWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNull(); return;
            case bool b: writer.WriteBoolean(b); return;
            case string s: writer.WriteString(s); return;
            case sbyte sb: writer.WriteInt64(sb); return;
            case short sh: writer.WriteInt64(sh); return;
            case int i: writer.WriteInt64(i); return;
            case long l: writer.WriteInt64(l); return;
            case byte by: writer.WriteUInt64(by); return;
            case ushort ush: writer.WriteUInt64(ush); return;
            case uint u: writer.WriteUInt64(u); return;
            case ulong ul: writer.WriteUInt64(ul); return;
            case float f: writer.WriteSingle(f); return;
            case double d: writer.WriteDouble(d); return;
            case Dictionary<string, object?> dict:
                WriteDict(ref writer, dict);
                return;
            case IEnumerable<object?> list:
                WriteList(ref writer, list);
                return;
        }

        // Everything else (RecordId, DateTime, DateTimeOffset, Guid, decimal,
        // user-defined records, …) dispatches to the typed converter in the
        // host CborOptions registry via the non-generic
        // `ICborConverter.Write(ref CborWriter, object)` surface — same path
        // Dahomey.Cbor uses internally when value-type erasure (`object`)
        // hides the runtime type.
        var runtimeType = value.GetType();
        var converter = _options.Registry.ConverterRegistry.Lookup(runtimeType);
        if (converter is null)
        {
            throw new CborException(
                $"No CBOR converter registered for runtime type '{runtimeType}'. Register one via " +
                $"`options.Registry.ConverterRegistry.RegisterConverter(...)` before passing values of this " +
                $"type inside a Dictionary<string, object?>.");
        }
        converter.Write(ref writer, value);
    }

    private void WriteDict(ref CborWriter writer, Dictionary<string, object?> dict)
    {
        writer.WriteBeginMap(dict.Count);
        foreach (var kv in dict)
        {
            writer.WriteString(kv.Key);
            WriteValue(ref writer, kv.Value);
        }
        writer.WriteEndMap(dict.Count);
    }

    private void WriteList(ref CborWriter writer, IEnumerable<object?> list)
    {
        // Materialise so we can write the array length up-front (CBOR
        // length-prefixed arrays are Dahomey.Cbor's default).
        var array = list as IList<object?> ?? list.ToList();
        writer.WriteBeginArray(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            WriteValue(ref writer, array[i]);
        }
        writer.WriteEndArray(array.Count);
    }
}
