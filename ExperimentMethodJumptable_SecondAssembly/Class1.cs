using System.Runtime.CompilerServices;

namespace ExperimentMethodJumptable_SecondAssembly;

static class Initializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Console.WriteLine("SecondAssembly Initialize");
    }
}

public class OtherJavaClass1
{
    static OtherJavaClass1() => Console.WriteLine("OtherJavaClass1 static ctor");
    public static IntPtr GetFunctionPointer(int arg) => arg * 2;
}