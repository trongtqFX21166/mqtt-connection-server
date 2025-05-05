using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.KafkaClient.Models;
using Platform.KafkaClient;
using Platform.Serilog;
using VmlMQTT.Infratructure.Data;
using Microsoft.EntityFrameworkCore;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Infratructure.Repositories;
using Platfrom.MQTTnet;

namespace VmlMQTT.ConsumerNotifyMessage.Service
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

                    services.AddDbContext<VmlMQTTDbContext>(options =>
    options.UseNpgsql(context.Configuration["ConnectionStrings:DefaultConnection"]),
    ServiceLifetime.Singleton,
    ServiceLifetime.Singleton);

                    services.AddSingleton<IMQTTPublish, MQTTPublish>();

                    services.AddSingleton<IUserSessionRepository, UserSessionRepository>();
                    services.AddSingleton<IUserRepository, UserRepository>();
                    services.AddSingleton<IEmqxBrokerHostRepository, EmqxBrokerHostRepository>();
                }).RegisterSerilogConfig();
    }
}
