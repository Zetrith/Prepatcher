using System.Collections.Generic;
using Mono.Cecil;
using System.Runtime.InteropServices;
using Mono.Cecil.Cil;

namespace Prepatcher;

internal static class Util
{
    public static string ToStringNullable(this object? o)
    {
        return o == null ? "Null" : o.ToString();
    }

    public static string ShortName(this AssemblyDefinition asm)
    {
        return asm.Name.Name;
    }

    public static string ManagedFolderOS()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "Resources/Data/Managed";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Managed";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Managed";
        throw new Exception("Unknown platform");
    }

    public static IEnumerable<TypeDefinition> BaseTypesAndSelfResolved(this TypeDefinition? type)
    {
        while (type != null)
        {
            yield return type;
            type = type.BaseType?.Resolve();
        }
    }

    public static IEnumerable<T> BFS<T>(IEnumerable<T> start, Func<T, IEnumerable<T>> next)
    {
        var result = new HashSet<T>();
        var todo = new Queue<T>();

        foreach (var o in start)
            todo.Enqueue(o);

        while (todo.Count > 0)
        {
            var t = todo.Dequeue();
            result.Add(t);

            foreach (var d in next(t))
                if (!result.Contains(d))
                    todo.Enqueue(d);
        }

        return result;
    }

    public static void SetEmptyBody(MethodDefinition def)
    {
        def.Body = new MethodBody(def)
        {
            Instructions = { Instruction.Create(OpCodes.Ret) }
        };
    }
}
