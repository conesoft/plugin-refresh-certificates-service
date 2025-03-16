using Conesoft.Files;
using Conesoft.Hosting;
using Conesoft.Notifications;
using Conesoft.Services.RefreshCertificates.Options;
using Humanizer;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshCertificates.Services;

public class CertificateUpdaterService(HostEnvironment environment, IOptions<DnsimpleConfiguration> dnsimple, IOptions<LetsEncryptConfiguration> letsEncrypt, IHttpClientFactory factory, Notifier notifier)
{
    bool alreadyUpdating = false;
    public async Task UpdateCertificates()
    {
        try
        {
            if (!alreadyUpdating)
            {
                alreadyUpdating = true;
                await ActuallyUpdateCertificates();
            }
        }
        finally
        {
            alreadyUpdating = false;
        }
    }

    private async Task ActuallyUpdateCertificates()
    {
        var certificateStorage = environment.Global.Storage / "host" / "certificates";
        var deploymentSource = environment.Global.Deployments;

        var active = deploymentSource.Directories.SelectMany(d => d.Files).Select(f => f.NameWithoutExtension).Where(IsValidDomain).ToArray();
        var inactive = certificateStorage.Files.Where(f => active.Contains(f.NameWithoutExtension) == false).Select(f => f.NameWithoutExtension).ToArray();

        foreach (var cert in active)
        {
            var file = certificateStorage / Filename.From(cert, "pfx");
            if (file.Exists && LoadCertificate(file).NotAfter > DateTime.Today + TimeSpan.FromDays(2))
            {
                var timespan = LoadCertificate(file).NotAfter - DateTime.UtcNow;
                Log.Information("certificate for {cert} is active and up to date (for {date})", cert, timespan.Humanize(precision: 2));
            }
            else
            {
                await notifier.Notify(title: "Certificate Update", $"Updating Certificate for {cert}");
                Log.Information("creating certificate for {cert}", cert);
                await CreateCertificate(file);
                Log.Information("... done, valid till {date}", LoadCertificate(file).NotAfter.Humanize());
            }
        }

        foreach (var cert in inactive)
        {
            var file = certificateStorage / Filename.From(cert, "pfx");
            if (LoadCertificate(file).NotAfter <= DateTime.Today)
            {
                Log.Information("deleted old certificate for {cert}", cert);
                await file.Delete();
            }
            else
            {
                Log.Information("found old certificate for {cert}", cert);
            }
        }
    }

    public async Task CreateCertificate(File cert)
    {
        var client = await LetsEncrypt.Client.Login(letsEncrypt.Value.Mail, factory.CreateClient, dnsimple.Value.Token, production: true);

        if (client == null)
        {
            return;
        }

        var bytes = await client.CreateCertificateFor(cert.NameWithoutExtension, letsEncrypt.Value.CertificatePassword, new()
        {
            CountryName = letsEncrypt.Value.CountryName,
            State = letsEncrypt.Value.State,
            Locality = letsEncrypt.Value.Locality,
            Organization = letsEncrypt.Value.Organization,
            OrganizationUnit = letsEncrypt.Value.OrganizationUnit
        });

        await cert.WriteBytes(bytes);
    }

    public X509Certificate2 LoadCertificate(File file) => X509CertificateLoader.LoadPkcs12FromFile(file.Path, letsEncrypt.Value.CertificatePassword);

    bool IsValidDomain(string domain) => Uri.TryCreate($"https://{domain}", UriKind.Absolute, out var result) && result.Scheme == Uri.UriSchemeHttps;
}
