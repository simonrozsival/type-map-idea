using System.Collections.Frozen;
using System.Runtime.CompilerServices;

// Idea: Using IL to store typerefs and memberrefs for efficient loading of types and method delegates.
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
    var factory = map.GetMethod("JavaClass33");
    var ptr = factory(42);
    Console.WriteLine(ptr); // 3 * 42 = 126
}

{
    // 2. type map
    var map = new TypeMapping(new byte[0]);
    var type = map.GetType("JavaClass33");
    Console.WriteLine(type);
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

    public Func<int, IntPtr> GetMethod(string javaClassName)
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

        // IL_002c: ldloc.0
        // IL_002d: switch (IL_00d7, IL_00f8, IL_0119, IL_013a, IL_015b, IL_017c, IL_019d, IL_01be, IL_01df, IL_0200, IL_0221, IL_0242, IL_0263, IL_0284, IL_02a5, IL_02c6, IL_02e7, IL_0308, IL_0329, IL_034a, IL_036b, IL_038c, IL_03ad, IL_03ce, IL_03ef, IL_0410, IL_0431, IL_0452, IL_0473, IL_0494, IL_04b5, IL_04d6, IL_04f7, IL_0518, IL_0539, IL_055a, IL_057b, IL_0599, IL_05b7, IL_05d5)
        //
        // IL_00d2: br IL_05f3
        //
        // IL_00d7: ldsfld class [System.Runtime]System.Func`2<int32, native int> MethodMapping/'<>O'::'<0>__GetFunctionPointer'
        // IL_00dc: dup
        // IL_00dd: brtrue.s IL_00f2
        //
        // IL_00df: pop
        // IL_00e0: ldnull
        // IL_00e1: ldftn native int JavaClass0::GetFunctionPointer(int32)
        // IL_00e7: newobj instance void class [System.Runtime]System.Func`2<int32, native int>::.ctor(object, native int)
        // IL_00ec: dup
        // IL_00ed: stsfld class [System.Runtime]System.Func`2<int32, native int> MethodMapping/'<>O'::'<0>__GetFunctionPointer'
        //
        // IL_00f2: stloc.2
        // IL_00f3: br IL_05fe
        //
        // IL_00f8: ldsfld class [System.Runtime]System.Func`2<int32, native int> MethodMapping/'<>O'::'<1>__GetFunctionPointer'
        // IL_00fd: dup
        // IL_00fe: brtrue.s IL_0113
        //
        // IL_0100: pop
        // IL_0101: ldnull
        // IL_0102: ldftn native int JavaClass1::GetFunctionPointer(int32)
        // IL_0108: newobj instance void class [System.Runtime]System.Func`2<int32, native int>::.ctor(object, native int)
        // IL_010d: dup
        // IL_010e: stsfld class [System.Runtime]System.Func`2<int32, native int> MethodMapping/'<>O'::'<1>__GetFunctionPointer'
        //
        // IL_0113: stloc.2
        // IL_0114: br IL_05fe
        //
        // IL_0119: ldsfld class [System.Runtime]System.Func`2<int32, native int> MethodMapping/'<>O'::'<2>__GetFunctionPointer'
        // IL_011e: dup
        // IL_011f: brtrue.s IL_0134
        //
        // IL_0121: pop
        // IL_0122: ldnull
        // IL_0123: ldftn native int JavaClass2::GetFunctionPointer(int32)
        // IL_0129: newobj instance void class [System.Runtime]System.Func`2<int32, native int>::.ctor(object, native int)
        // IL_012e: dup
        // IL_012f: stsfld class [System.Runtime]System.Func`2<int32, native int> MethodMapping/'<>O'::'<2>__GetFunctionPointer'
        //
        // IL_0134: stloc.2
        // IL_0135: br IL_05fe
        //
        // ...

        return index switch
        {
            // when this method is jitted, will it cause all the types to be loaded? or are those just `ldftn <memberref>` instructions that won't cause the type to load?
            // how do I test that?
            0 => JavaClass0.GetFunctionPointer,
            1 => JavaClass1.GetFunctionPointer,
            2 => JavaClass2.GetFunctionPointer,
            3 => JavaClass3.GetFunctionPointer,
            4 => JavaClass4.GetFunctionPointer,
            5 => JavaClass5.GetFunctionPointer,
            6 => JavaClass6.GetFunctionPointer,
            7 => JavaClass7.GetFunctionPointer,
            8 => JavaClass8.GetFunctionPointer,
            9 => JavaClass9.GetFunctionPointer,
            10 => JavaClass10.GetFunctionPointer,
            11 => JavaClass11.GetFunctionPointer,
            12 => JavaClass12.GetFunctionPointer,
            13 => JavaClass13.GetFunctionPointer,
            14 => JavaClass14.GetFunctionPointer,
            15 => JavaClass15.GetFunctionPointer,
            16 => JavaClass16.GetFunctionPointer,
            17 => JavaClass17.GetFunctionPointer,
            18 => JavaClass18.GetFunctionPointer,
            19 => JavaClass19.GetFunctionPointer,
            20 => JavaClass20.GetFunctionPointer,
            21 => JavaClass21.GetFunctionPointer,
            22 => JavaClass22.GetFunctionPointer,
            23 => JavaClass23.GetFunctionPointer,
            24 => JavaClass24.GetFunctionPointer,
            25 => JavaClass25.GetFunctionPointer,
            26 => JavaClass26.GetFunctionPointer,
            27 => JavaClass27.GetFunctionPointer,
            28 => JavaClass28.GetFunctionPointer,
            29 => JavaClass29.GetFunctionPointer,
            30 => JavaClass30.GetFunctionPointer,
            31 => JavaClass31.GetFunctionPointer,
            32 => JavaClass32.GetFunctionPointer,
            33 => JavaClass33.GetFunctionPointer,
            34 => JavaClass34.GetFunctionPointer,
            35 => JavaClass35.GetFunctionPointer,
            36 => JavaClass36.GetFunctionPointer,
            37 => JavaClass37.GetFunctionPointer,
            38 => JavaClass38.GetFunctionPointer,
            39 => JavaClass39.GetFunctionPointer,
            // 40 => ExperimentMethodJumptable_SecondAssembly.OtherJavaClass1.GetFunctionPointer, // --- second assembly module initializer ran!
            40 => Get_ExperimentMethodJumptable_SecondAssembly_OtherJavaClass1_GetFunctionPointer(), // --- second assembly module initializer didn't run! (with R2R it does, is it a problem though?)
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
    private static Func<int, IntPtr> Get_ExperimentMethodJumptable_SecondAssembly_OtherJavaClass1_GetFunctionPointer()
        => ExperimentMethodJumptable_SecondAssembly.OtherJavaClass1.GetFunctionPointer;
}

class TypeMapping
{
    // int[] _hashes;
    // string[] _keys;
    // (int, int)[] _buckets;

    private readonly FrozenDictionary<string, int> _indexMap;

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
    public static IntPtr GetFunctionPointer(int arg) => arg * 0;
}

public class JavaClass1
{
    static JavaClass1() => Console.WriteLine("JavaClass1 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 1;
}

public class JavaClass2
{
    static JavaClass2() => Console.WriteLine("JavaClass2 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 2;
}

public class JavaClass3
{
    static JavaClass3() => Console.WriteLine("JavaClass3 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 3;
}

public class JavaClass4
{
    static JavaClass4() => Console.WriteLine("JavaClass4 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 4;
}

public class JavaClass5
{
    static JavaClass5() => Console.WriteLine("JavaClass5 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 5;
}

public class JavaClass6
{
    static JavaClass6() => Console.WriteLine("JavaClass6 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 6;
}

public class JavaClass7
{
    static JavaClass7() => Console.WriteLine("JavaClass7 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 7;
}

public class JavaClass8
{
    static JavaClass8() => Console.WriteLine("JavaClass8 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 8;
}

public class JavaClass9
{
    static JavaClass9() => Console.WriteLine("JavaClass9 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 9;
}


public class JavaClass10
{
    static JavaClass10() => Console.WriteLine("JavaClass10 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 0;
}

public class JavaClass11
{
    static JavaClass11() => Console.WriteLine("JavaClass11 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 1;
}

public class JavaClass12
{
    static JavaClass12() => Console.WriteLine("JavaClass12 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 2;
}

public class JavaClass13
{
    static JavaClass13() => Console.WriteLine("JavaClass13 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 3;
}

public class JavaClass14
{
    static JavaClass14() => Console.WriteLine("JavaClass14 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 4;
}

public class JavaClass15
{
    static JavaClass15() => Console.WriteLine("JavaClass15 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 5;
}

public class JavaClass16
{
    static JavaClass16() => Console.WriteLine("JavaClass16 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 6;
}

public class JavaClass17
{
    static JavaClass17() => Console.WriteLine("JavaClass17 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 7;
}

public class JavaClass18
{
    static JavaClass18() => Console.WriteLine("JavaClass18 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 8;
}

public class JavaClass19
{
    static JavaClass19() => Console.WriteLine("JavaClass19 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 9;
}

public class JavaClass20
{
    static JavaClass20() => Console.WriteLine("JavaClass20 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 0;
}

public class JavaClass21
{
    static JavaClass21() => Console.WriteLine("JavaClass21 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 1;
}

public class JavaClass22
{
    static JavaClass22() => Console.WriteLine("JavaClass22 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 2;
}

public class JavaClass23
{
    static JavaClass23() => Console.WriteLine("JavaClass23 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 3;
}

public class JavaClass24
{
    static JavaClass24() => Console.WriteLine("JavaClass24 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 4;
}

public class JavaClass25
{
    static JavaClass25() => Console.WriteLine("JavaClass25 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 5;
}

public class JavaClass26
{
    static JavaClass26() => Console.WriteLine("JavaClass26 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 6;
}

public class JavaClass27
{
    static JavaClass27() => Console.WriteLine("JavaClass27 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 7;
}

public class JavaClass28
{
    static JavaClass28() => Console.WriteLine("JavaClass28 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 8;
}

public class JavaClass29
{
    static JavaClass29() => Console.WriteLine("JavaClass29 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 9;
}

public class JavaClass30
{
    static JavaClass30() => Console.WriteLine("JavaClass30 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 0;
}

public class JavaClass31
{
    static JavaClass31() => Console.WriteLine("JavaClass31 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 1;
}

public class JavaClass32
{
    static JavaClass32() => Console.WriteLine("JavaClass32 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 2;
}

public class JavaClass33
{
    static JavaClass33() => Console.WriteLine("JavaClass33 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 3;
}

public class JavaClass34
{
    static JavaClass34() => Console.WriteLine("JavaClass34 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 4;
}

public class JavaClass35
{
    static JavaClass35() => Console.WriteLine("JavaClass35 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 5;
}

public class JavaClass36
{
    static JavaClass36() => Console.WriteLine("JavaClass36 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 6;
}

public class JavaClass37
{
    static JavaClass37() => Console.WriteLine("JavaClass37 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 7;
}

public class JavaClass38
{
    static JavaClass38() => Console.WriteLine("JavaClass38 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 8;
}

public class JavaClass39
{
    static JavaClass39() => Console.WriteLine("JavaClass39 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 9;
}
