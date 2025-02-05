using System.IO.Hashing;

internal unsafe class PrecompiledLookup
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

    public PrecompiledLookup(byte* data, int length)
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

    public static PrecompiledLookupInfo Compile(IEnumerable<byte[]> items)
    {
        var sortedValues = items.OrderBy(e => e, new ByteArrayComparer()).ToArray();

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

        return new PrecompiledLookupInfo
        {
            ValueIndexes = sortedValues.Select((e, i) => (e, i)).ToDictionary(new XxHash3Comparer()),
            RawBytes = [
                ..BitConverter.GetBytes(sortedValues.Length),
                ..hashes,
                ..valueOffsets,
                ..values,
            ],
        };
    }

    // TODO there must be some built-in comparer
    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public int Compare(byte[]? x, byte[]? y)
        {
            if (x is null || y is null) return x is null ? y is null ? 0 : -1 : 1;
            int xy = Hash(x).CompareTo(Hash(y));
            return xy != 0 ? xy : MemoryExtensions.SequenceCompareTo(x.AsSpan(), y.AsSpan());
        }
    }

    public int IndexOf(ReadOnlySpan<byte> value)
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

            int hashCmp = valueHash.CompareTo(Hashes[i]);
            int c = hashCmp != 0 ? hashCmp : MemoryExtensions.SequenceCompareTo(value, GetValue(i));

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

    // TODO use proper hash function
    private static int Hash(ReadOnlySpan<byte> value)
        => unchecked((int)XxHash3.HashToUInt64(value));

    private static int Compare(ReadOnlySpan<byte> left, int leftHash, ReadOnlySpan<byte> right, int rightHash)
    {
        int hashCmp = leftHash.CompareTo(rightHash);
        return hashCmp != 0 ? hashCmp : left.SequenceCompareTo(right);
    }

    public class PrecompiledLookupInfo
    {
        public required Dictionary<byte[], int> ValueIndexes { get; init; }
        public required byte[] RawBytes { get; init; }
    }

    private sealed class XxHash3Comparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            return x.AsSpan().SequenceEqual(y);
        }

        public bool Equals(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y) => x.SequenceEqual(y);
        public int GetHashCode(ReadOnlySpan<byte> obj) => unchecked((int)XxHash3.HashToUInt64(obj));
        public int GetHashCode(byte[] obj) => unchecked((int)XxHash3.HashToUInt64(obj.AsSpan()));
    }

}
