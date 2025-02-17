using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

internal unsafe class PrecompiledFrozenDictionary : IReadOnlyDictionary<byte[], int>
{
    private readonly GCHandle _pinnedInput;
    private readonly int _itemsCount;
    private readonly int* _keyHashes;
    private readonly int* _values;
    private readonly int _totalKeysLength;
    private readonly byte* _keys;
    private readonly int* _keyOffsets;
    private readonly int _bucketCount;
    private readonly Bucket* _buckets;
    private readonly ulong _fastModMultiplier;

    private ReadOnlySpan<byte> RawKeys => new(_keys, _totalKeysLength);
    private ReadOnlySpan<int> KeyHashes => new(_keyHashes, _itemsCount);
    private ReadOnlySpan<int> KeyOffsets => new(_keyOffsets, _itemsCount);
    private ReadOnlySpan<int> Values => new(_values, _itemsCount);
    private ReadOnlySpan<Bucket> Buckets => new(_buckets, _bucketCount);

    private ReadOnlySpan<byte> GetKey(int index) => RawKeys.Slice(KeyOffsets[index], KeyLength(index));

    private int KeyLength(int index) => index < _itemsCount - 1
        ? KeyOffsets[index + 1] - KeyOffsets[index]
        : _totalKeysLength - KeyOffsets[index];

    public int Count => _itemsCount;

    IEnumerable<byte[]> IReadOnlyDictionary<byte[], int>.Keys => Enumerable.Range(0, _itemsCount).Select(i => GetKey(i).ToArray());
    IEnumerable<int> IReadOnlyDictionary<byte[], int>.Values => Enumerable.Range(0, _itemsCount).Select(i => Values[i]);

    public int this[byte[] key] => IndexOf(key);

    // data must be pointing to native or pinned memory and that memory needs to be pinned for the whole lifetime of the object
    // public PrecompiledBinarySearchIndexLookup(byte* data, int length)
    public PrecompiledFrozenDictionary(byte[] input)
    {
        _pinnedInput = GCHandle.Alloc(input, GCHandleType.Pinned);
        byte* data = (byte*)_pinnedInput.AddrOfPinnedObject();

        _itemsCount = 0;
        int remainingLength = input.Length;

        if (remainingLength > 0)
        {
            _itemsCount = *(int*)data;
            // Console.WriteLine($"Items count: {_itemsCount}");

            data = data + sizeof(int);
            remainingLength -= sizeof(int);
        }
        
        if (_itemsCount == 0)
        {
            _keyHashes = null;
            _keyOffsets = null;
            _keys = null;
            _values = null;
            return;
        }

        if (remainingLength < 3 * _itemsCount * sizeof(int))
        {
            throw new ArgumentException("Invalid data length (hashes, values, key offsets)");
        }

        // Console.WriteLine($"KeyHashes: {Convert.ToBase64String(new ReadOnlySpan<byte>(data, _itemsCount * sizeof(int)).ToArray())}");
        _keyHashes = (int*)data;
        data += _itemsCount * sizeof(int);
        remainingLength -= _itemsCount * sizeof(int);

        // Console.WriteLine($"Key offsets: {Convert.ToBase64String(new ReadOnlySpan<byte>(data, _itemsCount * sizeof(int)).ToArray())}");
        _keyOffsets = (int*)data;
        data += _itemsCount * sizeof(int);
        remainingLength -= _itemsCount * sizeof(int);

        // Console.WriteLine($"Values: {Convert.ToBase64String(new ReadOnlySpan<byte>(data, _itemsCount * sizeof(int)).ToArray())}");
        _values = (int*)data;
        data += _itemsCount * sizeof(int);
        remainingLength -= _itemsCount * sizeof(int);

        // buckets
        if (remainingLength < sizeof(int))
        {
            throw new ArgumentException("Invalid data length (bucket count)");
        }

        _bucketCount = *(int*)data;
        data += sizeof(int);
        remainingLength -= sizeof(int);

        if (remainingLength < _bucketCount * sizeof(Bucket))
        {
            throw new ArgumentException($"Invalid data length (buckets): requiring at least {_bucketCount} * {sizeof(Bucket)} bytes, remaining {remainingLength}");
        }

        _buckets = (Bucket*)data;
        data += _bucketCount * sizeof(Bucket);
        remainingLength -= _bucketCount * sizeof(Bucket);

        if (remainingLength < sizeof(ulong))
        {
            throw new ArgumentException("Invalid data length (fast mod multiplier)");
        }

        _fastModMultiplier = *(ulong*)data;
        data += sizeof(ulong);
        remainingLength -= sizeof(ulong);
        // Console.WriteLine($"FastModMultiplier: {_fastModMultiplier}");

        // keys are the variable length end of the data
        _keys = data;
        _totalKeysLength = remainingLength;

        // validate key offsets
        int previousOffset = -1;
        for (int i = 0; i < _itemsCount; i++)
        {
            if (_keyOffsets[i] < previousOffset || _keyOffsets[i] >= _totalKeysLength)
            {
                throw new ArgumentException("Invalid value offsets");
            }

            previousOffset = _keyOffsets[i];
        }
    }

