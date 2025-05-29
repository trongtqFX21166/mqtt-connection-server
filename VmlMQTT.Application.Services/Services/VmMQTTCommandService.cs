using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using Platfrom.MQTTnet;
using Platfrom.MQTTnet.Models;
using System.Collections.Concurrent;
using System.Xml;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;
using VmlMQTT.Core.Interfaces.Repositories;

namespace VmlMQTT.Application.Services
{
    public class VmMQTTCommandService : IVmMQTTCommandService
    {
        private readonly ILogger<VmMQTTCommandService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IUserSessionRepository _userSessionRepository;
        private static IMqttClient _mqttClient;
        private static MqttFactory _mqttFactory;
        private readonly IConfiguration _configuration;



        // private readonly IEMQXBrokerApi _emQXBrokerApi;

        public VmMQTTCommandService(ILogger<VmMQTTCommandService> logger
            , IUserRepository userRepository
            , IUserSessionRepository userSessionRepository
, IConfiguration configuration
/*, IEMQXBrokerApi emQXBrokerApi*/)
        {
            _logger = logger;
            _userRepository = userRepository;
            _userSessionRepository = userSessionRepository;
            _configuration = configuration;
            //_emQXBrokerApi = emQXBrokerApi;
        }

        private static async Task PublishNotification(string topic, string msg)
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
                    .WithPayload(msg)
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

        private static async Task HandleDisconnected(MqttClientDisconnectedEventArgs args)
        {
            Console.WriteLine("Disconnected from MQTT broker!");

            if (args.Exception != null)
            {
                Console.WriteLine($"Reason: {args.Exception.Message}");
            }

            await Task.CompletedTask;
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

        public async Task<IOTHubResponse<string>> SendCommand(SendCommandRequest request)
        {
            var user = await _userRepository.GetByPhoneAsync(request.Phone);

            if(user == null)
            {
                return new IOTHubResponse<string>
                {
                    Code = 404,
                    Msg = "User not found"
                };
            }

            var userSession = await _userSessionRepository.GetByUserIdAndDeviceIdAsync(user.VMLUserId, request.DeviceId);

            if (userSession == null)
            {
                return new IOTHubResponse<string>
                {
                    Code = 404,
                    Msg = "Session not found"
                };
            }

            await ConnectToMqttBroker(new MQTTConfig
            {
                Host = userSession.Host,
                UserName = _configuration["MQTT:username"],
                Password = _configuration["MQTT:password"],
                Port = int.Parse(_configuration["MQTT:port"])
            });

            return await SendMsg(request.SessionId,
                request.RequestId,
                $"vml_command_client_request/2/{userSession.UniqueId}",
                JsonConvert.SerializeObject(new CommandRequest
                {
                    RequestId = request.RequestId,
                    SessionId = request.SessionId
                }),
                request.ResponseCommands);
        }

        private async Task<IOTHubResponse<string>> SendMsg(string sessionId,
            string requestId,
            string topic,
            string msg,
            ConcurrentDictionary<string, List<CommandResponse>> responseCommands)
        {
            try
            {
                await PublishNotification(topic, msg);

                int count = 1;
                do
                {
                    await Task.Delay(100); // await 100ms

                    if (!responseCommands.ContainsKey(sessionId))
                    {
                        responseCommands.TryAdd(sessionId, new List<CommandResponse>());
                    }

                    if (responseCommands[sessionId].Any(x => x.requestId == requestId))
                    {
                        var rsp = responseCommands[sessionId].FirstOrDefault(x => x.requestId == requestId);

                        if (rsp.code != 100)
                        {
                            _logger.LogDebug("{sessionId} send command::{requestId} response error {response}", sessionId, requestId, JsonConvert.SerializeObject(new
                            {
                                _code = rsp.code,
                                _msg = rsp.msg,
                                _sessionId = sessionId,
                                _requestId = requestId
                            }, Newtonsoft.Json.Formatting.None));
                        }

                        return new IOTHubResponse<string>
                        {
                            Code = 0,
                            Msg = rsp.msg,
                            Data = JsonConvert.SerializeObject(new
                            {
                                _code = rsp.code,
                                _msg = rsp.msg,
                                _sessionId = sessionId,
                                _requestId = requestId
                            }),
                        };
                    }

                    if (count > 300) // wait for 30s
                    {
                        break;
                    }

                    count++;
                    continue;

                } while (true);


                _logger.LogInformation($"{sessionId} send command timeout");

                return new IOTHubResponse<string>
                {
                    Code = 600,
                    Msg = "Request timeout",
                    Data = JsonConvert.SerializeObject(new
                    {
                        _code = 600,
                        _msg = "Request timeout",
                        _sessionId = sessionId,
                        _requestId = requestId
                    }),
                };
            }
            catch (Exception ex)
            {
                return new IOTHubResponse<string>
                {
                    Code = 500,
                    Msg = ex.Message,
                    Data = JsonConvert.SerializeObject(new
                    {
                        _code = 500,
                        _msg = ex.Message,
                        _sessionId = sessionId,
                        _requestId = requestId
                    }),
                };

            }

        }
    }
}
