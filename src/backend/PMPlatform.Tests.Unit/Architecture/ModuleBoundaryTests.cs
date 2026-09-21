using Mono.Cecil;

namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>A-1, A-2, A-3 and A-6: the module boundary of ADR-003 §6 and §8, checked on every build.</summary>
public sealed class ModuleBoundaryTests
{
    private const string FeaturesNamespace = "PMPlatform.Application.Features.";
    private const string PersistenceNamespace = "PMPlatform.Infrastructure.Persistence";
    private const string DbContextTypeName = "Microsoft.EntityFrameworkCore.DbContext";
    private const string DbSetTypeName = "Microsoft.EntityFrameworkCore.DbSet`1";

    [Fact]
    public void EveryFeatureNamespaceNamesARegisteredModule()
    {
        IEnumerable<string> unregistered = Solution.Types(Solution.Application)
            .Select(Solution.NamespaceOf)
            .Where(ns => ns.StartsWith(FeaturesNamespace, StringComparison.Ordinal))
            .Select(ns => ns[FeaturesNamespace.Length..].Split('.')[0])
            .Where(module => !ModuleRegistry.Modules.Contains(module))
            .Distinct(StringComparer.Ordinal);

        Assert.Empty(unregistered);
    }

    /// <summary>A-1: a module reaches another module only through that module's Contracts.</summary>
    [Fact]
    public void ModulesReferenceOtherModulesOnlyThroughContracts()
    {
        IEnumerable<string> violations = CrossModuleReferences()
            .Where(x => !IsContracts(x.Referenced, x.ReferencedModule))
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}");

        Assert.Empty(violations);
    }

    /// <summary>A-6: every cross-module reference is an edge ADR-003 §8 allows.</summary>
    /// <remarks>
    /// Until every module exists the check is one-directional: an unregistered reference fails; a registered
    /// edge not yet implemented does not. The equality form (§10) is switched on when the last module lands.
    /// </remarks>
    [Fact]
    public void CrossModuleReferencesAreRegisteredEdges()
    {
        IEnumerable<string> unregistered = CrossModuleReferences()
            .Select(x => (x.ReferrerModule, x.ReferencedModule))
            .Distinct()
            .Where(edge => !ModuleRegistry.Allows(edge.ReferrerModule, edge.ReferencedModule))
            .Select(edge => $"{edge.ReferrerModule} -> {edge.ReferencedModule}");

        Assert.Empty(unregistered);
    }

    /// <summary>A-2: a repository is referenced only by its owning module and the Infrastructure composition root.</summary>
    [Fact]
    public void RepositoriesAreReferencedOnlyByTheirOwningModule()
    {
        IEnumerable<string> violations = Solution.AllTypes()
            .SelectMany(t => Solution.ReferencedTypes(t).Select(r => (Type: t, Referenced: r)))
            .Where(x => Solution.IsSolutionType(x.Referenced) && IsRepository(x.Referenced))
            .Where(x => Solution.NamespaceOf(x.Type) != Solution.Infrastructure)
            .Where(x => ModuleRegistry.ModuleOf(Solution.NamespaceOf(x.Type)) != ModuleRegistry.ModuleOf(Solution.NamespaceOf(x.Referenced)))
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}");

        Assert.Empty(violations);
    }

    /// <summary>A-2: DbContext and DbSet exist only under Infrastructure/Persistence.</summary>
    [Fact]
    public void DbContextAndDbSetAreConfinedToPersistence()
    {
        IEnumerable<string> violations = Solution.AllTypes()
            .Where(t => !Solution.NamespaceOf(t).StartsWith(PersistenceNamespace, StringComparison.Ordinal))
            .SelectMany(t => Solution.ReferencedTypes(t).Select(r => (Type: t, Referenced: r)))
            .Where(x => x.Referenced.FullName is DbContextTypeName or DbSetTypeName)
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}");

        Assert.Empty(violations);
    }

    /// <summary>A-3: no entity holds a navigation to an entity another module owns; foreign identity is a value.</summary>
    [Fact]
    public void DomainTypesHoldNoStateOfAnotherModule()
    {
        IEnumerable<string> violations = Solution.Types(Solution.Domain)
            .Select(t => (Type: t, Module: ModuleRegistry.ModuleOf(Solution.NamespaceOf(t))))
            .Where(x => x.Module is not null)
            .SelectMany(x => Solution.StateTypes(x.Type).Select(s => (x.Type, x.Module, State: s)))
            .Where(x => Solution.AssemblyOf(x.State) == Solution.Domain)
            .Where(x => ModuleRegistry.ModuleOf(Solution.NamespaceOf(x.State)) is { } owner && owner != x.Module)
            .Select(x => $"{x.Type.FullName} holds {x.State.FullName}");

        Assert.Empty(violations);
    }

    private static IEnumerable<(TypeDefinition Type, string ReferrerModule, TypeReference Referenced, string ReferencedModule)> CrossModuleReferences() =>
        Solution.Types(Solution.Application)
            .Select(t => (Type: t, Module: FeatureModuleOf(t)))
            .Where(x => x.Module is not null)
            .SelectMany(x => Solution.ReferencedTypes(x.Type)
                .Where(r => Solution.AssemblyOf(r) == Solution.Application)
                .Select(r => (x.Type, ReferrerModule: x.Module!, Referenced: r, ReferencedModule: FeatureModuleOf(r))))
            .Where(x => x.ReferencedModule is not null && x.ReferencedModule != x.ReferrerModule)
            .Select(x => (x.Type, x.ReferrerModule, x.Referenced, ReferencedModule: x.ReferencedModule!));

    private static string? FeatureModuleOf(TypeReference type)
    {
        string ns = Solution.NamespaceOf(type);
        return ns.StartsWith(FeaturesNamespace, StringComparison.Ordinal)
            ? ns[FeaturesNamespace.Length..].Split('.')[0]
            : null;
    }

    private static bool IsContracts(TypeReference type, string module) =>
        Solution.NamespaceOf(type).StartsWith($"{FeaturesNamespace}{module}.Contracts", StringComparison.Ordinal);

    private static bool IsRepository(TypeReference type) =>
        type.Name.EndsWith("Repository", StringComparison.Ordinal) && ModuleRegistry.ModuleOf(Solution.NamespaceOf(type)) is not null;
}
