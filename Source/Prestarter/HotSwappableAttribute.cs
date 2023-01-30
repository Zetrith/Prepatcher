using System;

namespace Prestarter;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class HotSwappableAttribute : Attribute
{
}
