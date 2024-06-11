using Conesoft.Files;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshCertificates.Helpers;

public static class Certification
{
    public static async Task CreateCertificate(this File cert, IConfiguration configuration)
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

    public static X509Certificate2 LoadCertificate(this File file, IConfiguration configuration) => new(file.Path, configuration["hosting:certificate-password"]);

}
