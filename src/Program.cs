using Conesoft.Files;
using Conesoft.Hosting;
using Conesoft.Notifications;
using Conesoft.Services.RefreshCertificates.Helpers;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Linq;
using System.Net.Http;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

builder
    .AddHostConfigurationFiles(configurator =>
    {
        configurator.Add<DnsimpleConfiguration>("dnsimple");
        configurator.Add<LetsEncryptConfiguration>("letsencrypt");
    })
    .AddHostEnvironmentInfo()
    .AddLoggingService()
    .AddNotificationService()
    ;

builder.Services.AddHttpClient();

var host = builder.Build();

using var lifetime = await host.StartConsoleAsync();

var environment = host.Services.GetRequiredService<HostEnvironment>();
var notifier = host.Services.GetRequiredService<Notifier>();

var dnsimple = host.Services.GetRequiredService<IOptions<DnsimpleConfiguration>>();
var letsencrypt = host.Services.GetRequiredService<IOptions<LetsEncryptConfiguration>>();
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();


var certificateStorage = environment.Global.Storage / "Certificates";
var deploymentSource = environment.Global.Deployments;

Log.Information("certification watcher started");
Log.Information("certificate storage: {storage}", certificateStorage);
Log.Information("deployment source: {source}", deploymentSource);

var lastUpdate = DateTime.MinValue;

var watcherCancellationTokenSource = deploymentSource.Live(async () =>
{
    if (lastUpdate + TimeSpan.FromHours(1) < DateTime.UtcNow)
    {
        var active = deploymentSource.Directories.SelectMany(d => d.Files).Select(f => f.NameWithoutExtension).Where(IsValidDomain).ToArray();
        var inactive = certificateStorage.Files.Where(f => active.Contains(f.NameWithoutExtension) == false).Select(f => f.NameWithoutExtension).ToArray();

        foreach (var cert in active)
        {
            var file = certificateStorage / Filename.From(cert, "pfx");
            if (file.Exists && file.LoadCertificate(letsencrypt).NotAfter > DateTime.Today + TimeSpan.FromDays(2))
            {
                var timespan = file.LoadCertificate(letsencrypt).NotAfter - DateTime.UtcNow;
                Log.Information("certificate for {cert} is active and up to date (for {date})", cert, timespan.Humanize(precision: 2));
            }
            else
            {
                await notifier.Notify(title: "Certificate Update", $"Updating Certificate for {cert}");
                Log.Information("creating certificate for {cert}", cert);
                await file.CreateCertificate(letsencrypt, dnsimple, httpClientFactory);
                Log.Information("... done, valid till {date}", file.LoadCertificate(letsencrypt).NotAfter.Humanize());
            }
        }

        foreach (var cert in inactive)
        {
            var file = certificateStorage / Filename.From(cert, "pfx");
            if (file.LoadCertificate(letsencrypt).NotAfter <= DateTime.Today)
            {
                Log.Information("deleted old certificate for {cert}", cert);
                await file.Delete();
            }
            else
            {
                Log.Information("found old certificate for {cert}", cert);
            }
        }

        lastUpdate = DateTime.UtcNow;
    }
});

lifetime.CancellationToken.WaitHandle.WaitOne();

watcherCancellationTokenSource.Cancel();

static bool IsValidDomain(string domain) => Uri.TryCreate($"https://{domain}", UriKind.Absolute, out var result) && result.Scheme == Uri.UriSchemeHttps;