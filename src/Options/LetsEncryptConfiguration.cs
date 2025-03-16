using Microsoft.Extensions.Configuration;

namespace Conesoft.Services.RefreshCertificates.Options;

public class LetsEncryptConfiguration
{
    public string Mail { get; init; } = "";
    [ConfigurationKeyName("certificate-password")] public string CertificatePassword { get; init; } = "";
    [ConfigurationKeyName("country-name")] public string CountryName { get; init; } = "";
    public string State { get; init; } = "";
    public string Locality { get; init; } = "";
    public string Organization { get; init; } = "";
    [ConfigurationKeyName("organization-unit")] public string OrganizationUnit { get; init; } = "";
}