using System.Runtime.CompilerServices;

namespace ExperimentMethodJumptable_SecondAssembly;

static class Initializer
{
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize()
    {
        Console.WriteLine("SecondAssembly Initialize");
    }
#pragma warning restore CA2255
}

public partial class OtherClass1
{
    static OtherClass1() => Console.WriteLine("OtherClass1 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 2;
}