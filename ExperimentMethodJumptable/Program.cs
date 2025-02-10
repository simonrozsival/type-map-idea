using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// Idea: Using method bodies to store typerefs and memberrefs for efficient loading of types and method delegates.
//       We just build a lmethodKeye method(*) which can return any of the types or method delegates and create a jumptable
//       to jump to exactly to the right place - using a switch statement with cases being sequential numbers.
//       To map the class names to the consecutive numbers [0-N) we use a pre-compiled frozen dictionary.
//
//   - the lookup tables and the frozen dictionary need to be generated & precompiled after trimming
//   - works with all platforms and runtimes - including AOT and R2R
//   - the final code is AOT and trim safe
//   - it avoid loading types from other assemblies via one extra level of indirection (works also for R2R)
//
//   (*) in some cases when there are too many cases we need to split this into 2 (or more?) nesting levels
//       as described later with a reference to the xamarin-macios docs
//
// Other considerations:
//   - JITting this lookup method at startup might be an issue - R2R would be very beneficial





// TestHandwrittenMethodMapping();
// TestHandwrittenTypeMapping();
// TestPrecompiledBinarySearchIndexLookup();
// TestJavaInteropTypeMapPrototype();
TestJavaInteropFunctionPointerMapPrototype();




// ----

void TestHandwrittenMethodMapping()
{
    // 1. method map
    var map = new MethodMapping(new byte[0]);
    // var ptr = map.Invoke_CreateInstance("JavaClass33", jthis, transferOptions);
    
    // we want to call ... JavaClass333.GetFunctionPointer(42); but we know just the Java class name so we have to do...
    var ptr = map.Invoke_GetFunctionPointer("JavaClass33", 42); 
    Console.WriteLine(ptr); // 3 * 42 = 126
}

void TestHandwrittenTypeMapping()
{
    // 1. type map
    var map = new TypeMapping(new byte[0]);
    
    // we want to call ... JavaClass333.GetFunctionPointer(42); but we know just the Java class name so we have to do...
    var type = map.GetType("JavaClass33"); 
    Console.WriteLine(type); // "Class33"
}

