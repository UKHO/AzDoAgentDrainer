using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AzDoAgentDrainer.CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var tfs = new Uri(args[0]);
            var tfsPat = args[1];

            var azdo = new Uri(args[2]);
            var azdoPat = args[3];

            var computerName = args[4];            

            var agentsContext = await new AgentContextBuilder()
                                    .AddLogger(GetLogger())
                                    .AddServer(tfs, tfsPat)
                                    //.WherePoolId(2)
                                    .AddServer(azdo, azdoPat)
                                    .WhereComputerName(computerName)
                                    .Build();            

            Console.WriteLine("Agent Context Built");

            await agentsContext.Drain();
            Console.WriteLine("All Jobs Finished again");
            await agentsContext.Enable();
        }

        private static Microsoft.Extensions.Logging.ILogger GetLogger()
        {
            var providers = new LoggerProviderCollection();

            Log.Logger = new LoggerConfiguration()
              .MinimumLevel.Debug()
              .WriteTo.Console()
              .WriteTo.Providers(providers)
              .CreateLogger();

            var services = new ServiceCollection();

            services.AddSingleton(providers);
            services.AddSingleton<ILoggerFactory>(sc =>
            {
                var providerCollection = sc.GetService<LoggerProviderCollection>();
                var factory = new SerilogLoggerFactory(null, true, providerCollection);

                foreach (var provider in sc.GetServices<ILoggerProvider>())
                    factory.AddProvider(provider);

                return factory;
            });

            services.AddLogging(l => l.AddConsole());

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();

            return logger;
        }
    }
}
