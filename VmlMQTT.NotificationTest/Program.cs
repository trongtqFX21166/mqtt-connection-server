using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace VmlMQTT.NotificationTest
{
    class Program
    {
        private static IMqttClient _mqttClient;
        private static MqttFactory _mqttFactory;
        private static string _brokerHost = "192.168.8.164"; // Replace with actual broker host
        private static int _brokerPort = 1883;
        private static string _username = "trongtest";
        private static string _password = "123456";
        private static string _notifyTopicBase = "vml_notify/1/";

        static async Task Main(string[] args)
        {
            Console.WriteLine("MQTT Notification Test Service");
            Console.WriteLine("==============================");


            try
            {
                await ConnectToMqttBroker();
                await RunInteractiveMode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                if (_mqttClient?.IsConnected == true)
                {
                    await _mqttClient.DisconnectAsync();
                    Console.WriteLine("Disconnected from MQTT broker.");
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task ConnectToMqttBroker()
        {
            Console.WriteLine($"Connecting to MQTT broker at {_brokerHost}:{_brokerPort}...");

            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();

            // Configure client options
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerHost, _brokerPort)
                .WithCredentials(_username, _password)
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

        private static async Task RunInteractiveMode()
        {
            bool exitRequested = false;

            while (!exitRequested && _mqttClient.IsConnected)
            {
                Console.WriteLine("\nCommands:");
                Console.WriteLine("1. Send notification to all users");
                Console.WriteLine("2. Send notification to specific user");
                Console.WriteLine("3. Exit");
                Console.Write("\nSelect an option (1-3): ");

                string command = Console.ReadLine()?.Trim();

                switch (command)
                {
                    case "1":
                        await SendNotificationToAll();
                        break;

                    case "2":
                        await SendNotificationToUser();
                        break;

                    case "3":
                        exitRequested = true;
                        break;

                    default:
                        Console.WriteLine("Invalid option. Please select 1-3.");
                        break;
                }
            }
        }

        private static async Task SendNotificationToAll()
        {
            Console.Write("Enter notification message: ");
            string message = Console.ReadLine() ?? "Hello World";

            var notification = new NotificationMessage
            {
                To = "*",
                MessageType = "Notify",
                Message = message
            };

            string topic = _notifyTopicBase + "all";
            await PublishNotification(topic, notification);
        }

        private static async Task SendNotificationToUser()
        {
            Console.Write("Enter user ID: ");
            string sessionId = Console.ReadLine() ?? "1";

            Console.Write("Enter notification message: ");
            string message = Console.ReadLine() ?? "Hello World";

            var notification = new NotificationMessage
            {
                To = sessionId,
                MessageType = "Notify",
                Message = message
            };

            string topic = _notifyTopicBase + sessionId;
            await PublishNotification(topic, notification);
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
                string jsonPayload = JsonSerializer.Serialize(notification);

                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(jsonPayload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
                Console.WriteLine($"Notification sent to topic: {topic}");
                Console.WriteLine($"Payload: {jsonPayload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending notification: {ex.Message}");
            }
        }
    }

    class NotificationMessage
    {
        public string To { get; set; }
        public string MessageType { get; set; }
        public string Message { get; set; }
    }
}