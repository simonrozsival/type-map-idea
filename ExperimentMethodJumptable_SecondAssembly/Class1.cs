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

public class OtherClass1
{
    static OtherClass1() => Console.WriteLine("OtherClass1 static ctor");
    public static IntPtr MyMethod(int arg) => arg * 2;
}