using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.KafkaClient.Models;
using Platform.KafkaClient;
using Platform.Serilog;

namespace VmlMQTT.HandleVmlEvent.Service
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            host.Run();
        }


        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddHostedService<HostedService>();
                    services.Configure<ConsumerConfig>("Consumer",
                        context.Configuration.GetSection("Consumer"));
                    services.AddSingleton<IConsumer>(s =>
                    {
                        var settings = s.GetRequiredService<IOptionsMonitor<ConsumerConfig>>().Get("Consumer");
                        var logger = s.GetRequiredService<ILogger<Consumer>>();

                        return new Consumer(logger, settings);
                    });
                    services.AddHttpClient("VmlMQTTAuthApi", httpClient =>
                    {
                        httpClient.BaseAddress = new Uri($"{context.Configuration["VmlMQTTAuthApi"]}");
                    });
                }).RegisterSerilogConfig();
    }
}
