using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet;
using System.Text;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;
using Newtonsoft.Json;
using VmlMQTT.Application.Models;

namespace VmlMQTT.Auth.Api
{
    public class ResponseHostedService : BackgroundService
    {
        private readonly ILogger<ResponseHostedService> _logger;
        private readonly IEmqxBrokerHostRepository _brokerRepository;
        private readonly IResponseManager _responseManager;
        private readonly IConfiguration _configuration;
        private readonly List<IMqttClient> _clients = new();

        public ResponseHostedService(
            ILogger<ResponseHostedService> logger,
            IEmqxBrokerHostRepository brokerRepository,
            IResponseManager responseManager,
            IConfiguration configuration)
        {
            _logger = logger;
            _brokerRepository = brokerRepository;
            _responseManager = responseManager;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var brokers = await _brokerRepository.GetAllAsync();
            var activeBrokers = brokers.Where(b => b.IsActive).ToList();

            foreach (var broker in activeBrokers)
            {
                try
                {
                    var client = await CreateMqttClient(broker);
                    _clients.Add(client);

                    // Subscribe to response topics
                    var responseTopic = $"vml_command_client_response/{broker.Id}/+";
                    await client.SubscribeAsync(responseTopic, MqttQualityOfServiceLevel.AtLeastOnce);

                    _logger.LogInformation("Subscribed to response topic: {Topic}", responseTopic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to setup MQTT client for broker {BrokerId}", broker.Id);
                }
            }

            // Keep running until cancellation
            await Task.Delay(-1, stoppingToken);
        }

        private async Task<IMqttClient> CreateMqttClient(EmqxBrokerHost broker)
        {
            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(broker.Ip, broker.Port)
                .WithCredentials(
                    _configuration["MQTT:username"],
                    _configuration["MQTT:password"])
                .WithClientId($"response-handler-{broker.Id}-{Environment.MachineName}")
                .WithCleanSession(true)
                .Build();

            client.ApplicationMessageReceivedAsync += HandleResponseMessage;
            client.DisconnectedAsync += args =>
            {
                _logger.LogWarning("Response handler disconnected from broker {BrokerId}: {Reason}",
                    broker.Id, args.Reason);
                return Task.CompletedTask;
            };

            await client.ConnectAsync(options);
            return client;
        }

        private Task HandleResponseMessage(MqttApplicationMessageReceivedEventArgs args)
        {
            try
            {
                var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
                var response = JsonConvert.DeserializeObject<CommandResponse>(payload);

                if (response != null && !string.IsNullOrEmpty(response.RequestId))
                {
                    response.ReceivedAt = DateTime.UtcNow;
                    _responseManager.RegisterResponse(response);

                    _logger.LogDebug("Registered response for request {RequestId} with code {Code}",
                        response.RequestId, response.Code);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing response message from topic {Topic}",
                    args.ApplicationMessage.Topic);
            }

            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var client in _clients)
            {
                try
                {
                    if (client.IsConnected)
                    {
                        await client.DisconnectAsync();
                    }
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing MQTT client");
                }
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
