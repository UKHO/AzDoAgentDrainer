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
using Polly.Extensions.Http;

namespace AzureVmAgentsService
{
    public class Program
    {
        static Random jitterer = new Random();

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddTransient<SimpleLoggingHandler>();
                    services.AddRefitClient<IInstanceMetadataServiceAPI>()
                        .ConfigureHttpClient(c =>
                        {
                            c.BaseAddress = new Uri("http://169.254.169.254");

                            /* Scheduled Events is enabled for your service the first time you make a request for events.
                               You should expect a delayed response in your first call of up to two minutes.
                               https://docs.microsoft.com/en-gb/azure/virtual-machines/windows/scheduled-events */
                            c.Timeout = TimeSpan.FromSeconds(120);
                        })
                        .AddHttpMessageHandler<SimpleLoggingHandler>()
                        .AddPolicyHandler((service, request) => HttpPolicyExtensions
                            .HandleTransientHttpError()
                            .OrResult(r => (int)r.StatusCode == 410) // Retry after some time for a max of 70 seconds 
                            .OrResult(r => (int)r.StatusCode == 429) // The API currently supports a maximum of 5 queries per second
                            .WaitAndRetryAsync(new[]
                            {
                                TimeSpan.FromSeconds(jitterer.Next(3, 10)),
                                TimeSpan.FromSeconds(jitterer.Next(12, 22)),
                                TimeSpan.FromSeconds(jitterer.Next(21, 31)),
                                TimeSpan.FromSeconds(jitterer.Next(36, 50)),
                                TimeSpan.FromSeconds(jitterer.Next(51, 60))
                            }, onRetry: (outcome, timespan, retryAttempt, context) => 
                            {
                                service.GetService<ILogger<IInstanceMetadataServiceAPI>>().LogWarning("Retrying {httpMethod} to {address} due to {statusCode}. Delaying for {delay}s",
                                    outcome.Result.RequestMessage.Method,
                                    outcome.Result.RequestMessage.RequestUri,
                                    outcome.Result.StatusCode,
                                    timespan.TotalSeconds);
                            }));
                    services.AddSingleton<IAgentOperator, AgentOperator>(sp =>
                    {                        
                        var config = sp.GetService<IConfiguration>().GetSection("drainer").Get<AzureDevopsConfig>();
                        var loggerFactory = sp.GetService<ILoggerFactory>();
                        var hostEnv = sp.GetService<IHostEnvironment>();
                        var logger = loggerFactory.CreateLogger("AgentOperator");

                        var computerName = hostEnv.IsProduction() ? Environment.MachineName.ToUpper() : config.ComputerName.ToUpper();

                        if (string.IsNullOrEmpty(computerName))
                            throw new Exception("A drainer:computername has not been set in configuration");

                        logger.LogInformation("MachineName is {machineName}", computerName);

                        return new AgentOperatorBuilder()
                          .AddLogger(logger)
                          .AddInstance(config.Uri, config.Pat)
                          .SelectAgents(x =>
                          {
                              return x.Where(agent => agent.ComputerName.ToUpper() == computerName.ToUpper());
                          })
                          .Build().Result;
                    });
                });
    }
}


