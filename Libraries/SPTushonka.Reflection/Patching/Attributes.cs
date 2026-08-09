using JetBrains.Annotations;

namespace SPTarkov.Reflection.Patching;

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
public class PatchPrefixAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
public class PatchPostfixAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
public class PatchTranspilerAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
public class PatchFinalizerAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
public class PatchIlManipulatorAttribute : Attribute { }

/// <summary>
///     If added to a patch, it will not be used during auto patching
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class IgnoreAutoPatchAttribute : Attribute;

/// <summary>
///     If added to a patch, it will only be enabled during debug builds
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DebugPatchAttribute : Attribute;