unsafe void TestPrecompiledBinarySearchIndexLookup()
{
    // Sorted values
    byte[][] values = ["JavaClass0"u8.ToArray(), "JavaClass1"u8.ToArray(), "JavaClass2"u8.ToArray(), "JavaClass3"u8.ToArray(), "JavaClass4"u8.ToArray(), "JavaClass5"u8.ToArray(), "JavaClass6"u8.ToArray(), "JavaClass7"u8.ToArray(), "JavaClass8"u8.ToArray(), "JavaClass9"u8.ToArray(), "JavaClass10"u8.ToArray(), "JavaClass11"u8.ToArray(), "JavaClass12"u8.ToArray(), "JavaClass13"u8.ToArray(), "JavaClass14"u8.ToArray(), "JavaClass15"u8.ToArray(), "JavaClass16"u8.ToArray(), "JavaClass17"u8.ToArray(), "JavaClass18"u8.ToArray(), "JavaClass19"u8.ToArray(), "JavaClass20"u8.ToArray(), "JavaClass21"u8.ToArray(), "JavaClass22"u8.ToArray(), "JavaClass23"u8.ToArray(), "JavaClass24"u8.ToArray(), "JavaClass25"u8.ToArray(), "JavaClass26"u8.ToArray(), "JavaClass27"u8.ToArray(), "JavaClass28"u8.ToArray(), "JavaClass29"u8.ToArray(), "JavaClass30"u8.ToArray(), "JavaClass31"u8.ToArray(), "JavaClass32"u8.ToArray(), "JavaClass33"u8.ToArray(), "JavaClass34"u8.ToArray(), "JavaClass35"u8.ToArray(), "JavaClass36"u8.ToArray(), "JavaClass37"u8.ToArray(), "JavaClass38"u8.ToArray(), "JavaClass39"u8.ToArray(), "OtherJavaClass1"u8.ToArray()];

    // the precompiled info will tell us in which order the values are stored
    // - they are just sorted so we can use binary search at the moment
    // - but the order might be different if we actually encode the frozen dictionary there
    var precompiledInfo = PrecompiledBinarySearchIndexLookup.Compile(values);
    var base64 = Convert.ToBase64String(precompiledInfo.RawBytes);

    Console.WriteLine($"Precompiled: {base64}");

    // ----

    // byte[] rawBytes = Convert.FromBase64String(base64);
    
    // I got this from the output of the previous run:
    byte[] rawBytes = Convert.FromBase64String("KQAAAOEjBISK1BeFgEGkjEfJ7JRwGgWWYDYcm/O6A57VEN6kBvPKq0QKIq5BQy++8MQ1x8iYJc+fVyTR0ZdJ3J+o9dyhTSLhQUzv5PedSPpJ5TkJVriWI/6ZUScE0acuHvbVLrUeKDEUpFQ3QN5SOevFczl6jp4+ckO1ThGMyk640/5PpMYLU0j4Klu3Y3NcytAzZux4/WjJH0p1gWFmedqNo331VYl+AAAAAAoAAAAVAAAAIAAAACsAAAA2AAAAQQAAAEsAAABWAAAAYAAAAGsAAAB2AAAAgQAAAIwAAACWAAAAoQAAAKwAAAC3AAAAwQAAAMwAAADXAAAA4gAAAO0AAAD4AAAAAgEAABEBAAAbAQAAJgEAADABAAA6AQAARQEAAFABAABbAQAAZgEAAHABAAB7AQAAhgEAAJEBAACcAQAApwEAALIBAABKYXZhQ2xhc3MySmF2YUNsYXNzMThKYXZhQ2xhc3MxNUphdmFDbGFzczE2SmF2YUNsYXNzMzhKYXZhQ2xhc3MzNkphdmFDbGFzczdKYXZhQ2xhc3MzMUphdmFDbGFzczlKYXZhQ2xhc3MyN0phdmFDbGFzczExSmF2YUNsYXNzMjFKYXZhQ2xhc3MxMEphdmFDbGFzczRKYXZhQ2xhc3MzN0phdmFDbGFzczMwSmF2YUNsYXNzMjZKYXZhQ2xhc3M1SmF2YUNsYXNzMTJKYXZhQ2xhc3MzMkphdmFDbGFzczI5SmF2YUNsYXNzMzlKYXZhQ2xhc3MyNUphdmFDbGFzczFPdGhlckphdmFDbGFzczFKYXZhQ2xhc3MwSmF2YUNsYXNzMjJKYXZhQ2xhc3MzSmF2YUNsYXNzOEphdmFDbGFzczE0SmF2YUNsYXNzMjhKYXZhQ2xhc3MyMEphdmFDbGFzczE5SmF2YUNsYXNzNkphdmFDbGFzczM0SmF2YUNsYXNzMTdKYXZhQ2xhc3MxM0phdmFDbGFzczMzSmF2YUNsYXNzMzVKYXZhQ2xhc3MyM0phdmFDbGFzczI0");
    // - this is just to show that the precompiled info is always stable
    // - the precompiled Lookup can be used to generate some additional code where the indexes of those values can baked into something like a jumptable

    // // make sure we don't cheat
    // byte* nativeMemory = (byte*)NativeMemory.AllocZeroed((nuint)rawBytes.Length);
    // NativeMemory.Copy(Unsafe.AsPointer(ref precompiledInfo.RawBytes[0]), nativeMemory, (nuint)rawBytes.Length);
    // var hydrated = new PrecompiledBinarySearchIndexLookup(nativeMemory, rawBytes.Length);

    var hydrated = new PrecompiledBinarySearchIndexLookup(rawBytes);

    int correct = 0;
    int incorrect = 0;

    foreach (var value in values)
    {
        var expected = precompiledInfo.Indexes[value];
        var actual = hydrated[value];

        if (expected != actual)
        {
            Console.WriteLine($"\"{Encoding.UTF8.GetString(value)}\" => {actual} (expected {expected})");
            incorrect++;
        }
        else
        {
            correct++;
        }
    }

    if (incorrect == 0)
    {
        Console.WriteLine($"All {correct} values are correct");
    }
    else
    {
        Console.WriteLine($"Correct: {correct}, Incorrect: {incorrect}");
    }
}

