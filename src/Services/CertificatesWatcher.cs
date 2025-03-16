using Conesoft.Files;
using Conesoft.Hosting;
using Serilog;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshCertificates.Services;

public class CertificatesWatcher(HostEnvironment environment, CertificateUpdaterService certificateUpdater) : BackgroundEntryWatcher<Directory>
{
    protected override Task<Directory> GetEntry() => Task.FromResult(environment.Global.Storage / "host" / "certificates");

    public override Task OnChange(Directory entry)
    {
        Log.Information("checking for certificates update due to certificate changes");
        return certificateUpdater.UpdateCertificates();
    }
}
