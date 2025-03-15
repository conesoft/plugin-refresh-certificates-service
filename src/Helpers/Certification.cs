using Conesoft.Files;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshCertificates.Helpers;

public static class Certification
{
    public static async Task CreateCertificate(this File cert, IOptions<LetsEncryptConfiguration> letsEncrypt, IOptions<DnsimpleConfiguration> dnsimple, IHttpClientFactory httpClientFactory)
    {
        var client = await LetsEncrypt.Client.Login(letsEncrypt.Value.Mail, httpClientFactory.CreateClient, dnsimple.Value.Token, production: true);

        var bytes = await client.CreateCertificateFor(cert.NameWithoutExtension, letsEncrypt.Value.CertificatePassword, new LetsEncrypt.CertificateInformation()
        {
            CountryName = letsEncrypt.Value.CountryName,
            State = letsEncrypt.Value.State,
            Locality = letsEncrypt.Value.Locality,
            Organization = letsEncrypt.Value.Organization,
            OrganizationUnit = letsEncrypt.Value.OrganizationUnit
        });

        await cert.WriteBytes(bytes);
    }

    public static X509Certificate2 LoadCertificate(this File file, IOptions<LetsEncryptConfiguration> letsEncrypt) => X509CertificateLoader.LoadPkcs12FromFile(file.Path, letsEncrypt.Value.CertificatePassword);
}
