using Conesoft.Files;
using Conesoft.Hosting;
using Conesoft.Notifications;
using Conesoft.Services.RefreshCertificates.Helpers;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Linq;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

builder
    .AddHostConfigurationFiles()
    .AddHostEnvironmentInfo()
    .AddLoggingService()
    .AddNotificationService()
    ;

var host = builder.Build();

using var lifetime = await host.StartConsoleAsync();

var configuration = builder.Configuration;
var environment = host.Services.GetRequiredService<HostEnvironment>();
var notifier = host.Services.GetRequiredService<Notifier>();

var certificateStorage = environment.Global.Storage / "Certificates";
var deploymentSource = environment.Root / "Deployments" / "Websites";

Log.Information("certification watcher started");
Log.Information("certificate storage: {storage}", certificateStorage);
Log.Information("deployment source: {source}", deploymentSource);

var lastUpdate = DateTime.MinValue;

await foreach (var _ in deploymentSource.Live(allDirectories: false, lifetime.CancellationToken))
{
    if (lastUpdate + TimeSpan.FromHours(1) < DateTime.UtcNow)
    {
        var active = deploymentSource.Files.Select(f => f.NameWithoutExtension).ToArray();
        var inactive = certificateStorage.Files.Where(f => active.Contains(f.NameWithoutExtension) == false).Select(f => f.NameWithoutExtension).ToArray();

        foreach (var cert in active)
        {
            var file = certificateStorage / Filename.From(cert, "pfx");
            if (file.Exists && file.LoadCertificate(configuration).NotAfter > DateTime.Today + TimeSpan.FromDays(2))
            {
                var timespan = file.LoadCertificate(configuration).NotAfter - DateTime.UtcNow;
                Log.Information("certificate for {cert} is active and up to date (for {date})", cert, timespan.Humanize(precision: 2));
            }
            else
            {
                await notifier.Notify(title: "Certificate Update", $"Updating Certificate for {cert}");
                Log.Information("creating certificate for {cert}", cert);
                await file.CreateCertificate(configuration);
                Log.Information("... done, valid till {date}", file.LoadCertificate(configuration).NotAfter.Humanize());
            }
        }

        foreach (var cert in inactive)
        {
            var file = certificateStorage / Filename.From(cert, "pfx");
            if (file.LoadCertificate(configuration).NotAfter <= DateTime.Today)
            {
                Log.Information("deleted old certificate for {cert}", cert);
                file.Delete();
            }
            else
            {
                Log.Information("found old certificate for {cert}", cert);
            }
        }

        lastUpdate = DateTime.UtcNow;
    }
}
