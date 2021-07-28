using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using UnityEngine;
using System.Runtime.InteropServices;
using System.ComponentModel;
using Mono.Cecil.Cil;

namespace Prepatcher
{
    public static class Util
    {
        public static void Insert<T>(this IList<T> list, int index, params T[] items)
        {
            foreach (T item in items)
                list.Insert(index++, item);
        }

        public static object TryConvert(this TypeConverter converter, string s)
        {
            try
            {
                return converter.ConvertFromInvariantString(s);
            }
            catch
            {
                return null;
            }
        }

        public static bool CallsAThisCtor(MethodDefinition method)
        {
            foreach (var inst in method.Body.Instructions)
                if (inst.OpCode == OpCodes.Call && inst.Operand is MethodDefinition m && m.IsConstructor && m.DeclaringType == method.DeclaringType)
                    return true;
            return false;
        }

        public static OpCode? GetConstantOpCode(object c)
        {
            return GetConstantOpCode(c.GetType());
        }

        public static OpCode? GetConstantOpCode(Type t)
        {
            var code = Type.GetTypeCode(t);

            if (code >= TypeCode.Boolean && code <= TypeCode.UInt32)
                return OpCodes.Ldc_I4;

            if (code >= TypeCode.Int64 && code <= TypeCode.UInt64)
                return OpCodes.Ldc_I8;

            if (code == TypeCode.Single)
                return OpCodes.Ldc_R4;

            if (code == TypeCode.Double)
                return OpCodes.Ldc_R8;

            if (code == TypeCode.String)
                return OpCodes.Ldstr;

            return null;
        }

        public static ulong ToUInt64(object obj)
        {
            if (obj is ulong u)
                return u;
            return (ulong)Convert.ToInt64(obj);
        }

        public static object FromUInt64(ulong from, Type to) => Type.GetTypeCode(to) switch
        {
            TypeCode.Byte => (byte)from,
            TypeCode.SByte => (sbyte)from,
            TypeCode.Int16 => (short)from,
            TypeCode.UInt16 => (ushort)from,
            TypeCode.Int32 => (int)from,
            TypeCode.UInt32 => (uint)from,
            TypeCode.Int64 => (long)from,
            TypeCode.UInt64 => from,
            _ => null
        };

        public static ulong? FindNotTaken(ulong start, ulong max, HashSet<ulong> taken)
        {
            // TODO maybe wrap around?
            for (ulong i = start; i <= max; i++)
            {
                if (taken.Contains(i)) continue;
                return i;
            }

            return null;
        }

        static string ManagedFolderOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "Resources/Data/Managed";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Managed";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Managed";
            return null;
        }

        public static string DataPath(string file)
        {
            return Path.Combine(Application.dataPath, ManagedFolderOS(), file);
        }

        public static IEnumerable<MethodDefinition> GetConstructors(this TypeDefinition self)
        {
            return self.Methods.Where(method => method.IsConstructor);
        }

        public static HashSet<string> AssembliesDependingOn(IEnumerable<Assembly> assemblies, params string[] on)
        {
            var dependants = new Dictionary<string, HashSet<string>>();

            foreach (var asm in assemblies)
                foreach (var reference in asm.GetReferencedAssemblies())
                {
                    if (!dependants.TryGetValue(reference.Name, out var set))
                        dependants[reference.Name] = set = new HashSet<string>();

                    set.Add(asm.GetName().Name);
                }

            var result = new HashSet<string>();
            var todo = new Queue<string>();

            foreach (var o in on)
                todo.Enqueue(o);

            while (todo.Count > 0)
            {
                var t = todo.Dequeue();
                result.Add(t);

                if (!dependants.ContainsKey(t)) continue;

                foreach (var d in dependants[t])
                    if (!result.Contains(d))
                        todo.Enqueue(d);
            }

            return result;
        }
    }
}
