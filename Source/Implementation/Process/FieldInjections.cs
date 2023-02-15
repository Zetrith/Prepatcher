using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;

namespace Prepatcher.Process;

internal partial class FieldAdder
{
    private Dictionary<(TypeDefinition targetType, TypeDefinition compType), (MethodDefinition initMethod, FieldDefinition listField)> injectionSites = new();

    internal void RegisterInjection(Type targetType, Type compType, string initMethod, string listField)
    {
        RegisterInjection(
            set.ReflectionToCecil(targetType),
            set.ReflectionToCecil(compType),
            initMethod,
            listField
        );
    }

    internal void RegisterInjection(TypeDefinition targetType, TypeDefinition compType, string initMethod, string listField)
    {
        injectionSites[(targetType, compType)] = (
            targetType.Methods.FirstOrDefault(m => m.Name == initMethod),
            targetType.Fields.FirstOrDefault(m => m.Name == listField)
        );
    }

    private void PatchInjectionSite(MethodDefinition accessor, FieldDefinition newField)
    {
        // ldtoken newfield
        // ldarg 0
        // ldarg 0
        // ldfld complist
        // call InjectionHelper.TryInject

        var (initMethod, listField) = GetInjectionSite(accessor)!.Value;

        var body = initMethod.Body;
        var retInst = body.Instructions.Last();
        body.Instructions.Remove(retInst);

        body.GetILProcessor().Emit(OpCodes.Ldtoken, newField);
        body.GetILProcessor().Emit(OpCodes.Ldarg_0);
        body.GetILProcessor().Emit(OpCodes.Ldarg_0);
        body.GetILProcessor().Emit(OpCodes.Ldfld, listField);
        body.GetILProcessor().Emit(
            OpCodes.Call,
            initMethod.Module.ImportReference(AccessTools.Method(typeof(InjectionHelper), nameof(InjectionHelper.TryInject)))
        );

        body.Instructions.Add(retInst);
    }

    private (MethodDefinition, FieldDefinition)? GetInjectionSite(MethodDefinition accessor)
    {
        var possibleTypes =
            from targetType in FirstParameterTypeResolved(accessor)!.BaseTypesAndSelfResolved()
            from fieldType in FieldType(accessor).Resolve().BaseTypesAndSelfResolved()
            select (targetType, fieldType);

        // SingleOrDefault is used to throw on ambiguity
        var siteId = possibleTypes.SingleOrDefault(p => injectionSites.ContainsKey(p));
        if (siteId == default)
            return null;

        return injectionSites[siteId];
    }

    private static bool HasInjection(MethodDefinition accessor)
    {
        return accessor.HasCustomAttribute(typeof(InjectComponentAttribute).FullName);
    }
}
