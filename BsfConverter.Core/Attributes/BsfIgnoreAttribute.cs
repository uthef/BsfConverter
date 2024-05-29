using System.Reflection;

namespace BsfConverter.Core.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class BsfIgnoreAttribute : Attribute {}
