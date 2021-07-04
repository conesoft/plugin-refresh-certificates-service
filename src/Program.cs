using Conesoft.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;

namespace Conesoft.Services.RefreshCertificates
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var log = Hosting.Host.Root / Filename.From(Hosting.Host.Name.ToLowerInvariant() + " log", "txt");

                System.IO.File.WriteAllText(log.Path, "");

                Log.Logger = new LoggerConfiguration()
                    .WriteTo.File(log.Path, buffered: false, shared: true, flushToDiskInterval: TimeSpan.FromSeconds(1))
                    .CreateLogger();
            }
            catch (Exception)
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .CreateLogger();
            }

            Log.Information("App has started");

            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            finally
            {
                Log.Information("App has stopped");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                })
                .UseSerilog();
    }
}
