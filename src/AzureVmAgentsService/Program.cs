using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Refit;
using Microsoft.Extensions.Configuration;
using AzureDevopsAgentOperator;
using AzureVmAgentsService.Models;

namespace AzureVmAgentsService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)               
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddRefitClient<IInstanceMetadataServiceAPI>()
                        .ConfigureHttpClient(c =>
                        {
                            c.BaseAddress = new Uri("http://169.254.169.254");

                            /* Scheduled Events is enabled for your service the first time you make a request for events.
                               You should expect a delayed response in your first call of up to two minutes.
                               https://docs.microsoft.com/en-gb/azure/virtual-machines/windows/scheduled-events */
                            c.Timeout = TimeSpan.FromSeconds(120);
                        })
                        .AddTransientHttpErrorPolicy(p => p.RetryAsync(3));

                    services.AddSingleton<IAgentOperator, AgentOperator>(sp =>
                    {                        
                        var config = sp.GetService<IConfiguration>().GetSection("drainer").Get<AzureDevopsConfig>();
                        var loggerFactory = sp.GetService<ILoggerFactory>();
                        var hostEnv = sp.GetService<IHostEnvironment>();

                        var computerName = hostEnv.IsProduction() ? Environment.MachineName : config.ComputerName;

                        if (string.IsNullOrEmpty(computerName))
                            throw new Exception("A drainer:computername has not been set in configuration");

                        return new AgentOperatorBuilder()
                          .AddLogger(loggerFactory.CreateLogger("AgentOperator"))
                          .AddInstance(config.Uri, config.Pat)
                          .SelectAgents(x =>
                          {
                              return x.Where(agent => agent.ComputerName.ToUpper() == computerName);
                          })
                          .Build().Result;
                    });
                });
    }
}


