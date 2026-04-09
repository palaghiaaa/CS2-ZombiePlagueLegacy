using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        var asm = Assembly.LoadFrom("..\\src\\ZombiePlagueLegacyCS2\\lib\\Economy.Contract.dll");
        var t = asm.GetTypes().FirstOrDefault(x => x.Name == "IEconomyAPIv1");
        if (t == null)
        {
            Console.WriteLine("TYPE_NOT_FOUND");
            return;
        }

        Console.WriteLine("TYPE:" + t.FullName);
        foreach (var ev in t.GetEvents())
        {
            Console.WriteLine($"EVENT:{ev.Name}:{ev.EventHandlerType.FullName}");
            foreach (var m in ev.EventHandlerType.GetMethods())
                Console.WriteLine("  MH:" + m.Name);
        }

        foreach (var m in t.GetMethods())
        {
            Console.WriteLine("METHOD:" + m.Name + ":" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)));
        }
    }
}

