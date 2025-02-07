using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;

internal unsafe class PrecompiledBinarySearchIndexLookup : IReadOnlyDictionary<byte[], int>
{
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

    public PrecompiledBinarySearchIndexLookup(byte* data, int length)
    {
        _itemsCount = 0;
        int remainingLength = length;

        if (length > 0)
        {
            _itemsCount = *(int*)data;

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
        var sortedValues = items.OrderBy(e => e, new XxHash3Comparer()).ToArray();

        List<byte> hashes = new();
        List<byte> valueOffsets = new();
        List<byte> values = new();

        int offset = 0;
        foreach (var entry in sortedValues)
        {
            hashes.AddRange(BitConverter.GetBytes(Hash(entry)));
            valueOffsets.AddRange(BitConverter.GetBytes(offset));
            values.AddRange(entry);
            offset += entry.Length;
        }

        return new PrecompiledInfo
        {
            Indexes = sortedValues.Select((e, i) => (e, i)).ToDictionary(new XxHash3Comparer()),
            RawBytes = [
                ..BitConverter.GetBytes(sortedValues.Length),
                ..hashes,
                ..valueOffsets,
                ..values,
            ],
        };
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
        int valueHash = Hash(value);

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

    private static int Hash(ReadOnlySpan<byte> value)
        => unchecked((int)XxHash3.HashToUInt64(value));


    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<KeyValuePair<byte[], int>> GetEnumerator()
        => Enumerable.Range(0, _itemsCount).Select(i => new KeyValuePair<byte[], int>(GetValue(i).ToArray(), i)).GetEnumerator();

    public class PrecompiledInfo
    {
        public required IReadOnlyDictionary<byte[], int> Indexes { get; init; }
        public required byte[] RawBytes { get; init; }
    }

    // TODO there must be some built-in comparer for this
    private sealed class XxHash3Comparer : IEqualityComparer<byte[]>, IComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            return x.AsSpan().SequenceEqual(y);
        }

        public int Compare(byte[]? x, byte[]? y)
        {
            if (x is null || y is null) return x is null ? y is null ? 0 : -1 : 1;
            var xy = Hash(x).CompareTo(Hash(y));
            return xy != 0 ? xy : MemoryExtensions.SequenceCompareTo(x.AsSpan(), y.AsSpan());
        }

        public bool Equals(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y) => x.SequenceEqual(y);
        public int GetHashCode(ReadOnlySpan<byte> obj) => unchecked((int)XxHash3.HashToUInt64(obj));
        public int GetHashCode(byte[] obj) => unchecked((int)XxHash3.HashToUInt64(obj.AsSpan()));
    }

}
