using System.Reflection;

namespace BsfConverter.Core.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public class BsfModelAttribute : Attribute {}
