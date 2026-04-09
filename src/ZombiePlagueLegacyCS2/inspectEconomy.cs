// ── Inspection Tool for Economy.Contract Reflection [DEBUG ONLY] ──────────────
// This file is a development-time utility to inspect the IEconomyAPIv1 interface
// from the Economy.Contract assembly. It is not part of the main ZPL plugin code.
//
// Note: This file uses Console.WriteLine for development/build-time reflection
// inspection only and is not invoked at runtime by the plugin.
//
// To rebuild this inspection tool (not recommended), uncomment and run:
// ---
// using System;
// using System.Linq;
// using System.Reflection;
// class P {
//   static void Main() {
//     var asm = Assembly.LoadFrom("lib/Economy.Contract.dll");
//     var t = asm.GetTypes().FirstOrDefault(x => x.Name == "IEconomyAPIv1");
//     if (t == null) { Console.WriteLine("TYPE_NOT_FOUND"); return; }
//     Console.WriteLine("TYPE:" + t.FullName);
//     foreach (var ev in t.GetEvents()) {
//       Console.WriteLine("EVENT:" + ev.Name + ":" + ev.EventHandlerType.FullName);
//       foreach (var m in ev.EventHandlerType.GetMethods()) Console.WriteLine("  MH:" + m.Name);
//     }
//     foreach (var m in t.GetMethods()) Console.WriteLine("METHOD:" + m.Name + ":" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)));
//   }
// }
// ---
