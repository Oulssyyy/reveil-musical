using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReveilMusical.Application.WakeUp;
using ReveilMusical.Cli;
using ReveilMusical.Infrastructure;

// Point d'entrée appelé par l'ordonnanceur à l'heure du réveil : <userId> <jour> <météo>
if (!WakeUpArguments.TryParse(args, out var request, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}

var builder = Host.CreateApplicationBuilder();
builder.Services.AddReveilMusical(builder.Configuration);
using var host = builder.Build();

var report = await host.Services.GetRequiredService<IWakeUpService>().WakeUpAsync(request!);

Console.WriteLine();
Console.WriteLine($"Utilisateur : {report.UserId}");
Console.WriteLine($"Morceau     : {report.Track} (source : {report.TrackSource})");
Console.WriteLine($"Canal       : {report.Channel?.ToString() ?? "aucun"}");
Console.WriteLine($"Délivré     : {(report.Delivered ? "oui" : "NON")}{(report.Degraded ? " — mode dégradé" : "")}");
foreach (var incident in report.Incidents)
    Console.WriteLine($"  ! {incident}");

return report.Delivered ? 0 : 1;
