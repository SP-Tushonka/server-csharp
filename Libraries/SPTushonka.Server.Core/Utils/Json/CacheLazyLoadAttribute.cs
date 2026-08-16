namespace SPTarkov.Server.Core.Utils.Json;

/// <summary>
/// Marks a type whose <see cref="LazyLoad{T}"/> values should hold onto what they deserialise.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class CacheLazyLoadAttribute : Attribute;
