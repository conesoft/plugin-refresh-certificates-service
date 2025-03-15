using Microsoft.Extensions.Configuration;

namespace Conesoft.Services.RefreshCertificates.Helpers;

public class LetsEncryptConfiguration
{
    public string Mail { get; set; }
    [ConfigurationKeyName("certificate-password")] public string CertificatePassword { get; set; }
    [ConfigurationKeyName("country-name")] public string CountryName { get; set; }
    public string State { get; set; }
    public string Locality { get; set; }
    public string Organization { get; set; }
    [ConfigurationKeyName("organization-unit")] public string OrganizationUnit { get; set; }
}