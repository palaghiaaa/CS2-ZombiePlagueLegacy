using System.Reflection;
var dll = Assembly.LoadFile("/home/runner/.nuget/packages/swiftlys2.cs2/1.3.2/lib/net10.0/SwiftlyS2.CS2.dll");
foreach (var t in dll.GetTypes())
{
    if (t.Name.Contains("Trace"))
    {
        Console.WriteLine($"Type: {t.FullName}");
        foreach (var m in t.GetMethods())
            Console.WriteLine($"  Method: {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    }
}
