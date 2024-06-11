using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Net.Http;
using System.Threading.Tasks;
using System;

namespace Conesoft.Helpers;

public class Notification
{
    public static async Task Notify(string message)
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
            Log.Information("Failed to notify");
        }
    }
}
