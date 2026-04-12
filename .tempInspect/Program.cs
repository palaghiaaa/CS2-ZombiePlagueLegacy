using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        void TryLoad(string path) { try { Assembly.LoadFrom(path); } catch { } }
        var basePath = "/tmp/nuget_restore";
        foreach (var dll in new[] {
            $"{basePath}/microsoft.extensions.options/10.0.0/lib/net10.0/Microsoft.Extensions.Options.dll",
            $"{basePath}/microsoft.extensions.logging.abstractions/10.0.0/lib/net10.0/Microsoft.Extensions.Logging.Abstractions.dll",
            $"{basePath}/microsoft.extensions.hosting.abstractions/10.0.0/lib/net10.0/Microsoft.Extensions.Hosting.Abstractions.dll",
            $"{basePath}/microsoft.extensions.dependencyinjection.abstractions/10.0.0/lib/net10.0/Microsoft.Extensions.DependencyInjection.Abstractions.dll",
            $"{basePath}/microsoft.extensions.configuration.abstractions/10.0.0/lib/net10.0/Microsoft.Extensions.Configuration.Abstractions.dll",
            $"{basePath}/microsoft.extensions.primitives/10.0.0/lib/net10.0/Microsoft.Extensions.Primitives.dll",
        }) TryLoad(dll);
        
        var asm = Assembly.LoadFrom("/tmp/nuget_restore/swiftlys2.cs2/1.1.5-beta.27/lib/net10.0/SwiftlyS2.CS2.dll");
        var types = asm.GetLoadableTypes();
        
        // Find ENetworkDisconnectionReason - what namespace?
        var t = types.FirstOrDefault(x => x.Name == "ENetworkDisconnectionReason");
        if (t != null) Console.WriteLine("Namespace: " + t.Namespace + " FullName: " + t.FullName);
        
        // Find related namespaces
        foreach (var ns in types.Select(x => x.Namespace).Distinct().OrderBy(x => x))
            if (ns != null && (ns.Contains("Network") || ns.Contains("Players") || ns.Contains("Misc")))
                Console.WriteLine("NS: " + ns);
    }
}

static class Ext {
    public static IEnumerable<Type> GetLoadableTypes(this Assembly asm) {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }
}
