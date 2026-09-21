using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>Reads the four solution assemblies with Mono.Cecil and enumerates the types each type references.</summary>
internal static class Solution
{
    public const string Domain = "PMPlatform.Domain";
    public const string Application = "PMPlatform.Application";
    public const string Infrastructure = "PMPlatform.Infrastructure";
    public const string Api = "PMPlatform.Api";

    public static readonly IReadOnlyList<string> AssemblyNames = [Domain, Application, Infrastructure, Api];

    private static readonly Lazy<IReadOnlyDictionary<string, AssemblyDefinition>> LoadedAssemblies = new(Load);

    public static AssemblyDefinition Assembly(string name) => LoadedAssemblies.Value[name];

    public static IEnumerable<TypeDefinition> Types(string assemblyName) =>
        Assembly(assemblyName).MainModule.Types.SelectMany(Flatten).Where(t => t.Name != "<Module>");

    public static IEnumerable<TypeDefinition> AllTypes() => AssemblyNames.SelectMany(Types);

    /// <summary>Namespace of the outermost declaring type, so nested and compiler-generated types attribute to their owner.</summary>
    public static string NamespaceOf(TypeReference type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type.Namespace;
    }

    public static TypeReference Outermost(TypeReference type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }

    public static string AssemblyOf(TypeReference type) => type.Scope switch
    {
        AssemblyNameReference reference => reference.Name,
        ModuleDefinition module => module.Assembly.Name.Name,
        ModuleReference moduleReference => moduleReference.Name,
        _ => string.Empty,
    };

    public static bool IsSolutionType(TypeReference type) => AssemblyNames.Contains(AssemblyOf(type));

    /// <summary>Every type this type references — signatures, attributes, and method bodies — excluding itself and its nested types.</summary>
    public static IEnumerable<TypeReference> ReferencedTypes(TypeDefinition type)
    {
        string self = Outermost(type).FullName;
        return CollectReferences(type)
            .SelectMany(Unwrap)
            .Where(t => Outermost(t).FullName != self)
            .DistinctBy(t => t.FullName, StringComparer.Ordinal);
    }

    /// <summary>Types referenced from the fields and properties of a type — the shape of its state, not its behaviour.</summary>
    public static IEnumerable<TypeReference> StateTypes(TypeDefinition type) =>
        type.Fields.Select(f => f.FieldType)
            .Concat(type.Properties.Select(p => p.PropertyType))
            .SelectMany(Unwrap)
            .DistinctBy(t => t.FullName, StringComparer.Ordinal);

    /// <summary>Every string literal in a type: constant fields and <c>ldstr</c> operands in method bodies.</summary>
    public static IEnumerable<string> StringLiterals(TypeDefinition type) =>
        type.Fields.Where(f => f.HasConstant).Select(f => f.Constant).OfType<string>()
            .Concat(type.Methods.Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions)
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Select(i => (string)i.Operand));

    public static bool DerivesFrom(TypeDefinition type, string baseTypeFullName)
    {
        for (TypeReference? current = type.BaseType; current is not null; current = current.Resolve()?.BaseType)
        {
            if (current.FullName == baseTypeFullName || current.GetElementType().FullName == baseTypeFullName)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<TypeReference> CollectReferences(TypeDefinition type)
    {
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }

        foreach (InterfaceImplementation implementation in type.Interfaces)
        {
            yield return implementation.InterfaceType;
        }

        foreach (CustomAttribute attribute in type.CustomAttributes)
        {
            yield return attribute.AttributeType;
        }

        foreach (FieldDefinition field in type.Fields)
        {
            yield return field.FieldType;
        }

        foreach (PropertyDefinition property in type.Properties)
        {
            yield return property.PropertyType;
        }

        foreach (MethodDefinition method in type.Methods)
        {
            yield return method.ReturnType;

            foreach (ParameterDefinition parameter in method.Parameters)
            {
                yield return parameter.ParameterType;
            }

            foreach (CustomAttribute attribute in method.CustomAttributes)
            {
                yield return attribute.AttributeType;
            }

            if (!method.HasBody)
            {
                continue;
            }

            foreach (VariableDefinition variable in method.Body.Variables)
            {
                yield return variable.VariableType;
            }

            foreach (Instruction instruction in method.Body.Instructions)
            {
                switch (instruction.Operand)
                {
                    case TypeReference typeReference:
                        yield return typeReference;
                        break;
                    case MethodReference methodReference:
                        yield return methodReference.DeclaringType;
                        yield return methodReference.ReturnType;
                        foreach (ParameterDefinition parameter in methodReference.Parameters)
                        {
                            yield return parameter.ParameterType;
                        }

                        if (methodReference is GenericInstanceMethod genericMethod)
                        {
                            foreach (TypeReference argument in genericMethod.GenericArguments)
                            {
                                yield return argument;
                            }
                        }

                        break;
                    case FieldReference fieldReference:
                        yield return fieldReference.DeclaringType;
                        yield return fieldReference.FieldType;
                        break;
                    default:
                        break;
                }
            }
        }
    }

    private static IEnumerable<TypeReference> Unwrap(TypeReference type)
    {
        switch (type)
        {
            case GenericParameter:
                yield break;
            case GenericInstanceType generic:
                yield return generic.ElementType;
                foreach (TypeReference argument in generic.GenericArguments.SelectMany(Unwrap))
                {
                    yield return argument;
                }

                break;
            case TypeSpecification specification:
                foreach (TypeReference element in Unwrap(specification.ElementType))
                {
                    yield return element;
                }

                break;
            default:
                yield return type;
                break;
        }
    }

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes.SelectMany(Flatten))
        {
            yield return nested;
        }
    }

    private static Dictionary<string, AssemblyDefinition> Load()
    {
        string directory = AppContext.BaseDirectory;
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(directory);
        var parameters = new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate };
        return AssemblyNames.ToDictionary(
            name => name,
            name => AssemblyDefinition.ReadAssembly(Path.Combine(directory, name + ".dll"), parameters),
            StringComparer.Ordinal);
    }
}
