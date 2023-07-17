using System.Reflection;

namespace Prepatcher.Process;

internal interface IAssemblySet
{
    ModifiableAssembly? AddAssembly(string friendlyName, Assembly asm);
    ModifiableAssembly? AddAssembly(string friendlyName, string asmFilePath);
    bool HasAssembly(string name);
}
