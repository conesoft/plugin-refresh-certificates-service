using Conesoft.Files;
using Conesoft.Hosting;
using Serilog;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshCertificates.Services;

public class DeploymentWatcher(HostEnvironment environment, CertificateUpdaterService certificateUpdater) : BackgroundEntryWatcher<Directory>
{
    protected override Task<Directory> GetEntry() => Task.FromResult(environment.Global.Deployments);
    protected override bool AllDirectories => true;

    public override Task OnChange(Directory entry)
    {
        Log.Information("checking for certificates update due to deployment changes");
        return certificateUpdater.UpdateCertificates();
    }
}
