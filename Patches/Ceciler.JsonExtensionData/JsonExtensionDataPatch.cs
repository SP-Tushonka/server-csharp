using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Ceciler.JsonExtensionData;

public class JsonExtensionDataPatch : IPatcher
{
    private TypeReference? _dictionaryStringObjectReference;

    public void Patch(AssemblyDefinition assembly)
    {
        var sptReferenceType = assembly.MainModule.Types.First(t => t.FullName == "SPTarkov.Server.Core.Utils.Reference.StaticReferences");
        var propertyReferenceType = sptReferenceType.Properties.First(p => p.Name == "Reference");
        var fieldReferenceType = sptReferenceType.Fields.First(p => p.Name == "_reference");
        _dictionaryStringObjectReference = propertyReferenceType.PropertyType;

        // We need to steal from the constructor the IL line 2 (index 1)
        var createDictionaryReference = sptReferenceType.GetConstructors().First(c => c.Parameters.Count == 0).Body.Instructions[1];
        var dictionaryConstructor = (MethodReference)createDictionaryReference.Operand;

        // Interlocked.CompareExchange<Dictionary<string, object>>(ref field, value, null)
        var compareExchangeDefinition = typeof(Interlocked)
            .GetMethods()
            .First(m => m.Name == nameof(Interlocked.CompareExchange) && m.IsGenericMethodDefinition && m.GetParameters().Length == 3);
        var compareExchange = new GenericInstanceMethod(assembly.MainModule.ImportReference(compareExchangeDefinition));
        compareExchange.GenericArguments.Add(_dictionaryStringObjectReference);

        var processed = new HashSet<string>();
        foreach (var typeDefinition in assembly.MainModule.Types)
        {
            if (
                !typeDefinition.Namespace.Contains("SPTarkov.Server.Core.Models")
                || typeDefinition.IsInterface
                || typeDefinition.IsEnum
                || IsStaticClass(typeDefinition)
                || processed.Contains(typeDefinition.FullName)
                || typeDefinition.IsAbstract
                || typeDefinition.HasGenericParameters
            )
            {
                continue;
            }

            var propertyDefinition = new PropertyDefinition("ExtensionData", PropertyAttributes.None, _dictionaryStringObjectReference);
            propertyDefinition.CustomAttributes.Add(propertyReferenceType.CustomAttributes.First());

            // Add backing field. Not populated by the constructor - see the getter below
            var field = new FieldDefinition("_extensionData", FieldAttributes.Private, _dictionaryStringObjectReference);
            field.CustomAttributes.Add(fieldReferenceType.CustomAttributes.First());
            typeDefinition.Fields.Add(field);

            // Add getter, creating the dictionary on first read:
            //     if (_extensionData is null) Interlocked.CompareExchange(ref _extensionData, new(), null);
            //     return _extensionData;
            //
            var get = new MethodDefinition(
                "get_ExtensionData",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                _dictionaryStringObjectReference
            );

            var loadForReturn = Instruction.Create(OpCodes.Ldarg_0);

            get.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, field));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Brtrue_S, loadForReturn));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Ldflda, field));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Newobj, dictionaryConstructor));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Call, compareExchange));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
            get.Body.Instructions.Add(loadForReturn);
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, field));
            get.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

            // Add setter
            var set = new MethodDefinition(
                "set_ExtensionData",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Void
            );

            set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, _dictionaryStringObjectReference));
            set.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            set.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            set.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, field));
            set.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

            propertyDefinition.SetMethod = set;
            propertyDefinition.GetMethod = get;
            typeDefinition.Methods.Add(set);
            typeDefinition.Methods.Add(get);

            typeDefinition.Properties.Add(propertyDefinition);

            processed.Add(typeDefinition.FullName);
        }

        var writerParams = new WriterParameters { WriteSymbols = true };
        assembly.Write(writerParams);
    }

    private bool IsStaticClass(TypeDefinition type)
    {
        return type.IsClass && type.IsAbstract && type.IsSealed;
    }

    public string Name
    {
        get { return "ExtensionData"; }
    }
}
