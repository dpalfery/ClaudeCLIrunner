using ClaudeCLIRunner.Configuration;
using ClaudeCLIRunner.Interfaces;
using ClaudeCLIRunner.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClaudeCLIRunner;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception: {ex}");
            Environment.Exit(1);
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", 
                    optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables("CLAUDECLI_");
                config.AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Configuration
                services.Configure<ClaudeCliConfig>(
                    hostContext.Configuration.GetSection("ClaudeCliConfig"));

                // Services
                services.AddSingleton<IWorkItemService, MockWorkItemService>();
                services.AddSingleton<IClaudeCliExecutor, ClaudeCliExecutor>();
                services.AddSingleton<WorkItemOrchestrator>();

                // Background service
                services.AddHostedService<PollingBackgroundService>();
            })
            .ConfigureLogging((hostContext, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                logging.AddConsole();
                logging.AddDebug();
                
                // File logging can be added with third-party providers like Serilog if needed
            })
            .UseWindowsService(); // Enable running as Windows Service
}
