﻿using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable CS8321 // The local function '...' is declared but never used

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
// TestJavaInteropFunctionPointerMapPrototype();
TestPrecompiledFrozenDictionary();




// ----

void TestHandwrittenMethodMapping()
{
    // 1. method map
    var map = new MethodMapping(new byte[0]);
    // var ptr = map.Invoke_CreateInstance("JavaClass33", jthis, transferOptions);
    
    // we want to call ... JavaClass333.MyMethod(42); but we know just the Java class name so we have to do...
    var ptr = map.Invoke_MyMethod("JavaClass33", 42); 
    Console.WriteLine(ptr); // 3 * 42 = 126
}

void TestHandwrittenTypeMapping()
{
    // 1. type map
    var map = new TypeMapping(new byte[0]);
    
    // we want to call ... JavaClass333.MyMethod(42); but we know just the Java class name so we have to do...
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

unsafe void TestPrecompiledFrozenDictionary()
{
    // Sorted values
    byte[][] values = ["JavaClass0"u8.ToArray(), "JavaClass1"u8.ToArray(), "JavaClass2"u8.ToArray(), "JavaClass3"u8.ToArray(), "JavaClass4"u8.ToArray(), "JavaClass5"u8.ToArray(), "JavaClass6"u8.ToArray(), "JavaClass7"u8.ToArray(), "JavaClass8"u8.ToArray(), "JavaClass9"u8.ToArray(), "JavaClass10"u8.ToArray(), "JavaClass11"u8.ToArray(), "JavaClass12"u8.ToArray(), "JavaClass13"u8.ToArray(), "JavaClass14"u8.ToArray(), "JavaClass15"u8.ToArray(), "JavaClass16"u8.ToArray(), "JavaClass17"u8.ToArray(), "JavaClass18"u8.ToArray(), "JavaClass19"u8.ToArray(), "JavaClass20"u8.ToArray(), "JavaClass21"u8.ToArray(), "JavaClass22"u8.ToArray(), "JavaClass23"u8.ToArray(), "JavaClass24"u8.ToArray(), "JavaClass25"u8.ToArray(), "JavaClass26"u8.ToArray(), "JavaClass27"u8.ToArray(), "JavaClass28"u8.ToArray(), "JavaClass29"u8.ToArray(), "JavaClass30"u8.ToArray(), "JavaClass31"u8.ToArray(), "JavaClass32"u8.ToArray(), "JavaClass33"u8.ToArray(), "JavaClass34"u8.ToArray(), "JavaClass35"u8.ToArray(), "JavaClass36"u8.ToArray(), "JavaClass37"u8.ToArray(), "JavaClass38"u8.ToArray(), "JavaClass39"u8.ToArray(), "OtherJavaClass1"u8.ToArray()];

    // the precompiled info will tell us in which order the values are stored
    // - they are just sorted so we can use binary search at the moment
    // - but the order might be different if we actually encode the frozen dictionary there
    var precompiledInfo = PrecompiledFrozenDictionary.Compile(values);
    var base64 = Convert.ToBase64String(precompiledInfo.RawBytes);

    Console.WriteLine($"Precompiled: {base64}");

    // ----

    // byte[] rawBytes = Convert.FromBase64String(base64);
    
    // I got this from the output of the previous run:
    byte[] rawBytes = Convert.FromBase64String("KQAAAPVViX7ajaN9BvPKq3AaBZZBQy++/plRJ7UeKDG40/5P0ZdJ3Fa4liNBTO/kgEGkjHqOnj5I+CpbckO1TsrQM2ZJ5TkJ87oDntUQ3qShTSLhyJglz59XJNERjMpO68VzOfedSPpA3lI5pMYLU+x4/Wi3Y3NcFKRUN8kfSnXwxDXH4SMEhIFhZnlECiKuitQXhZ+o9dxHyeyUBNGnLh721S5gNhybAAAAAAsAAAAWAAAAIAAAACsAAAA2AAAAQQAAAFAAAABbAAAAZgAAAHEAAAB7AAAAhgAAAJAAAACaAAAApQAAALAAAAC7AAAAxQAAANAAAADbAAAA5gAAAPAAAAD7AAAABQEAABABAAAbAQAAJgEAADEBAAA8AQAARgEAAFEBAABcAQAAZgEAAHEBAAB8AQAAhwEAAJIBAACdAQAAqAEAALIBAAAYAAAAFwAAAAkAAAAmAAAACwAAACcAAAAoAAAAFAAAACUAAAAdAAAABQAAAA8AAAAIAAAABgAAAA4AAAARAAAAIAAAAAcAAAAfAAAAGgAAAAoAAAAEAAAAHAAAAAMAAAAMAAAAFgAAABMAAAANAAAAIgAAAAAAAAAhAAAAFQAAAAIAAAAjAAAAGwAAABIAAAAeAAAAEAAAABkAAAABAAAAJAAAAGEBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAwAAAAMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUAAAAFAAAABgAAAAYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABwAAAAcAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACQAAAAkAAAAAAAAAAAAAAAoAAAAKAAAACwAAAAsAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAAAADAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA0AAAANAAAADgAAAA4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPAAAADwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEQAAABEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEgAAABIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEwAAABMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABQAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAFQAAABUAAAAAAAAAAAAAAAAAAAAAAAAAFgAAABYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAFwAAABcAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGAAAABgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGQAAABkAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGgAAABoAAAAAAAAAAAAAABsAAAAbAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABwAAAAdAAAAAAAAAAAAAAAAAAAAAAAAAB4AAAAeAAAAHwAAAB8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIQAAACEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAiAAAAIgAAAAAAAAAAAAAAIwAAACMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAkAAAAJAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAlAAAAJQAAACYAAAAmAAAAAAAAAAAAAAAAAAAAAAAAACcAAAAnAAAAAAAAAAAAAAAAAAAAAAAAACgAAAAoAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAZvQPKoanuQBKYXZhQ2xhc3MyNEphdmFDbGFzczIzSmF2YUNsYXNzOUphdmFDbGFzczM4SmF2YUNsYXNzMTFKYXZhQ2xhc3MzOU90aGVySmF2YUNsYXNzMUphdmFDbGFzczIwSmF2YUNsYXNzMzdKYXZhQ2xhc3MyOUphdmFDbGFzczVKYXZhQ2xhc3MxNUphdmFDbGFzczhKYXZhQ2xhc3M2SmF2YUNsYXNzMTRKYXZhQ2xhc3MxN0phdmFDbGFzczMySmF2YUNsYXNzN0phdmFDbGFzczMxSmF2YUNsYXNzMjZKYXZhQ2xhc3MxMEphdmFDbGFzczRKYXZhQ2xhc3MyOEphdmFDbGFzczNKYXZhQ2xhc3MxMkphdmFDbGFzczIySmF2YUNsYXNzMTlKYXZhQ2xhc3MxM0phdmFDbGFzczM0SmF2YUNsYXNzMEphdmFDbGFzczMzSmF2YUNsYXNzMjFKYXZhQ2xhc3MySmF2YUNsYXNzMzVKYXZhQ2xhc3MyN0phdmFDbGFzczE4SmF2YUNsYXNzMzBKYXZhQ2xhc3MxNkphdmFDbGFzczI1SmF2YUNsYXNzMUphdmFDbGFzczM2");
    // - this is just to show that the precompiled info is always stable
    // - the precompiled Lookup can be used to generate some additional code where the indexes of those values can baked into something like a jumptable

    // // make sure we don't cheat
    // byte* nativeMemory = (byte*)NativeMemory.AllocZeroed((nuint)rawBytes.Length);
    // NativeMemory.Copy(Unsafe.AsPointer(ref precompiledInfo.RawBytes[0]), nativeMemory, (nuint)rawBytes.Length);
    // var hydrated = new PrecompiledBinarySearchIndexLookup(nativeMemory, rawBytes.Length);

    if (!rawBytes.SequenceEqual(precompiledInfo.RawBytes))
    {
        throw new Exception("Precompiled info is not stable");
    }
    else
    {
        Console.WriteLine("Precompiled info is stable");
    }

    var hydrated = new PrecompiledFrozenDictionary(rawBytes);

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
    var ptr = map.MyMethod("JavaClass3"u8, myArgument: 2);
    Console.WriteLine($"JavaClass3.MyMethod(2) = {ptr} (expecting 6)");
}

unsafe void TestJavaInteropFunctionPointerMapPrototype()
{
    var map = new JavaInteropFunctionPointerMap();
    var fnptr = map.MyMethod("JavaClass3"u8);
    var ptr = fnptr(2);
    Console.WriteLine($"JavaClass3.MyMethod(2) = {ptr} (expecting 6)");
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
        // IL_00db: ldmethodKey.2monodroid_typemap_java_to_managed
        // IL_00dc: call native int JavaClass0::MyMethod(int32)
        // IL_00e1: stloc.2
        // IL_00e2: br IL_02ab
        //
        // IL_00e7: ldmethodKey.2
        // IL_00e8: call native int JavaClass1::MyMethod(int32)
        // IL_00ed: stloc.2
        // IL_00ee: br IL_02ab
        //
        // IL_00f3: ldmethodKey.2
        // IL_00f4: call native int JavaClass2::MyMethod(int32)
        // IL_00f9: stloc.2
        // IL_00fa: br IL_02ab
        //
        // ...

        return index switch
        {
            // when this method is jitted, will it cause all the types to be loaded? or are those just `ldftn <memberref>` instructions that won't cause the type to load?
            // how do I test that?
            0 => Class0.MyMethod(vtableSlot),
            1 => Class1.MyMethod(vtableSlot),
            2 => Class2.MyMethod(vtableSlot),
            3 => Class3.MyMethod(vtableSlot),
            4 => Class4.MyMethod(vtableSlot),
            5 => Class5.MyMethod(vtableSlot),
            6 => Class6.MyMethod(vtableSlot),
            7 => Class7.MyMethod(vtableSlot),
            8 => Class8.MyMethod(vtableSlot),
            9 => Class9.MyMethod(vtableSlot),
            10 => Class10.MyMethod(vtableSlot),
            11 => Class11.MyMethod(vtableSlot),
            12 => Class12.MyMethod(vtableSlot),
            13 => Class13.MyMethod(vtableSlot),
            14 => Class14.MyMethod(vtableSlot),
            15 => Class15.MyMethod(vtableSlot),
            16 => Class16.MyMethod(vtableSlot),
            17 => Class17.MyMethod(vtableSlot),
            18 => Class18.MyMethod(vtableSlot),
            19 => Class19.MyMethod(vtableSlot),
            20 => Class20.MyMethod(vtableSlot),
            21 => Class21.MyMethod(vtableSlot),
            22 => Class22.MyMethod(vtableSlot),
            23 => Class23.MyMethod(vtableSlot),
            24 => Class24.MyMethod(vtableSlot),
            25 => Class25.MyMethod(vtableSlot),
            26 => Class26.MyMethod(vtableSlot),
            27 => Class27.MyMethod(vtableSlot),
            28 => Class28.MyMethod(vtableSlot),
            29 => Class29.MyMethod(vtableSlot),
            30 => Class30.MyMethod(vtableSlot),
            31 => Class31.MyMethod(vtableSlot),
            32 => Class32.MyMethod(vtableSlot),
            33 => Class33.MyMethod(vtableSlot),
            34 => Class34.MyMethod(vtableSlot),
            35 => Class35.MyMethod(vtableSlot),
            36 => Class36.MyMethod(vtableSlot),
            37 => Class37.MyMethod(vtableSlot),
            38 => Class38.MyMethod(vtableSlot),
            39 => Class39.MyMethod(vtableSlot),
            // 40 => ExperimentMethodJumptable_SecondAssembly.OtherClass1.MyMethod(myArgument), // --- second assembly module initializer ran!
            40 => Get_ExperimentMethodJumptable_SecondAssembly_OtherClass1_MyMethod(vtableSlot), // --- second assembly module initializer didn't run! (with R2R it does, is it a problem though?)
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
    private static IntPtr Get_ExperimentMethodJumptable_SecondAssembly_OtherClass1_MyMethod(int myArgument)
        => ExperimentMethodJumptable_SecondAssembly.OtherClass1.MyMethod(myArgument);
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
    static abstract IntPtr MyMethod(int myArgument);
}

public partial class Class0 : IJavaInteropFunctionPointerResolver
{
    static Class0() => Console.WriteLine("Class0 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 0;
}

public partial class Class1 : IJavaInteropFunctionPointerResolver
{
    static Class1() => Console.WriteLine("Class1 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 1;
}

public partial class Class2 : IJavaInteropFunctionPointerResolver
{
    static Class2() => Console.WriteLine("Class2 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 2;
    
    // public static IntPtr MyMethod(int myArgument)
    //     => myArgument switch
    //     {
    //         0 => (IntPtr)(delegate* unmanageD<...)&A,
    //     }
}

public partial class Class3 : IJavaInteropFunctionPointerResolver
{
    static Class3() => Console.WriteLine("Class3 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 3;
}

public partial class Class4 : IJavaInteropFunctionPointerResolver
{
    static Class4() => Console.WriteLine("Class4 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 4;
}

public partial class Class5 : IJavaInteropFunctionPointerResolver
{
    static Class5() => Console.WriteLine("Class5 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 5;
}

public partial class Class6 : IJavaInteropFunctionPointerResolver
{
    static Class6() => Console.WriteLine("Class6 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 6;
}

public partial class Class7 : IJavaInteropFunctionPointerResolver
{
    static Class7() => Console.WriteLine("Class7 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 7;
}

public partial class Class8 : IJavaInteropFunctionPointerResolver
{
    static Class8() => Console.WriteLine("Class8 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 8;
}

public partial class Class9 : IJavaInteropFunctionPointerResolver
{
    static Class9() => Console.WriteLine("Class9 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 9;
}


public partial class Class10 : IJavaInteropFunctionPointerResolver
{
    static Class10() => Console.WriteLine("Class10 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 0;
}

public partial class Class11 : IJavaInteropFunctionPointerResolver
{
    static Class11() => Console.WriteLine("Class11 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 1;
}

public partial class Class12 : IJavaInteropFunctionPointerResolver
{
    static Class12() => Console.WriteLine("Class12 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 2;
}

public partial class Class13 : IJavaInteropFunctionPointerResolver
{
    static Class13() => Console.WriteLine("Class13 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 3;
}

public partial class Class14 : IJavaInteropFunctionPointerResolver
{
    static Class14() => Console.WriteLine("Class14 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 4;
}

public partial class Class15 : IJavaInteropFunctionPointerResolver
{
    static Class15() => Console.WriteLine("Class15 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 5;
}

public partial class Class16 : IJavaInteropFunctionPointerResolver
{
    static Class16() => Console.WriteLine("Class16 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 6;
}

public partial class Class17 : IJavaInteropFunctionPointerResolver
{
    static Class17() => Console.WriteLine("Class17 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 7;
}

public partial class Class18 : IJavaInteropFunctionPointerResolver
{
    static Class18() => Console.WriteLine("Class18 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 8;
}

public partial class Class19 : IJavaInteropFunctionPointerResolver
{
    static Class19() => Console.WriteLine("Class19 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 9;
}

public partial class Class20 : IJavaInteropFunctionPointerResolver
{
    static Class20() => Console.WriteLine("Class20 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 0;
}

public partial class Class21 : IJavaInteropFunctionPointerResolver
{
    static Class21() => Console.WriteLine("Class21 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 1;
}

public partial class Class22 : IJavaInteropFunctionPointerResolver
{
    static Class22() => Console.WriteLine("Class22 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 2;
}

public partial class Class23 : IJavaInteropFunctionPointerResolver
{
    static Class23() => Console.WriteLine("Class23 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 3;
}

public partial class Class24 : IJavaInteropFunctionPointerResolver
{
    static Class24() => Console.WriteLine("Class24 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 4;
}

public partial class Class25 : IJavaInteropFunctionPointerResolver
{
    static Class25() => Console.WriteLine("Class25 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 5;
}

public partial class Class26 : IJavaInteropFunctionPointerResolver
{
    static Class26() => Console.WriteLine("Class26 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 6;
}

public partial class Class27 : IJavaInteropFunctionPointerResolver
{
    static Class27() => Console.WriteLine("Class27 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 7;
}

public partial class Class28 : IJavaInteropFunctionPointerResolver
{
    static Class28() => Console.WriteLine("Class28 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 8;
}

public partial class Class29 : IJavaInteropFunctionPointerResolver
{
    static Class29() => Console.WriteLine("Class29 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 9;
}

public partial class Class30 : IJavaInteropFunctionPointerResolver
{
    static Class30() => Console.WriteLine("Class30 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 0;
}

public partial class Class31 : IJavaInteropFunctionPointerResolver
{
    static Class31() => Console.WriteLine("Class31 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 1;
}

public partial class Class32 : IJavaInteropFunctionPointerResolver
{
    static Class32() => Console.WriteLine("Class32 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 2;
}

public partial class Class33 : IJavaInteropFunctionPointerResolver
{
    static Class33() => Console.WriteLine("Class33 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 3;
}

public partial class Class34 : IJavaInteropFunctionPointerResolver
{
    static Class34() => Console.WriteLine("Class34 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 4;
}

public partial class Class35 : IJavaInteropFunctionPointerResolver
{
    static Class35() => Console.WriteLine("Class35 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 5;
}

public partial class Class36 : IJavaInteropFunctionPointerResolver
{
    static Class36() => Console.WriteLine("Class36 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 6;
}

public partial class Class37 : IJavaInteropFunctionPointerResolver
{
    static Class37() => Console.WriteLine("Class37 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 7;
}

public partial class Class38 : IJavaInteropFunctionPointerResolver
{
    static Class38() => Console.WriteLine("Class38 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 8;
}

public partial class Class39 : IJavaInteropFunctionPointerResolver
{
    static Class39() => Console.WriteLine("Class39 static ctor");
    public static IntPtr MyMethod(int myArgument) => myArgument * 9;
}