    public static PrecompiledInfo Compile(IEnumerable<byte[]> items)
    {
        var frozenDictionary = items.Select((item, index) => (item, index)).ToDictionary().ToFrozenDictionary(XxHash3Comparer.Instance);

        var keyOffsets = new List<byte>();
        var values = new List<byte>();
        var keyHashes = new List<byte>();
        var keys = new List<byte>();
        var buckets = new List<byte>();

        if (frozenDictionary.GetType().Namespace != "System.Collections.Frozen"
            || frozenDictionary.GetType().Name != "DefaultFrozenDictionary`2")
        {
            throw new InvalidOperationException($"Expected a System.Collections.Frozen.FrozenDictionary`2, got {frozenDictionary.GetType().Name}");
        }

        object hashTable = frozenDictionary.GetType().GetField("_hashTable", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!.GetValue(frozenDictionary)!;
        Array bucketsArray = (Array)hashTable.GetType().GetField("_buckets", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!.GetValue(hashTable)!;
        ulong fastModMultiplier = (ulong)hashTable.GetType().GetField("_fastModMultiplier", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hashTable)!;
        Array hashCodesArray = (Array)hashTable.GetType().GetProperty("HashCodes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hashTable)!;

        foreach (var bucket in bucketsArray)
        {
            var start = (int)bucket.GetType().GetField("StartIndex", BindingFlags.Public | BindingFlags.Instance)!.GetValue(bucket)!;
            var end = (int)bucket.GetType().GetField("EndIndex", BindingFlags.Public | BindingFlags.Instance)!.GetValue(bucket)!;
            buckets.AddRange(BitConverter.GetBytes(start));
            buckets.AddRange(BitConverter.GetBytes(end));
        }

        foreach (var hashCode in hashCodesArray)
        {
            keyHashes.AddRange(BitConverter.GetBytes((int)hashCode));
        }

        int keyOffset = 0;

        foreach (var (key, value) in frozenDictionary)
        {
            // Console.WriteLine($"Key: {Encoding.UTF8.GetString(key)}, value: {value}");
            values.AddRange(BitConverter.GetBytes(value));
            keys.AddRange(key);
            keyOffsets.AddRange(BitConverter.GetBytes(keyOffset));
            keyOffset += key.Length;
        }

        // Console.WriteLine($"Dictionary: {frozenDictionary.Count}");
        // Console.WriteLine($"KeyOffsets: {keyOffsets.Count}");
        // Console.WriteLine($"Keys: {keys.Count}");
        // Console.WriteLine($"Values: {values.Count}, {Convert.ToBase64String(values.ToArray())}");
        // Console.WriteLine($"Buckets arr: {bucketsArray.Length}");
        // Console.WriteLine($"Buckets: {buckets.Count}");
        // Console.WriteLine($"FastModMultiplier: {fastModMultiplier}");

        return new PrecompiledInfo
        {
            Indexes = frozenDictionary,
            RawBytes = [
                ..BitConverter.GetBytes(frozenDictionary.Count),
                ..keyHashes,
                ..keyOffsets,
                ..values,
                ..BitConverter.GetBytes(bucketsArray.Length),
                ..buckets,
                ..BitConverter.GetBytes(fastModMultiplier),
                ..keys,
            ],
        };
    }

    // public void Dump()
    // {
    //     for (int i = 0; i < _itemsCount; i++)
    //     {
    //         Console.WriteLine($"{Encoding.UTF8.GetString(GetKey(i))} => {Values[i]}");
    //     }
    // }

    public bool ContainsKey(byte[] key) => IndexOf(key) >= 0;

    public bool TryGetValue(byte[] key, [MaybeNullWhen(false)] out int value)
    {
        var index = IndexOf(key);
        if (index < 0)
        {
            value = 0;
            return false;
        }

        value = Values[index];
        return true;
    }

    // this is copy&pasted from FrozenDictionary, ideally this could be reused
    private void FindMatchingEntries(int hashCode, out int startIndex, out int endIndex)
    {
        Bucket b = _buckets[(int)FastMod((uint)hashCode, (uint)_bucketCount, _fastModMultiplier)];
        startIndex = b.StartIndex;
        endIndex = b.EndIndex;

        static uint FastMod(uint value, uint divisor, ulong multiplier)
        {
            Debug.Assert(divisor <= int.MaxValue);
            uint highbits = (uint)(((((multiplier * value) >> 32) + 1) * divisor) >> 32);
            Debug.Assert(highbits == value % divisor);
            return highbits;
        }
    }

    // this is copy&pasted from FrozenDictionary (just slightly modified), ideally this could be reused
    private int IndexOf(ReadOnlySpan<byte> key)
    {
        int hashCode = XxHash3Comparer.Instance.GetHashCode(key);
        FindMatchingEntries(hashCode, out int index, out int endIndex);

        while (index <= endIndex)
        {
            if (hashCode == KeyHashes[index])
            {
                if (XxHash3Comparer.Instance.Equals(key, GetKey(index)))
                {
                    return _values[index];
                }
            }
            index++;
        }

        return -1;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<KeyValuePair<byte[], int>> GetEnumerator()
        => Enumerable.Range(0, _itemsCount).Select(i => new KeyValuePair<byte[], int>(GetKey(i).ToArray(), i)).GetEnumerator();

    public class PrecompiledInfo
    {
        public required IReadOnlyDictionary<byte[], int> Indexes { get; init; }
        public required byte[] RawBytes { get; init; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Bucket
    {
        public readonly int StartIndex;
        public readonly int EndIndex;
    }
}
