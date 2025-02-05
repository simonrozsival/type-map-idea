using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// Idea: Using method bodies to store typerefs and memberrefs for efficient loading of types and method delegates.
//       We just build a large method(*) which can return any of the types or method delegates and create a jumptable
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


{
    // 1. method map
    var map = new MethodMapping(new byte[0]);
    // var ptr = map.Invoke_CreateInstance("JavaClass33", jthis, transferOptions);
    
    // we want to call ... JavaClass333.MyMethod(42); but we know just the Java class name so we have to do...
    var ptr = map.Invoke_MyMethod("JavaClass33", 42); 
    Console.WriteLine(ptr); // 3 * 42 = 126
}

unsafe
{
    // Sorted values
    byte[][] values = ["JavaClass0"u8.ToArray(), "JavaClass1"u8.ToArray(), "JavaClass2"u8.ToArray(), "JavaClass3"u8.ToArray(), "JavaClass4"u8.ToArray(), "JavaClass5"u8.ToArray(), "JavaClass6"u8.ToArray(), "JavaClass7"u8.ToArray(), "JavaClass8"u8.ToArray(), "JavaClass9"u8.ToArray(), "JavaClass10"u8.ToArray(), "JavaClass11"u8.ToArray(), "JavaClass12"u8.ToArray(), "JavaClass13"u8.ToArray(), "JavaClass14"u8.ToArray(), "JavaClass15"u8.ToArray(), "JavaClass16"u8.ToArray(), "JavaClass17"u8.ToArray(), "JavaClass18"u8.ToArray(), "JavaClass19"u8.ToArray(), "JavaClass20"u8.ToArray(), "JavaClass21"u8.ToArray(), "JavaClass22"u8.ToArray(), "JavaClass23"u8.ToArray(), "JavaClass24"u8.ToArray(), "JavaClass25"u8.ToArray(), "JavaClass26"u8.ToArray(), "JavaClass27"u8.ToArray(), "JavaClass28"u8.ToArray(), "JavaClass29"u8.ToArray(), "JavaClass30"u8.ToArray(), "JavaClass31"u8.ToArray(), "JavaClass32"u8.ToArray(), "JavaClass33"u8.ToArray(), "JavaClass34"u8.ToArray(), "JavaClass35"u8.ToArray(), "JavaClass36"u8.ToArray(), "JavaClass37"u8.ToArray(), "JavaClass38"u8.ToArray(), "JavaClass39"u8.ToArray(), "OtherJavaClass1"u8.ToArray()];

    var precompiledInfo = PrecompiledLookup.Compile(values);
    var base64 = Convert.ToBase64String(precompiledInfo.RawBytes);

    Console.WriteLine($"Precompiled: {base64}");

    // ----

    // byte[] rawBytes = Convert.FromBase64String(base64);
    byte[] rawBytes = Convert.FromBase64String("KQAAAOEjBISK1BeFgEGkjEfJ7JRwGgWWYDYcm/O6A57VEN6kBvPKq0QKIq5BQy++8MQ1x8iYJc+fVyTR0ZdJ3J+o9dyhTSLhQUzv5PedSPpJ5TkJVriWI/6ZUScE0acuHvbVLrUeKDEUpFQ3QN5SOevFczl6jp4+ckO1ThGMyk640/5PpMYLU0j4Klu3Y3NcytAzZux4/WjJH0p1gWFmedqNo331VYl+AAAAAAoAAAAVAAAAIAAAACsAAAA2AAAAQQAAAEsAAABWAAAAYAAAAGsAAAB2AAAAgQAAAIwAAACWAAAAoQAAAKwAAAC3AAAAwQAAAMwAAADXAAAA4gAAAO0AAAD4AAAAAgEAABEBAAAbAQAAJgEAADABAAA6AQAARQEAAFABAABbAQAAZgEAAHABAAB7AQAAhgEAAJEBAACcAQAApwEAALIBAABKYXZhQ2xhc3MySmF2YUNsYXNzMThKYXZhQ2xhc3MxNUphdmFDbGFzczE2SmF2YUNsYXNzMzhKYXZhQ2xhc3MzNkphdmFDbGFzczdKYXZhQ2xhc3MzMUphdmFDbGFzczlKYXZhQ2xhc3MyN0phdmFDbGFzczExSmF2YUNsYXNzMjFKYXZhQ2xhc3MxMEphdmFDbGFzczRKYXZhQ2xhc3MzN0phdmFDbGFzczMwSmF2YUNsYXNzMjZKYXZhQ2xhc3M1SmF2YUNsYXNzMTJKYXZhQ2xhc3MzMkphdmFDbGFzczI5SmF2YUNsYXNzMzlKYXZhQ2xhc3MyNUphdmFDbGFzczFPdGhlckphdmFDbGFzczFKYXZhQ2xhc3MwSmF2YUNsYXNzMjJKYXZhQ2xhc3MzSmF2YUNsYXNzOEphdmFDbGFzczE0SmF2YUNsYXNzMjhKYXZhQ2xhc3MyMEphdmFDbGFzczE5SmF2YUNsYXNzNkphdmFDbGFzczM0SmF2YUNsYXNzMTdKYXZhQ2xhc3MxM0phdmFDbGFzczMzSmF2YUNsYXNzMzVKYXZhQ2xhc3MyM0phdmFDbGFzczI0");

    // make sure we don't cheat
    byte* nativeMemory = (byte*)NativeMemory.AllocZeroed((nuint)rawBytes.Length);
    NativeMemory.Copy(Unsafe.AsPointer(ref precompiledInfo.RawBytes[0]), nativeMemory, (nuint)rawBytes.Length);

    var hydrated = new PrecompiledLookup(nativeMemory, rawBytes.Length);

    int correct = 0;
    int incorrect = 0;

    foreach (var value in values)
    {
        var expected = precompiledInfo.ValueIndexes[value];
        var actual = hydrated.IndexOf(value);
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

    Console.WriteLine($"Correct: {correct}, Incorrect: {incorrect}");
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

    public IntPtr Invoke_MyMethod(string javaClassName, int vtableSlot)
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
        // IL_00db: ldarg.2monodroid_typemap_java_to_managed
        // IL_00dc: call native int JavaClass0::MyMethod(int32)
        // IL_00e1: stloc.2
        // IL_00e2: br IL_02ab
        //
        // IL_00e7: ldarg.2
        // IL_00e8: call native int JavaClass1::MyMethod(int32)
        // IL_00ed: stloc.2
        // IL_00ee: br IL_02ab
        //
        // IL_00f3: ldarg.2
        // IL_00f4: call native int JavaClass2::MyMethod(int32)
        // IL_00f9: stloc.2
        // IL_00fa: br IL_02ab
        //
        // ...

        return index switch
        {
            // when this method is jitted, will it cause all the types to be loaded? or are those just `ldftn <memberref>` instructions that won't cause the type to load?
            // how do I test that?
            0 => JavaClass0.MyMethod(vtableSlot),
            1 => JavaClass1.MyMethod(vtableSlot),
            2 => JavaClass2.MyMethod(vtableSlot),
            3 => JavaClass3.MyMethod(vtableSlot),
            4 => JavaClass4.MyMethod(vtableSlot),
            5 => JavaClass5.MyMethod(vtableSlot),
            6 => JavaClass6.MyMethod(vtableSlot),
            7 => JavaClass7.MyMethod(vtableSlot),
            8 => JavaClass8.MyMethod(vtableSlot),
            9 => JavaClass9.MyMethod(vtableSlot),
            10 => JavaClass10.MyMethod(vtableSlot),
            11 => JavaClass11.MyMethod(vtableSlot),
            12 => JavaClass12.MyMethod(vtableSlot),
            13 => JavaClass13.MyMethod(vtableSlot),
            14 => JavaClass14.MyMethod(vtableSlot),
            15 => JavaClass15.MyMethod(vtableSlot),
            16 => JavaClass16.MyMethod(vtableSlot),
            17 => JavaClass17.MyMethod(vtableSlot),
            18 => JavaClass18.MyMethod(vtableSlot),
            19 => JavaClass19.MyMethod(vtableSlot),
            20 => JavaClass20.MyMethod(vtableSlot),
            21 => JavaClass21.MyMethod(vtableSlot),
            22 => JavaClass22.MyMethod(vtableSlot),
            23 => JavaClass23.MyMethod(vtableSlot),
            24 => JavaClass24.MyMethod(vtableSlot),
            25 => JavaClass25.MyMethod(vtableSlot),
            26 => JavaClass26.MyMethod(vtableSlot),
            27 => JavaClass27.MyMethod(vtableSlot),
            28 => JavaClass28.MyMethod(vtableSlot),
            29 => JavaClass29.MyMethod(vtableSlot),
            30 => JavaClass30.MyMethod(vtableSlot),
            31 => JavaClass31.MyMethod(vtableSlot),
            32 => JavaClass32.MyMethod(vtableSlot),
            33 => JavaClass33.MyMethod(vtableSlot),
            34 => JavaClass34.MyMethod(vtableSlot),
            35 => JavaClass35.MyMethod(vtableSlot),
            36 => JavaClass36.MyMethod(vtableSlot),
            37 => JavaClass37.MyMethod(vtableSlot),
            38 => JavaClass38.MyMethod(vtableSlot),
            39 => JavaClass39.MyMethod(vtableSlot),
            // 40 => ExperimentMethodJumptable_SecondAssembly.OtherJavaClass1.MyMethod(arg), // --- second assembly module initializer ran!
            40 => Get_ExperimentMethodJumptable_SecondAssembly_OtherJavaClass1_MyMethod(vtableSlot), // --- second assembly module initializer didn't run! (with R2R it does, is it a problem though?)
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        // when there are big gaps between the case numbers, it is not a simple jumptable:
        //
        // return index switch
        // {
        //     // when this method is jitted, will it cause all the types to be loaded? or are those just `ldftn <memberref>` instructions that won't cause the type to load?
        //     // how do I test that?
        //     0000 => JavaClass0.MyMethod,
        //     // 1000 => JavaClass1.MyMethod,
        //     // 2000 => JavaClass2.MyMethod,
        //     3000 => JavaClass3.MyMethod,
        //     4000 => JavaClass4.MyMethod,
        //     // 5000 => JavaClass5.MyMethod,
        //     // 6000 => JavaClass6.MyMethod,
        //     7000 => JavaClass7.MyMethod,
        //     8000 => JavaClass8.MyMethod,
        //     // 9000 => JavaClass9.MyMethod,
        //     10000 => JavaClass10.MyMethod,
        //     11000 => JavaClass11.MyMethod,
        //     12000 => JavaClass12.MyMethod,
        //     // 13000 => JavaClass13.MyMethod,
        //     // 14000 => JavaClass14.MyMethod,
        //     15000 => JavaClass15.MyMethod,
        //     16000 => JavaClass16.MyMethod,
        //     17000 => JavaClass17.MyMethod,
        //     // 18000 => JavaClass18.MyMethod,
        //     // 19000 => JavaClass19.MyMethod,
        //     // 20000 => JavaClass20.MyMethod,
        //     // 21000 => JavaClass21.MyMethod,
        //     // 22000 => JavaClass22.MyMethod,
        //     // 23000 => JavaClass23.MyMethod,
        //     // 24000 => JavaClass24.MyMethod,
        //     // 25000 => JavaClass25.MyMethod,
        //     // 26000 => JavaClass26.MyMethod,
        //     27000 => JavaClass27.MyMethod,
        //     28000 => JavaClass28.MyMethod,
        //     // 29000 => JavaClass29.MyMethod,
        //     // 30000 => JavaClass30.MyMethod,
        //     // 31000 => JavaClass31.MyMethod,
        //     // 32000 => JavaClass32.MyMethod,
        //     33000 => JavaClass33.MyMethod,
        //     // 34000 => JavaClass34.MyMethod,
        //     // 35000 => JavaClass35.MyMethod,
        //     // 36000 => JavaClass36.MyMethod,
        //     // 37000 => JavaClass37.MyMethod,
        //     // 38000 => JavaClass38.MyMethod,
        //     39000 => JavaClass39.MyMethod,
        //     _ => throw new ArgumentOutOfRangeException(nameof(index))
        // };
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // -- this is necessary for R2R to skip loading the other assembly unless the type is actually requested
    private static IntPtr Get_ExperimentMethodJumptable_SecondAssembly_OtherJavaClass1_MyMethod(int arg)
        => ExperimentMethodJumptable_SecondAssembly.OtherJavaClass1.MyMethod(arg);
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
        // IL_00db: ldtoken JavaClass0
        // IL_00e0: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
        // IL_00e5: stloc.2
        // IL_00e6: br IL_0353
        //
        // IL_00eb: ldtoken JavaClass1
        // IL_00f0: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
        // IL_00f5: stloc.2
        // IL_00f6: br IL_0353
        //
        // IL_00fb: ldtoken JavaClass2
        // IL_0100: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
        // IL_0105: stloc.2
        // IL_0106: br IL_0353
        //
        // ...

        return index switch
        {
            // when this method is jitted, will it cause all the types to be loaded? or are those just `ldftn <memberref>` instructions that won't cause the type to load?
            // how do I test that?
            0 => typeof(JavaClass0),
            1 => typeof(JavaClass1),
            2 => typeof(JavaClass2),
            3 => typeof(JavaClass3),
            4 => typeof(JavaClass4),
            5 => typeof(JavaClass5),
            6 => typeof(JavaClass6),
            7 => typeof(JavaClass7),
            8 => typeof(JavaClass8),
            9 => typeof(JavaClass9),
            10 => typeof(JavaClass10),
            11 => typeof(JavaClass11),
            12 => typeof(JavaClass12),
            13 => typeof(JavaClass13),
            14 => typeof(JavaClass14),
            15 => typeof(JavaClass15),
            16 => typeof(JavaClass16),
            17 => typeof(JavaClass17),
            18 => typeof(JavaClass18),
            19 => typeof(JavaClass19),
            20 => typeof(JavaClass20),
            21 => typeof(JavaClass21),
            22 => typeof(JavaClass22),
            23 => typeof(JavaClass23),
            24 => typeof(JavaClass24),
            25 => typeof(JavaClass25),
            26 => typeof(JavaClass26),
            27 => typeof(JavaClass27),
            28 => typeof(JavaClass28),
            29 => typeof(JavaClass29),
            30 => typeof(JavaClass30),
            31 => typeof(JavaClass31),
            32 => typeof(JavaClass32),
            33 => typeof(JavaClass33),
            34 => typeof(JavaClass34),
            35 => typeof(JavaClass35),
            36 => typeof(JavaClass36),
            37 => typeof(JavaClass37),
            38 => typeof(JavaClass38),
            39 => typeof(JavaClass39),
            // 40 => typeof(ExperimentMethodJumptable_SecondAssembly.OtherJavaClass1), // --- second assembly module initializer ran!
            40 => Get_ExperimentMethodJumptable_SecondAssembly_OtherJavaClass1(), // --- second assembly module initializer didn't run!
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // -- this is necessary for R2R to skip loading the other assembly unless the type is actually requested
    private static Type Get_ExperimentMethodJumptable_SecondAssembly_OtherJavaClass1()
        => typeof(ExperimentMethodJumptable_SecondAssembly.OtherJavaClass1);
}

public class JavaClass0
{
    static JavaClass0() => Console.WriteLine("JavaClass0 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 0;
}

public class JavaClass1
{
    static JavaClass1() => Console.WriteLine("JavaClass1 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 1;
}

public class JavaClass2
{
    static JavaClass2() => Console.WriteLine("JavaClass2 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 2;
    
    // public static IntPtr MyMethod(int arg)
    //     => arg switch
    //     {
    //         0 => (IntPtr)(delegate* unmanageD<...)&A,
    //     }
}

public class JavaClass3
{
    static JavaClass3() => Console.WriteLine("JavaClass3 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 3;
}

public class JavaClass4
{
    static JavaClass4() => Console.WriteLine("JavaClass4 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 4;
}

public class JavaClass5
{
    static JavaClass5() => Console.WriteLine("JavaClass5 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 5;
}

public class JavaClass6
{
    static JavaClass6() => Console.WriteLine("JavaClass6 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 6;
}

public class JavaClass7
{
    static JavaClass7() => Console.WriteLine("JavaClass7 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 7;
}

public class JavaClass8
{
    static JavaClass8() => Console.WriteLine("JavaClass8 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 8;
}

public class JavaClass9
{
    static JavaClass9() => Console.WriteLine("JavaClass9 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 9;
}


public class JavaClass10
{
    static JavaClass10() => Console.WriteLine("JavaClass10 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 0;
}

public class JavaClass11
{
    static JavaClass11() => Console.WriteLine("JavaClass11 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 1;
}

public class JavaClass12
{
    static JavaClass12() => Console.WriteLine("JavaClass12 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 2;
}

public class JavaClass13
{
    static JavaClass13() => Console.WriteLine("JavaClass13 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 3;
}

public class JavaClass14
{
    static JavaClass14() => Console.WriteLine("JavaClass14 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 4;
}

public class JavaClass15
{
    static JavaClass15() => Console.WriteLine("JavaClass15 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 5;
}

public class JavaClass16
{
    static JavaClass16() => Console.WriteLine("JavaClass16 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 6;
}

public class JavaClass17
{
    static JavaClass17() => Console.WriteLine("JavaClass17 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 7;
}

public class JavaClass18
{
    static JavaClass18() => Console.WriteLine("JavaClass18 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 8;
}

public class JavaClass19
{
    static JavaClass19() => Console.WriteLine("JavaClass19 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 9;
}

public class JavaClass20
{
    static JavaClass20() => Console.WriteLine("JavaClass20 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 0;
}

public class JavaClass21
{
    static JavaClass21() => Console.WriteLine("JavaClass21 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 1;
}

public class JavaClass22
{
    static JavaClass22() => Console.WriteLine("JavaClass22 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 2;
}

public class JavaClass23
{
    static JavaClass23() => Console.WriteLine("JavaClass23 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 3;
}

public class JavaClass24
{
    static JavaClass24() => Console.WriteLine("JavaClass24 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 4;
}

public class JavaClass25
{
    static JavaClass25() => Console.WriteLine("JavaClass25 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 5;
}

public class JavaClass26
{
    static JavaClass26() => Console.WriteLine("JavaClass26 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 6;
}

public class JavaClass27
{
    static JavaClass27() => Console.WriteLine("JavaClass27 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 7;
}

public class JavaClass28
{
    static JavaClass28() => Console.WriteLine("JavaClass28 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 8;
}

public class JavaClass29
{
    static JavaClass29() => Console.WriteLine("JavaClass29 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 9;
}

public class JavaClass30
{
    static JavaClass30() => Console.WriteLine("JavaClass30 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 0;
}

public class JavaClass31
{
    static JavaClass31() => Console.WriteLine("JavaClass31 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 1;
}

public class JavaClass32
{
    static JavaClass32() => Console.WriteLine("JavaClass32 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 2;
}

public class JavaClass33
{
    static JavaClass33() => Console.WriteLine("JavaClass33 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 3;
}

public class JavaClass34
{
    static JavaClass34() => Console.WriteLine("JavaClass34 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 4;
}

public class JavaClass35
{
    static JavaClass35() => Console.WriteLine("JavaClass35 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 5;
}

public class JavaClass36
{
    static JavaClass36() => Console.WriteLine("JavaClass36 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 6;
}

public class JavaClass37
{
    static JavaClass37() => Console.WriteLine("JavaClass37 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 7;
}

public class JavaClass38
{
    static JavaClass38() => Console.WriteLine("JavaClass38 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 8;
}

public class JavaClass39
{
    static JavaClass39() => Console.WriteLine("JavaClass39 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 9;
}
