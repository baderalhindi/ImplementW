using Mono.Cecil;

namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>A-4: the project dependency directions of ADR-003 §4.2 (L-1 to L-4).</summary>
public sealed class LayeringTests
{
    private const string CompositionRoot = "Program";

    [Fact]
    public void DomainReferencesNoOtherProject()
    {
        IEnumerable<string> solutionReferences = Solution.Assembly(Solution.Domain).MainModule.AssemblyReferences
            .Select(r => r.Name)
            .Where(n => n.StartsWith("PMPlatform.", StringComparison.Ordinal));

        Assert.Empty(solutionReferences);
    }

    [Fact]
    public void ApplicationReferencesNeitherInfrastructureNorApi()
    {
        IEnumerable<string> violations = Solution.Types(Solution.Application)
            .SelectMany(t => Solution.ReferencedTypes(t).Select(r => (Type: t, Referenced: r)))
            .Where(x => Solution.AssemblyOf(x.Referenced) is Solution.Infrastructure or Solution.Api)
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}");

        Assert.Empty(violations);
    }

    [Fact]
    public void ApiReferencesInfrastructureOnlyFromTheCompositionRoot()
    {
        IEnumerable<string> violations = Solution.Types(Solution.Api)
            .Where(t => Solution.Outermost(t).Name != CompositionRoot)
            .SelectMany(t => Solution.ReferencedTypes(t).Select(r => (Type: t, Referenced: r)))
            .Where(x => Solution.AssemblyOf(x.Referenced) == Solution.Infrastructure)
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}");

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryProjectAssemblyIsPresent()
    {
        foreach (string name in Solution.AssemblyNames)
        {
            AssemblyDefinition assembly = Solution.Assembly(name);
            Assert.Equal(name, assembly.Name.Name);
        }
    }
}
