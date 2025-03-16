using Conesoft.Hosting;
using Conesoft.Notifications;
using Conesoft.Services.RefreshCertificates.Options;
using Conesoft.Services.RefreshCertificates.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services
    .AddHttpClient()
    .AddSingleton<CertificateUpdaterService>()
    .AddHostedService<DeploymentWatcher>()
    .AddHostedService<CertificatesWatcher>()
    .AddHostedService<Timer>()
    ;

var host = builder.Build();
await host.RunAsync();