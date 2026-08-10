using System.Reflection;
using Xunit;

namespace ScoutCampPlanner.ArchitectureTests;

public sealed class DomainDependencyTests
{
    [Fact]
    public void Domain_assemblies_have_no_framework_or_infrastructure_dependencies()
    {
        Assembly[] domainAssemblies =
        [
            typeof(Platform.Domain.Tenant).Assembly,
            typeof(Camp.Domain.Camp).Assembly,
            typeof(Catering.Domain.MealPlan).Assembly
        ];
        string[] forbiddenPrefixes = ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql"];

        var violations = domainAssemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies()
                .Where(reference => forbiddenPrefixes.Any(prefix => reference.Name!.StartsWith(prefix, StringComparison.Ordinal)))
                .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Catering_domain_does_not_reference_camp_implementation()
    {
        var references = typeof(Catering.Domain.MealPlan).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, x => x.Name is not null && x.Name.StartsWith("ScoutCampPlanner.Camp", StringComparison.Ordinal));
    }

    [Fact]
    public void Platform_application_has_no_infrastructure_dependencies()
    {
        var references = typeof(Platform.Application.Authentication.IPasswordPolicy).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference =>
            reference.Name is not null &&
            (reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
             reference.Name.StartsWith("Konscious", StringComparison.Ordinal) ||
             reference.Name.StartsWith("zxcvbn", StringComparison.Ordinal) ||
             reference.Name.EndsWith(".Infrastructure", StringComparison.Ordinal)));
    }
}
