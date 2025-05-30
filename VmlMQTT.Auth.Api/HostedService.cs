using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using Platfrom.MQTTnet;
using Platfrom.MQTTnet.Models;
using System.Collections.Concurrent;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Auth.Api.Models;

namespace VmlMQTT.Auth.Api
{
    public class HostedService : BackgroundService
    {
        private readonly ILogger<HostedService> _logger;
        private IEmqxBrokerHostRepository _emqxBrokerHostRepository;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;


        public static ConcurrentDictionary<string, List<CommandResponse>> ResponseCommands = new ConcurrentDictionary<string, List<CommandResponse>>();

        public HostedService(ILogger<HostedService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            IServiceScopeFactory scopeFactory)
        {
            this._logger = logger;

            _configuration = configuration;
            _serviceProvider = serviceProvider;

            var scope = scopeFactory.CreateScope();
            _emqxBrokerHostRepository = scope.ServiceProvider.GetRequiredService<IEmqxBrokerHostRepository>();
        }

        private void mQTTSubcribe_HandleMessages(object sender, EventArgs e)
        {
            try
            {
                var msg = (e as MQTTEventMsg);
                if (msg == null)
                {
                    return;
                }

                var cmdreponse = JsonConvert.DeserializeObject<CommandResponse>(msg.Message);
                if (!ResponseCommands.ContainsKey(cmdreponse.deviceImei))
                {
                    ResponseCommands.TryAdd(cmdreponse.deviceImei, new List<CommandResponse>());
                }

                cmdreponse.ExpiredTime = DateTime.Now.AddSeconds(10);
                ResponseCommands[cmdreponse.deviceImei].Add(cmdreponse);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        private static async Task ConnectToMqttBroker(MQTTConfig mQTTConfig)
        {
            Console.WriteLine($"Connecting to MQTT broker at {mQTTConfig.Host}:{mQTTConfig.Port}...");

            var mqttFactory = new MqttFactory();
            var mqttClient = mqttFactory.CreateMqttClient();

            // Configure client options
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(mQTTConfig.Host, mQTTConfig.Port)
                .WithCredentials(mQTTConfig.UserName, mQTTConfig.Password)
                .WithClientId($"notification-service-{Guid.NewGuid()}")
                .WithCleanSession(true)
                .Build();

            // Set up handlers
            mqttClient.DisconnectedAsync += HandleDisconnected;

            // Connect
            await mqttClient.ConnectAsync(options, CancellationToken.None);

            Console.WriteLine("Connected to MQTT broker successfully!");
        }

        private static async Task HandleDisconnected(MqttClientDisconnectedEventArgs args)
        {
            Console.WriteLine("Disconnected from MQTT broker!");

            if (args.Exception != null)
            {
                Console.WriteLine($"Reason: {args.Exception.Message}");
            }

            await Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var emqxBrokerHosts = await _emqxBrokerHostRepository.GetAllAsync();
            emqxBrokerHosts = emqxBrokerHosts.Where(s => s.IsActive).ToList();

            foreach (var broker in emqxBrokerHosts)
            {
                var logger = _serviceProvider.GetRequiredService<ILogger<MQTTSubcribe>>();

                var mQTTSubcribe = new MQTTSubcribe(logger, new MQTTConfig
                {
                    Host = broker.Ip,
                    UserName = _configuration["MQTT:username"],
                    Password = _configuration["MQTT:password"],
                    Port = int.Parse(_configuration["MQTT:port"])
                });
                mQTTSubcribe.HandleMessages += mQTTSubcribe_HandleMessages;

                await mQTTSubcribe.SubcribeMultiTopic(stoppingToken
                , $"vml_command_client_request/{broker.Id}/+");
            }
        }
    }
}
