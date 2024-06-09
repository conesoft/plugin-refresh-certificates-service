using Conesoft.Files;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshCertificates
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonFile(Hosting.Host.GlobalConfiguration.Path).Build();

        X509Certificate2 Load(File file) => new(file.Path, configuration["hosting:certificate-password"]);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var certificateStorage = Hosting.Host.GlobalStorage / "Certificates";
            var deploymentSource = Hosting.Host.Root / "Deployments" / "Websites";

            await Notify($"Certificate watcher started");

            var lastUpdate = DateTime.MinValue;

            await foreach (var files in deploymentSource.Live().Changes().WithCancellation(stoppingToken))
            {
                if (files.ThereAreChanges || lastUpdate + TimeSpan.FromHours(1) < DateTime.UtcNow)
                {
                    var active = files.All.Files().Select(f => f.NameWithoutExtension).ToArray();
                    var inactive = certificateStorage.Files.Where(f => active.Contains(f.NameWithoutExtension) == false).ToArray();

                    foreach (var cert in active)
                    {
                        var file = certificateStorage / Filename.From(cert, "pfx");
                        if (file.Exists && Load(file).NotAfter > DateTime.Today + TimeSpan.FromDays(2))
                        {
                            logger.LogInformation("certificate for {cert} is active and up to date (till {date})", cert, Load(file).NotAfter);
                        }
                        else
                        {
                            await Notify($"Updating Certificate for {cert}");
                            logger.LogInformation("creating certificate for {cert}", cert);
                            await CreateCertificateFor(file);
                            logger.LogInformation("... done, valid till {date}", Load(file).NotAfter);
                        }
                    }
                    foreach (var cert in inactive)
                    {
                        // delete old certificates
                        logger.LogInformation("found old certificate for {cert}", cert);
                    }
                    lastUpdate = DateTime.UtcNow;
                }
            }
        }

        private async Task CreateCertificateFor(File cert)
        {
            var letsEncryptMail = configuration["hosting:letsencrypt-mail"];
            var dnsimpleToken = configuration["hosting:dnsimple-token"];
            var certificatePassword = configuration["hosting:certificate-password"];

            var certificateStorage = Hosting.Host.GlobalStorage / "Certificates";

            var client = await LetsEncrypt.Client.Login(letsEncryptMail, () => new HttpClient(), dnsimpleToken, production: true);

            var bytes = await client.CreateCertificateFor(cert.NameWithoutExtension, certificatePassword, new LetsEncrypt.CertificateInformation()
            {
                CountryName = "CH",
                State = "Switzerland",
                Locality = "Burg AG",
                Organization = "Conesoft",
                OrganizationUnit = "Dev"
            });

            await cert.WriteBytes(bytes);
        }

        private static async Task Notify(string message)
        {
            var configuration = new ConfigurationBuilder().AddJsonFile(Hosting.Host.GlobalSettings.Path).Build();
            var conesoftSecret = configuration["conesoft:secret"] ?? throw new Exception("Conesoft Secret not found in Configuration");

            var title = "Certificate Update";

            var query = new QueryBuilder
            {
                { "token", conesoftSecret! },
                { "title", title },
                { "message", message }
            };

            try
            {
                await new HttpClient().GetAsync($@"https://conesoft.net/notify" + query.ToQueryString());
            }
            catch (Exception)
            {
                Console.WriteLine("Failed to notify");
            }
        }
    }
}
