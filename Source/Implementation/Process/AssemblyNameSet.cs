using System.Collections.Generic;
using System.Reflection;

namespace Prepatcher.Process;

public class AssemblyNameSet : IAssemblySet
{
    private Dictionary<string, string> assemblies = new();

    public ModifiableAssembly AddAssembly(string friendlyName, Assembly asm)
    {
        assemblies[asm.GetName().Name] = friendlyName;
        return null!;
    }

    public ModifiableAssembly AddAssembly(string friendlyName, string asmFilePath)
    {
        assemblies[AssemblyName.GetAssemblyName(asmFilePath).Name] = friendlyName;
        return null!;
    }

    public bool HasAssembly(string name)
    {
        return assemblies.ContainsKey(name);
    }

    public string GetFriendlyName(string name)
    {
        return assemblies[name];
    }
}