void TestJavaInteropTypeMapPrototype()
{
    var map = JavaInteropTypeMap.Create();
    var ptr = map.GetFunctionPointer("JavaClass3"u8, methodKey: 2);
    Console.WriteLine($"JavaClass3.GetFunctionPointer(2) = {ptr} (expecting 6)");
}

unsafe void TestJavaInteropFunctionPointerMapPrototype()
{
    var map = JavaInteropFunctionPointerMap.Create();
    var fnptr = map.GetFunctionPointer("JavaClass3"u8);
    var ptr = fnptr(2);
    Console.WriteLine($"JavaClass3.GetFunctionPointer(2) = {ptr} (expecting 6)");
}

class MethodMapping
{
    // we would want to "hydrate" this dictionary from a byte span
    private readonly FrozenDictionary<string, int> _indexMap;
    // private readonly FrozenDictionary<byte[], int> _indexMap; -- for UTF-8 string keys?
    // private readonly FrozenDictionary<Guid, int> _indexMap; -- for COM?
    // string[] _keys; // or byte[][] _keys;
    // int[] _hashes;
    // (int, int)[] _buckets;
    // int[] _values;

    public MethodMapping(ReadOnlySpan<byte> data)
    {
        // build the "frozen dictionary" from the `data` span
        _indexMap = new Dictionary<string, int>
        {
            ["JavaClass0"] = 0,
            ["JavaClass1"] = 1,
            ["JavaClass2"] = 2,
            ["JavaClass3"] = 3,
            ["JavaClass4"] = 4,
            ["JavaClass5"] = 5,
            ["JavaClass6"] = 6,
            ["JavaClass7"] = 7,
            ["JavaClass8"] = 8,
            ["JavaClass9"] = 9,
            ["JavaClass10"] = 10,
            ["JavaClass11"] = 11,
            ["JavaClass12"] = 12,
            ["JavaClass13"] = 13,
            ["JavaClass14"] = 14,
            ["JavaClass15"] = 15,
            ["JavaClass16"] = 16,
            ["JavaClass17"] = 17,
            ["JavaClass18"] = 18,
            ["JavaClass19"] = 19,
            ["JavaClass20"] = 20,
            ["JavaClass21"] = 21,
            ["JavaClass22"] = 22,
            ["JavaClass23"] = 23,
            ["JavaClass24"] = 24,
            ["JavaClass25"] = 25,
            ["JavaClass26"] = 26,
            ["JavaClass27"] = 27,
            ["JavaClass28"] = 28,
            ["JavaClass29"] = 29,
            ["JavaClass30"] = 30,
            ["JavaClass31"] = 31,
            ["JavaClass32"] = 32,
            ["JavaClass33"] = 33,
            ["JavaClass34"] = 34,
            ["JavaClass35"] = 35,
            ["JavaClass36"] = 36,
            ["JavaClass37"] = 37,
            ["JavaClass38"] = 38,
            ["JavaClass39"] = 39,
            ["OtherJavaClass1"] = 40,
        }.ToFrozenDictionary();
    }

    // public Java.Lang.Object Invoke_CreateInstance(string javaClassName, IntPtr jthis, int transferOptions)
    // {
    //     // if (!_indexMap.TryGetValue(javaClassName, out var index))
    //     // {
    //     //     throw new ArgumentException($"Unknown Java class name: {javaClassName}");
    //     // }

        
    // }

