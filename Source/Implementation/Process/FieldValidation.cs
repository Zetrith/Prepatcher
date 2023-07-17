using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Prepatcher.Process;

internal partial class FieldAdder
{
    private string? CheckFieldAccessor(MethodDefinition accessor)
    {
        if (accessor.Parameters.Count() != 1)
            return "Accessor must have exactly one parameter";

        var target = FirstParameterTypeResolved(accessor);
        if (target == null)
            return "Couldn't resolve target type";

        if (target.IsInterface)
            return "Target type can't be an interface";

        if (!GenericArgumentsOf(accessor.Parameters.First().ParameterType).SequenceEqual(accessor.GenericParameters))
            return "The generic arguments of the target type don't match the generic parameters of the accessor";

        if (!set.FindAssembly(target)!.AllowPatches)
            return "Target type is not modifiable";

        if (HasInjection(accessor))
        {
            if (GetInjectionSite(accessor) == null)
                return "Unknown injection owner type/component type pair";

            if (accessor.ReturnType.IsByReference)
                return "Injected field cannot have a setter";
        }

        return null;
    }

    private static IEnumerable<TypeReference> GenericArgumentsOf(TypeReference t)
    {
        return t is GenericInstanceType gType ? gType.GenericArguments : Enumerable.Empty<TypeReference>();
    }
}
