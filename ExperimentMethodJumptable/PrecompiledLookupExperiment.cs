using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;

internal unsafe class PrecompiledBinarySearchIndexLookup : IReadOnlyDictionary<byte[], int>
{
    private readonly GCHandle _pinnedInput;
    private readonly int _itemsCount;
    private readonly int* _hashes;
    private readonly int* _valueOffsets;
    private readonly byte* _values;
    private int _totalValuesLength;

    private ReadOnlySpan<int> Hashes => new(_hashes, _itemsCount);
    private ReadOnlySpan<int> ValueOffsets => new(_valueOffsets, _itemsCount);
    private ReadOnlySpan<byte> AllValues => new(_values, _totalValuesLength);

    private ReadOnlySpan<byte> GetValue(int index)
        => AllValues.Slice(ValueOffsets[index], ValueLength(index));

    private int ValueLength(int index) => index < _itemsCount - 1
        ? ValueOffsets[index + 1] - ValueOffsets[index]
        : _totalValuesLength - ValueOffsets[index];

    public int Count => _itemsCount;

    public IEnumerable<byte[]> Keys => Values.Select(i => GetValue(i).ToArray());
    public IEnumerable<int> Values => Enumerable.Range(0, _itemsCount);

    public int this[byte[] key] => IndexOf(key);

    // data must be pointing to native or pinned memory and that memory needs to be pinned for the whole lifetime of the object
    // public PrecompiledBinarySearchIndexLookup(byte* data, int length)
    public PrecompiledBinarySearchIndexLookup(byte[] input)
    {
        _pinnedInput = GCHandle.Alloc(input, GCHandleType.Pinned);
        byte* data = (byte*)_pinnedInput.AddrOfPinnedObject();

        _itemsCount = 0;
        int remainingLength = input.Length;

        if (remainingLength > 0)
        {
            _itemsCount = *(int*)data;
            Console.WriteLine($"Items count: {_itemsCount}");

            data = data + sizeof(int);
            remainingLength -= sizeof(int);
        }
        
        if (_itemsCount == 0)
        {
            _hashes = null;
            _valueOffsets = null;
            _values = null;
            return;
        }

        if (remainingLength < 2 * _itemsCount * sizeof(int))
        {
            throw new ArgumentException("Invalid data length");
        }

        _hashes = (int*)data;

        data += _itemsCount * sizeof(int);
        remainingLength -= _itemsCount * sizeof(int);

        _valueOffsets = (int*)data;

        data += _itemsCount * sizeof(int);
        remainingLength -= _itemsCount * sizeof(int);

        _values = data;
        _totalValuesLength = remainingLength;

        int previousOffset = -1;
        for (int i = 0; i < _itemsCount; i++)
        {
            if (_valueOffsets[i] < previousOffset || _valueOffsets[i] >= _totalValuesLength)
            {
                throw new ArgumentException("Invalid value offsets");
            }

            previousOffset = _valueOffsets[i];
        }
    }

    public static PrecompiledInfo Compile(IEnumerable<byte[]> items)
    {
        // ??? this ordering did not work as expected?
        var sortedValues = items.OrderBy(e => e, XxHash3Comparer.Instance).ToArray();

        List<byte> hashes = new();
        List<byte> valueOffsets = new();
        List<byte> values = new();

        int offset = 0;
        foreach (var entry in sortedValues)
        {
            Console.WriteLine($"Entry: {Encoding.UTF8.GetString(entry)}, hash: {XxHash3Comparer.Hash(entry)}, offset: {offset}");

            hashes.AddRange(BitConverter.GetBytes(XxHash3Comparer.Hash(entry)));
            valueOffsets.AddRange(BitConverter.GetBytes(offset));
            values.AddRange(entry);
            offset += entry.Length;
        }

        return new PrecompiledInfo
        {
            Indexes = sortedValues.Select((e, i) => (e, i)).ToDictionary(XxHash3Comparer.Instance),
            RawBytes = [
                ..BitConverter.GetBytes(sortedValues.Length),
                ..hashes,
                ..valueOffsets,
                ..values,
            ],
        };
    }

    public void Dump()
    {
        foreach (var k in Keys)
        {
            Console.WriteLine($"{Encoding.UTF8.GetString(k)} => {this[k]}");
        }
    }

    public bool ContainsKey(byte[] key) => IndexOf(key) >= 0;

    public bool TryGetValue(byte[] key, [MaybeNullWhen(false)] out int value)
    {
        value = IndexOf(key);
        return value >= 0;
    }

    // Taken from MemoryExtensions.BinarySearch
    private int IndexOf(ReadOnlySpan<byte> value)
    {
        int valueHash = XxHash3Comparer.Hash(value);

        int lo = 0;
        int hi = _itemsCount - 1;

        // If length == 0, hi == -1, and loop will not be entered
        while (lo <= hi)
        {
            // PERF: `lo` or `hi` will never be negative inside the loop,
            //       so computing median using uints is safe since we know
            //       `length <= int.MaxValue`, and indices are >= 0
            //       and thus cannot overflow an uint.
            //       Saves one subtraction per loop compared to
            //       `int i = lo + ((hi - lo) >> 1);`
            int i = (int)(((uint)hi + (uint)lo) >> 1);


            int c = CompareTo(value, valueHash, i);
            if (c == 0)
            {
                return i;
            }
            else if (c > 0)
            {
                lo = i + 1;
            }
            else
            {
                hi = i - 1;
            }
        }
        // If none found, then a negative number that is the bitwise complement
        // of the index of the next element that is larger than or, if there is
        // no larger element, the bitwise complement of `length`, which
        // is `lo` at this point.
        return ~lo;
    }

    // This method must match `XxHash3Comparer`
    private int CompareTo(ReadOnlySpan<byte> value, int hash, int index)
    {
        int hashCmp = hash.CompareTo(Hashes[index]);
        return hashCmp != 0 ? hashCmp : MemoryExtensions.SequenceCompareTo(value, GetValue(index));
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<KeyValuePair<byte[], int>> GetEnumerator()
        => Enumerable.Range(0, _itemsCount).Select(i => new KeyValuePair<byte[], int>(GetValue(i).ToArray(), i)).GetEnumerator();

    public class PrecompiledInfo
    {
        public required IReadOnlyDictionary<byte[], int> Indexes { get; init; }
        public required byte[] RawBytes { get; init; }
    }
}

// TODO there must be some built-in comparer for this
internal sealed class XxHash3Comparer : IEqualityComparer<byte[]>, IEqualityComparer<ReadOnlySpan<byte>>, IComparer<byte[]>
{
    public static XxHash3Comparer Instance { get; } = new XxHash3Comparer();

    public bool Equals(byte[]? x, byte[]? y)
    {
        if (x is null && y is null) return true;
        if (x is null || y is null) return false;
        return x.AsSpan().SequenceEqual(y);
    }

    public int Compare(byte[]? x, byte[]? y)
    {
        System.Diagnostics.Debug.Assert(x is not null && y is not null);
        var xy = Hash(x).CompareTo(Hash(y));
        return xy != 0 ? xy : MemoryExtensions.SequenceCompareTo(x.AsSpan(), y.AsSpan());
    }

    public bool Equals(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y) => x.SequenceEqual(y);
    public int GetHashCode(ReadOnlySpan<byte> obj) => unchecked((int)XxHash3.HashToUInt64(obj));
    public int GetHashCode(byte[] obj) => unchecked((int)XxHash3.HashToUInt64(obj.AsSpan()));

    public static int Hash(ReadOnlySpan<byte> value)
        => unchecked((int)XxHash3.HashToUInt64(value));
}
