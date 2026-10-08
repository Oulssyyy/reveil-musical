using System.Reflection;
using ReveilMusical.Application.WakeUp;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.UnitTests.Architecture;

/// <summary>
/// Garde-fous automatiques sur les deux exigences d'architecture :
/// isolation du métier et injection de dépendances par abstractions.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Track).Assembly;
    private static readonly Assembly Application = typeof(WakeUpService).Assembly;

    private static readonly string[] ProviderVocabulary =
        ["itunes", "musicbrainz", "trackview", "http", "mail", "sms", "push", "whatsapp"];

    [Fact]
    public void Domain_depends_on_no_other_project_nor_third_party_package()
    {
        var references = Domain.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.All(references, name => Assert.True(name.StartsWith("System") || name == "netstandard", name));
    }

    [Fact]
    public void Application_only_knows_the_domain_and_abstractions()
    {
        var references = Application.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, n => n.Contains("Infrastructure") || n.Contains("Cli"));
        Assert.DoesNotContain(references, n => n.StartsWith("System.Net"));
        Assert.All(references.Where(n => n.StartsWith("Microsoft.Extensions")),
            n => Assert.True(n.EndsWith("Abstractions") || n == "Microsoft.Extensions.Options", n));
    }

    [Fact]
    public void Business_types_and_members_never_mention_a_provider_or_a_channel()
    {
        var names = Domain.GetTypes().Concat(Application.GetTypes())
            .Where(t => t.Namespace?.StartsWith("ReveilMusical") == true)
            .SelectMany(t => new[] { t.Name }.Concat(t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name)))
            .ToList();

        foreach (var word in ProviderVocabulary)
            Assert.DoesNotContain(names, n => n.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Business_services_receive_only_abstractions()
    {
        var services = Application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(i => i.Namespace!.StartsWith("ReveilMusical")));

        foreach (var service in services)
        foreach (var parameter in service.GetConstructors().SelectMany(c => c.GetParameters()))
        {
            var type = parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? parameter.ParameterType.GetGenericArguments()[0]
                : parameter.ParameterType;
            Assert.True(type.IsInterface, $"{service.Name}({parameter.Name}) dépend de la classe concrète {type.Name}");
        }
    }
}