    public IntPtr Invoke_GetFunctionPointer(string javaClassName, int vtableSlot)
    {
        // Could this method get too big to jit? --- https://github.com/xamarin/xamarin-macios/blob/main/docs/managed-static-registrar.md#method-mapping
        // how could we represent this method as a hash table?

        if (!_indexMap.TryGetValue(javaClassName, out var index))
        {
            throw new ArgumentException($"Unknown Java class name: {javaClassName}");
        }

        // will the switch work even if there are too wide gaps between the cases?
        // -> it seems it won't!
        // - BUT if this method is generated _after_ trimming then there won't be any gaps
        //   and it can be a simple jump table
        // - we can do the bucketing 2 level (or even multiple levels) nested jumptables as described in the xamarin-macios doc
        //
        // IL_002c: ldloc.0
        // IL_002d: switch (IL_00db, IL_00e7, IL_00f3, IL_00ff, IL_010b, IL_0117, IL_0123, IL_012f, IL_013b, IL_0147, IL_0153, IL_015f, IL_016b, IL_0177, IL_0183, IL_018f, IL_019b, IL_01a7, IL_01b3, IL_01bf, IL_01cb, IL_01d7, IL_01e3, IL_01ef, IL_01fb, IL_0207, IL_0213, IL_021f, IL_022b, IL_0234, IL_023d, IL_0246, IL_024f, IL_0258, IL_0261, IL_026a, IL_0273, IL_027c, IL_0285, IL_028e, IL_0297)
        //
        // IL_00d6: br IL_02a0
        //
        // IL_00db: ldmethodKey.2monodroid_typemap_java_to_managed
        // IL_00dc: call native int JavaClass0::GetFunctionPointer(int32)
        // IL_00e1: stloc.2
        // IL_00e2: br IL_02ab
        //
        // IL_00e7: ldmethodKey.2
        // IL_00e8: call native int JavaClass1::GetFunctionPointer(int32)
        // IL_00ed: stloc.2
        // IL_00ee: br IL_02ab
        //
        // IL_00f3: ldmethodKey.2
        // IL_00f4: call native int JavaClass2::GetFunctionPointer(int32)
        // IL_00f9: stloc.2
        // IL_00fa: br IL_02ab
        //
        // ...

        return index switch
        {
            // when this method is jitted, will it cause all the types to be loaded? or are those just `ldftn <memberref>` instructions that won't cause the type to load?
            // how do I test that?
            0 => Class0.GetFunctionPointer(vtableSlot),
            1 => Class1.GetFunctionPointer(vtableSlot),
            2 => Class2.GetFunctionPointer(vtableSlot),
            3 => Class3.GetFunctionPointer(vtableSlot),
            4 => Class4.GetFunctionPointer(vtableSlot),
            5 => Class5.GetFunctionPointer(vtableSlot),
            6 => Class6.GetFunctionPointer(vtableSlot),
            7 => Class7.GetFunctionPointer(vtableSlot),
            8 => Class8.GetFunctionPointer(vtableSlot),
            9 => Class9.GetFunctionPointer(vtableSlot),
            10 => Class10.GetFunctionPointer(vtableSlot),
            11 => Class11.GetFunctionPointer(vtableSlot),
            12 => Class12.GetFunctionPointer(vtableSlot),
            13 => Class13.GetFunctionPointer(vtableSlot),
            14 => Class14.GetFunctionPointer(vtableSlot),
            15 => Class15.GetFunctionPointer(vtableSlot),
            16 => Class16.GetFunctionPointer(vtableSlot),
            17 => Class17.GetFunctionPointer(vtableSlot),
            18 => Class18.GetFunctionPointer(vtableSlot),
            19 => Class19.GetFunctionPointer(vtableSlot),
            20 => Class20.GetFunctionPointer(vtableSlot),
            21 => Class21.GetFunctionPointer(vtableSlot),
            22 => Class22.GetFunctionPointer(vtableSlot),
            23 => Class23.GetFunctionPointer(vtableSlot),
            24 => Class24.GetFunctionPointer(vtableSlot),
            25 => Class25.GetFunctionPointer(vtableSlot),
            26 => Class26.GetFunctionPointer(vtableSlot),
            27 => Class27.GetFunctionPointer(vtableSlot),
            28 => Class28.GetFunctionPointer(vtableSlot),
            29 => Class29.GetFunctionPointer(vtableSlot),
            30 => Class30.GetFunctionPointer(vtableSlot),
            31 => Class31.GetFunctionPointer(vtableSlot),
            32 => Class32.GetFunctionPointer(vtableSlot),
            33 => Class33.GetFunctionPointer(vtableSlot),
            34 => Class34.GetFunctionPointer(vtableSlot),
            35 => Class35.GetFunctionPointer(vtableSlot),
            36 => Class36.GetFunctionPointer(vtableSlot),
            37 => Class37.GetFunctionPointer(vtableSlot),
            38 => Class38.GetFunctionPointer(vtableSlot),
            39 => Class39.GetFunctionPointer(vtableSlot),
            // 40 => ExperimentMethodJumptable_SecondAssembly.OtherClass1.GetFunctionPointer(methodKey), // --- second assembly module initializer ran!
            40 => Get_ExperimentMethodJumptable_SecondAssembly_OtherClass1_GetFunctionPointer(vtableSlot), // --- second assembly module initializer didn't run! (with R2R it does, is it a problem though?)
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        // when there are big gaps between the case numbers, it is not a simple jumptable:
        //
        // return index switch
        // {
        //     // when this method is jitted, will it cause all the types to be loaded? or are those just `ldftn <memberref>` instructions that won't cause the type to load?
        //     // how do I test that?
        //     0000 => JavaClass0.GetFunctionPointer,
        //     // 1000 => JavaClass1.GetFunctionPointer,
        //     // 2000 => JavaClass2.GetFunctionPointer,
        //     3000 => JavaClass3.GetFunctionPointer,
        //     4000 => JavaClass4.GetFunctionPointer,
        //     // 5000 => JavaClass5.GetFunctionPointer,
        //     // 6000 => JavaClass6.GetFunctionPointer,
        //     7000 => JavaClass7.GetFunctionPointer,
        //     8000 => JavaClass8.GetFunctionPointer,
        //     // 9000 => JavaClass9.GetFunctionPointer,
        //     10000 => JavaClass10.GetFunctionPointer,
        //     11000 => JavaClass11.GetFunctionPointer,
        //     12000 => JavaClass12.GetFunctionPointer,
        //     // 13000 => JavaClass13.GetFunctionPointer,
        //     // 14000 => JavaClass14.GetFunctionPointer,
        //     15000 => JavaClass15.GetFunctionPointer,
        //     16000 => JavaClass16.GetFunctionPointer,
        //     17000 => JavaClass17.GetFunctionPointer,
        //     // 18000 => JavaClass18.GetFunctionPointer,
        //     // 19000 => JavaClass19.GetFunctionPointer,
        //     // 20000 => JavaClass20.GetFunctionPointer,
        //     // 21000 => JavaClass21.GetFunctionPointer,
        //     // 22000 => JavaClass22.GetFunctionPointer,
        //     // 23000 => JavaClass23.GetFunctionPointer,
        //     // 24000 => JavaClass24.GetFunctionPointer,
        //     // 25000 => JavaClass25.GetFunctionPointer,
        //     // 26000 => JavaClass26.GetFunctionPointer,
        //     27000 => JavaClass27.GetFunctionPointer,
        //     28000 => JavaClass28.GetFunctionPointer,
        //     // 29000 => JavaClass29.GetFunctionPointer,
        //     // 30000 => JavaClass30.GetFunctionPointer,
        //     // 31000 => JavaClass31.GetFunctionPointer,
        //     // 32000 => JavaClass32.GetFunctionPointer,
        //     33000 => JavaClass33.GetFunctionPointer,
        //     // 34000 => JavaClass34.GetFunctionPointer,
        //     // 35000 => JavaClass35.GetFunctionPointer,
        //     // 36000 => JavaClass36.GetFunctionPointer,
        //     // 37000 => JavaClass37.GetFunctionPointer,
        //     // 38000 => JavaClass38.GetFunctionPointer,
        //     39000 => JavaClass39.GetFunctionPointer,
        //     _ => throw new ArgumentOutOfRangeException(nameof(index))
        // };
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // -- this is necessary for R2R to skip loading the other assembly unless the type is actually requested
    private static IntPtr Get_ExperimentMethodJumptable_SecondAssembly_OtherClass1_GetFunctionPointer(int methodKey)
        => ExperimentMethodJumptable_SecondAssembly.OtherClass1.GetFunctionPointer(methodKey);
}

class TypeMapping
{
    // int[] _hashes;
    // string[] _keys;
    // (int, int)[] _buckets;

    private readonly FrozenDictionary<string, int> _indexMap;
    // private readonly FrozenDictionary<string, TBD> _indexMap;

    public TypeMapping(ReadOnlySpan<byte> data)
    {
        // build the "frozen dictionary" from the `data` span
        _indexMap = new Dictionary<string, int>
        {
            ["JavaClass0"] = 0,
            ["JavaClass1"] = 1,
            ["JavaClass2"] = 2,
            ["JavaClass3"] = 3,
            ["JavaClass4"] = 4,
            ["JavaClass5"] = 5,
            ["JavaClass6"] = 6,
            ["JavaClass7"] = 7,
            ["JavaClass8"] = 8,
            ["JavaClass9"] = 9,
            ["JavaClass10"] = 10,
            ["JavaClass11"] = 11,
            ["JavaClass12"] = 12,
            ["JavaClass13"] = 13,
            ["JavaClass14"] = 14,
            ["JavaClass15"] = 15,
            ["JavaClass16"] = 16,
            ["JavaClass17"] = 17,
            ["JavaClass18"] = 18,
            ["JavaClass19"] = 19,
            ["JavaClass20"] = 20,
            ["JavaClass21"] = 21,
            ["JavaClass22"] = 22,
            ["JavaClass23"] = 23,
            ["JavaClass24"] = 24,
            ["JavaClass25"] = 25,
            ["JavaClass26"] = 26,
            ["JavaClass27"] = 27,
            ["JavaClass28"] = 28,
            ["JavaClass29"] = 29,
            ["JavaClass30"] = 30,
            ["JavaClass31"] = 31,
            ["JavaClass32"] = 32,
            ["JavaClass33"] = 33,
            ["JavaClass34"] = 34,
            ["JavaClass35"] = 35,
            ["JavaClass36"] = 36,
            ["JavaClass37"] = 37,
            ["JavaClass38"] = 38,
            ["JavaClass39"] = 39,
            ["OtherJavaClass1"] = 40,
        }.ToFrozenDictionary();
    }

    public Type GetType(string javaClassName)
    {
        // Could this method get too big to jit? --- https://github.com/xamarin/xamarin-macios/blob/main/docs/managed-static-registrar.md#method-mapping
        // how could we represent this method as a hash table?

        if (!_indexMap.TryGetValue(javaClassName, out var index))
        {
            throw new ArgumentException($"Unknown Java class name: {javaClassName}");
        }

        // IL_002c: ldloc.0
        // IL_002d: switch (IL_00db, IL_00eb, IL_00fb, IL_010b, IL_011b, IL_012b, IL_013b, IL_014b, IL_015b, IL_016b, IL_017b, IL_018b, IL_019b, IL_01ab, IL_01bb, IL_01cb, IL_01db, IL_01eb, IL_01fb, IL_020b, IL_021b, IL_022b, IL_023b, IL_024b, IL_025b, IL_026b, IL_027b, IL_028b, IL_029b, IL_02ab, IL_02bb, IL_02cb, IL_02d8, IL_02e5, IL_02f2, IL_02ff, IL_030c, IL_0319, IL_0326, IL_0333, IL_0340)
        //
        // IL_00d6: br IL_0348
        //
        // IL_00db: ldtoken Class0
        // IL_00e0: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
        // IL_00e5: stloc.2
        // IL_00e6: br IL_0353
        //
        // IL_00eb: ldtoken Class1
        // IL_00f0: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
        // IL_00f5: stloc.2
        // IL_00f6: br IL_0353
        //
        // IL_00fb: ldtoken Class2
        // IL_0100: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
        // IL_0105: stloc.2
        // IL_0106: br IL_0353
        //
        // ...

        return index switch
        {
            // when this method is jitted, will it cause all the types to be loaded? or are those just `ldftn <memberref>` instructions that won't cause the type to load?
            // how do I test that?
            0 => typeof(Class0),
            1 => typeof(Class1),
            2 => typeof(Class2),
            3 => typeof(Class3),
            4 => typeof(Class4),
            5 => typeof(Class5),
            6 => typeof(Class6),
            7 => typeof(Class7),
            8 => typeof(Class8),
            9 => typeof(Class9),
            10 => typeof(Class10),
            11 => typeof(Class11),
            12 => typeof(Class12),
            13 => typeof(Class13),
            14 => typeof(Class14),
            15 => typeof(Class15),
            16 => typeof(Class16),
            17 => typeof(Class17),
            18 => typeof(Class18),
            19 => typeof(Class19),
            20 => typeof(Class20),
            21 => typeof(Class21),
            22 => typeof(Class22),
            23 => typeof(Class23),
            24 => typeof(Class24),
            25 => typeof(Class25),
            26 => typeof(Class26),
            27 => typeof(Class27),
            28 => typeof(Class28),
            29 => typeof(Class29),
            30 => typeof(Class30),
            31 => typeof(Class31),
            32 => typeof(Class32),
            33 => typeof(Class33),
            34 => typeof(Class34),
            35 => typeof(Class35),
            36 => typeof(Class36),
            37 => typeof(Class37),
            38 => typeof(Class38),
            39 => typeof(Class39),
            // 40 => typeof(ExperimentMethodJumptable_SecondAssembly.OtherJavaClass1), // --- second assembly module initializer ran!
            40 => Get_ExperimentMethodJumptable_SecondAssembly_OtherClass1(), // --- second assembly module initializer didn't run!
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // -- this is necessary for R2R to skip loading the other assembly unless the type is actually requested
    private static Type Get_ExperimentMethodJumptable_SecondAssembly_OtherClass1()
        => typeof(ExperimentMethodJumptable_SecondAssembly.OtherClass1);
}

public interface IJavaInteropFunctionPointerResolver
{
    static abstract IntPtr GetFunctionPointer(int methodKey);
}

public partial class Class0 : IJavaInteropFunctionPointerResolver
{
    static Class0() => Console.WriteLine("Class0 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 0;
}

public partial class Class1 : IJavaInteropFunctionPointerResolver
{
    static Class1() => Console.WriteLine("Class1 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 1;
}

public partial class Class2 : IJavaInteropFunctionPointerResolver
{
    static Class2() => Console.WriteLine("Class2 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 2;
    
    // public static IntPtr GetFunctionPointer(int methodKey)
    //     => methodKey switch
    //     {
    //         0 => (IntPtr)(delegate* unmanageD<...)&A,
    //     }
}

public partial class Class3 : IJavaInteropFunctionPointerResolver
{
    static Class3() => Console.WriteLine("Class3 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 3;
}

public partial class Class4 : IJavaInteropFunctionPointerResolver
{
    static Class4() => Console.WriteLine("Class4 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 4;
}

public partial class Class5 : IJavaInteropFunctionPointerResolver
{
    static Class5() => Console.WriteLine("Class5 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 5;
}

public partial class Class6 : IJavaInteropFunctionPointerResolver
{
    static Class6() => Console.WriteLine("Class6 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 6;
}

public partial class Class7 : IJavaInteropFunctionPointerResolver
{
    static Class7() => Console.WriteLine("Class7 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 7;
}

public partial class Class8 : IJavaInteropFunctionPointerResolver
{
    static Class8() => Console.WriteLine("Class8 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 8;
}

public partial class Class9 : IJavaInteropFunctionPointerResolver
{
    static Class9() => Console.WriteLine("Class9 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 9;
}


public partial class Class10 : IJavaInteropFunctionPointerResolver
{
    static Class10() => Console.WriteLine("Class10 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 0;
}

public partial class Class11 : IJavaInteropFunctionPointerResolver
{
    static Class11() => Console.WriteLine("Class11 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 1;
}

public partial class Class12 : IJavaInteropFunctionPointerResolver
{
    static Class12() => Console.WriteLine("Class12 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 2;
}

public partial class Class13 : IJavaInteropFunctionPointerResolver
{
    static Class13() => Console.WriteLine("Class13 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 3;
}

public partial class Class14 : IJavaInteropFunctionPointerResolver
{
    static Class14() => Console.WriteLine("Class14 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 4;
}

public partial class Class15 : IJavaInteropFunctionPointerResolver
{
    static Class15() => Console.WriteLine("Class15 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 5;
}

public partial class Class16 : IJavaInteropFunctionPointerResolver
{
    static Class16() => Console.WriteLine("Class16 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 6;
}

public partial class Class17 : IJavaInteropFunctionPointerResolver
{
    static Class17() => Console.WriteLine("Class17 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 7;
}

public partial class Class18 : IJavaInteropFunctionPointerResolver
{
    static Class18() => Console.WriteLine("Class18 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 8;
}

public partial class Class19 : IJavaInteropFunctionPointerResolver
{
    static Class19() => Console.WriteLine("Class19 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 9;
}

public partial class Class20 : IJavaInteropFunctionPointerResolver
{
    static Class20() => Console.WriteLine("Class20 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 0;
}

public partial class Class21 : IJavaInteropFunctionPointerResolver
{
    static Class21() => Console.WriteLine("Class21 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 1;
}

public partial class Class22 : IJavaInteropFunctionPointerResolver
{
    static Class22() => Console.WriteLine("Class22 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 2;
}

public partial class Class23 : IJavaInteropFunctionPointerResolver
{
    static Class23() => Console.WriteLine("Class23 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 3;
}

public partial class Class24 : IJavaInteropFunctionPointerResolver
{
    static Class24() => Console.WriteLine("Class24 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 4;
}

public partial class Class25 : IJavaInteropFunctionPointerResolver
{
    static Class25() => Console.WriteLine("Class25 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 5;
}

public partial class Class26 : IJavaInteropFunctionPointerResolver
{
    static Class26() => Console.WriteLine("Class26 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 6;
}

public partial class Class27 : IJavaInteropFunctionPointerResolver
{
    static Class27() => Console.WriteLine("Class27 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 7;
}

public partial class Class28 : IJavaInteropFunctionPointerResolver
{
    static Class28() => Console.WriteLine("Class28 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 8;
}

public partial class Class29 : IJavaInteropFunctionPointerResolver
{
    static Class29() => Console.WriteLine("Class29 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 9;
}

public partial class Class30 : IJavaInteropFunctionPointerResolver
{
    static Class30() => Console.WriteLine("Class30 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 0;
}

public partial class Class31 : IJavaInteropFunctionPointerResolver
{
    static Class31() => Console.WriteLine("Class31 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 1;
}

public partial class Class32 : IJavaInteropFunctionPointerResolver
{
    static Class32() => Console.WriteLine("Class32 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 2;
}

public partial class Class33 : IJavaInteropFunctionPointerResolver
{
    static Class33() => Console.WriteLine("Class33 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 3;
}

public partial class Class34 : IJavaInteropFunctionPointerResolver
{
    static Class34() => Console.WriteLine("Class34 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 4;
}

public partial class Class35 : IJavaInteropFunctionPointerResolver
{
    static Class35() => Console.WriteLine("Class35 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 5;
}

public partial class Class36 : IJavaInteropFunctionPointerResolver
{
    static Class36() => Console.WriteLine("Class36 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 6;
}

public partial class Class37 : IJavaInteropFunctionPointerResolver
{
    static Class37() => Console.WriteLine("Class37 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 7;
}

public partial class Class38 : IJavaInteropFunctionPointerResolver
{
    static Class38() => Console.WriteLine("Class38 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 8;
}

public partial class Class39 : IJavaInteropFunctionPointerResolver
{
    static Class39() => Console.WriteLine("Class39 static ctor");
    public static IntPtr GetFunctionPointer(int methodKey) => methodKey * 9;
}
