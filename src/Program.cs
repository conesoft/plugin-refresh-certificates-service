using Conesoft.Files;
using Conesoft.Hosting;
using Conesoft.Services.RefreshCertificates.Helpers;
using Humanizer;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Linq;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
builder.Services
    .AddLoggingToHost()
    .AddPeriodicGarbageCollection(TimeSpan.FromMinutes(5));

var host = builder.Build();

await host.StartAsync();

var configuration = new ConfigurationBuilder().AddJsonFile(Host.GlobalConfiguration.Path).Build();

var certificateStorage = Host.GlobalStorage / "Certificates";
var deploymentSource = Host.Root / "Deployments" / "Websites";

Log.Information("certification watcher started");
Log.Information("certificate storage: {storage}", certificateStorage);
Log.Information("deployment source: {source}", deploymentSource);

var lastUpdate = DateTime.MinValue;

await foreach (var files in deploymentSource.Live().Changes())
{
    if (files.ThereAreChanges || lastUpdate + TimeSpan.FromHours(1) < DateTime.UtcNow)
    {
        var active = files.All.Files().Select(f => f.NameWithoutExtension).ToArray();
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
                await Conesoft.Helpers.Notification.Notify($"Updating Certificate for {cert}");
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
