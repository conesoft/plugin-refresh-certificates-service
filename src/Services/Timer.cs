using Conesoft.Hosting;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshCertificates.Services;

public class Timer(CertificateUpdaterService certificateUpdater) : PeriodicTask(TimeSpan.FromHours(1))
{
    protected override Task Process()
    {
        Log.Information("checking for certificate updates periodically");
        return certificateUpdater.UpdateCertificates();
    }
}
