using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.Protocol;
using MQTTnet;
using Newtonsoft.Json;
using Platform.KafkaClient;
using Platfrom.MQTTnet;
using Platfrom.MQTTnet.Models;
using VmlMQTT.ConsumerNotifyMessage.Service.Models;
using VmlMQTT.ConsumerNotifyMessage.Service.Protos;
using VmlMQTT.Core.Interfaces.Repositories;
using MQTTnet.Client;
using VmlMQTT.ConsumerNotifyMessage.Service.Repositories;

namespace VmlMQTT.ConsumerNotifyMessage.Service
{
    public class HostedService : BackgroundService
    {
        private ILogger<HostedService> _logger;
        private IConsumer _eventConsumer;
        private IUserSessionRepository _userSessionRepository;
        private IUserRepository _userRepository;
        private IEmqxBrokerHostRepository _emqxBrokerHostRepository;
        private readonly IConfiguration _configuration;
        private static IMqttClient _mqttClient;
        private static MqttFactory _mqttFactory;
        private readonly INotificationRepo _notificationRepo;



        public HostedService(ILogger<HostedService> logger
            , IConsumer eventConsumer,
            IUserSessionRepository userSessionRepository,
            IUserRepository userRepository,
            IEmqxBrokerHostRepository emqxBrokerHostRepository,
            IConfiguration configuration,
            INotificationRepo notificationRepo)
        {
            _logger = logger;

            _eventConsumer = eventConsumer;
            _eventConsumer.Consume += eventConsumer_Consume;
            _userSessionRepository = userSessionRepository;
            _userRepository = userRepository;
            _emqxBrokerHostRepository = emqxBrokerHostRepository;
            _configuration = configuration;
            _notificationRepo = notificationRepo;
        }


        private async void eventConsumer_Consume(Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, string> consumeResult)
        {
            if (string.IsNullOrWhiteSpace(consumeResult.Value))
            {
                return;
            }

            _logger.LogInformation(consumeResult.Value);

            try
            {
                var eventModel = JsonConvert.DeserializeObject<NotificationDto>(consumeResult.Value);

                var notification = new NotificationMessage
                {
                    Header = new HeaderNotification
                    {
                        Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
                        MessageType = eventModel.MessageType
                    },
                    Body = new BodyNotification
                    {
                        Body = eventModel.Body,
                        ImageUrl = eventModel.ImageUrl ?? string.Empty,
                        Title = eventModel.Title
                    }
                };

                int? userId = null;

                //send all user
                if (eventModel.To == "*")
                {
                    var emqxBrokerHosts = await _emqxBrokerHostRepository.GetAllAsync();
                    emqxBrokerHosts = emqxBrokerHosts.Where(s => s.IsActive).ToList();

                    foreach (var broker in emqxBrokerHosts)
                    {
                        await ConnectToMqttBroker(new MQTTConfig
                        {
                            Host = broker.Ip,
                            UserName = _configuration["MQTT:username"],
                            Password = _configuration["MQTT:password"],
                            Port = int.Parse(_configuration["MQTT:port"])
                        });

                        await PublishNotification($"vml_notify/{broker.Id}/all", notification);
                    }

                    _logger.LogInformation("Successfully send all user");
                }
                else
                {
                    var user = await _userRepository.GetByPhoneAsync(eventModel.To);

                    if (user == null)
                    {
                        _logger.LogWarning("user {user} not found", user);

                        return;
                    }

                    userId = user.VMLUserId;

                    var userSessions = await _userSessionRepository.GetAllByUserIdAsync(user.VMLUserId);

                    if (userSessions?.Count > 0)
                    {
                        foreach (var item in userSessions)
                        {
                            await ConnectToMqttBroker(new MQTTConfig
                            {
                                Host = item.Host,
                                UserName = _configuration["MQTT:username"],
                                Password = _configuration["MQTT:password"],
                                Port = int.Parse(_configuration["MQTT:port"])
                            });

                            await PublishNotification(item.SubTopics[0], notification);
                        }

                        _logger.LogInformation($"Successfully send to user {user.Phone}");
                    }
                }

                await CreateNotificationAsync(new NotificationDto
                {
                    To = eventModel.To,
                    Title = eventModel.Title,
                    Body = eventModel.Body,
                    ActionUrl = eventModel.ActionUrl,
                    Icon = eventModel.Icon,
                    ImageUrl = eventModel.ImageUrl,
                    MessageType = eventModel.MessageType
                }, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task CreateNotificationAsync(NotificationDto request, long? userId)
        {
            await _notificationRepo.AddAsync(new Entities.Notification
            {
                Type = request.MessageType,
                Title = request.Title,
                Content = request.Body,
                UserId = userId,
                Icon = !string.IsNullOrEmpty(request.Icon) ? request.Icon : "https://api.vietmap.io/vml/share/images/notification-icons/icons-01.png",
                ActionURL = request.ActionUrl,
                CreatedDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.WhenAll(Task.Factory.StartNew(() => _eventConsumer.RegisterConsume(stoppingToken)));
        }

        private static async Task ConnectToMqttBroker(MQTTConfig mQTTConfig)
        {
            Console.WriteLine($"Connecting to MQTT broker at {mQTTConfig.Host}:{mQTTConfig.Port}...");

            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();

            // Configure client options
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(mQTTConfig.Host, mQTTConfig.Port)
                .WithCredentials(mQTTConfig.UserName, mQTTConfig.Password)
                .WithClientId($"notification-service-{Guid.NewGuid()}")
                .WithCleanSession(true)
                .Build();

            // Set up handlers
            _mqttClient.DisconnectedAsync += HandleDisconnected;

            // Connect
            await _mqttClient.ConnectAsync(options, CancellationToken.None);

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

        private static async Task PublishNotification(string topic, NotificationMessage notification)
        {
            if (_mqttClient?.IsConnected != true)
            {
                Console.WriteLine("Not connected to MQTT broker!");
                return;
            }

            try
            {
                //string jsonPayload = JsonSerializer.Serialize(notification);

                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(notification.ToByteArray())
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
                Console.WriteLine($"Notification sent to topic: {topic}");
                // Console.WriteLine($"Payload: {jsonPayload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending notification: {ex.Message}");
            }
        }
    }
}
