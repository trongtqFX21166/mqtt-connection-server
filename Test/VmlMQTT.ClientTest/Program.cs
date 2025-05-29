using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace MqttTestClient
{
    public class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static IMqttClient _mqttClient;
        private static MqttFactory _mqttFactory;

        static async Task Main(string[] args)
        {
            Console.WriteLine("MQTT Test Client Starting...");
            Console.WriteLine("=============================");

            try
            {
                // Step 1: Call the API to start a session
                var sessionInfo = await StartMqttSession();
                if (sessionInfo == null)
                {
                    Console.WriteLine("Failed to start MQTT session. Exiting.");
                    return;
                }

                
                Console.WriteLine($"Broker Host: {sessionInfo.Host}");
                Console.WriteLine("Subscribe Topics:");
                foreach (var topic in sessionInfo.SubTopics)
                {
                    Console.WriteLine($"  - {topic}");
                }
                Console.WriteLine("Publish Topics:");
                foreach (var topic in sessionInfo.PubTopics)
                {
                    Console.WriteLine($"  - {topic}");
                }

                // Step 2: Connect to the MQTT broker
                await ConnectToMqttBroker(sessionInfo);

                // Step 3: Keep the application running and allow user to send test messages
                await RunInteractiveMode(sessionInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task<SessionInfo> StartMqttSession()
        {
            Console.WriteLine("Calling API to start MQTT session...");

            // API endpoint
            string apiUrl = "http://192.168.11.21:31236/api/MqttAuth/start-session";

            // Create request payload
            var requestData = new
            {
                phone = "933740889",
                userId = 15329,
                deviceInfo = "iphone",
                accessToken = "dcc8a00b97b2061c834b0635a3febda589d872d70a0a0d4746d333d4e9f362a3",
                refreshToken = "ea48b94fe7cabfe5c661943e348092d57ba207f95c5e4035842e0c5a0461c4a2"
            };

            try
            {
                // Send the request
                var response = await _httpClient.PostAsJsonAsync(apiUrl, requestData);
                response.EnsureSuccessStatusCode();

                // Parse the response
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API Response: {responseContent}");

                var apiResponse = JsonSerializer.Deserialize<ApiResponse<SessionInfo>>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apiResponse.Code != 200)
                {
                    Console.WriteLine($"API returned error: {apiResponse.Msg}");
                    return null;
                }

                apiResponse.Data.RefreshToken = requestData.refreshToken;
                return apiResponse.Data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling API: {ex.Message}");
                return null;
            }
        }

        private static async Task ConnectToMqttBroker(SessionInfo sessionInfo)
        {
            Console.WriteLine("Connecting to MQTT broker...");

            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();

            // Configure client options
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(sessionInfo.Host, 1883) // Default MQTT port
                .WithCredentials(sessionInfo.RefreshToken, sessionInfo.accessToken)
                .WithClientId($"test-client-{Guid.NewGuid()}")
                .WithCleanSession(true)
                .Build();

            // Set up handlers
            _mqttClient.ApplicationMessageReceivedAsync += HandleReceivedMessage;
            _mqttClient.DisconnectedAsync += HandleDisconnected;

            // Connect
            await _mqttClient.ConnectAsync(options, CancellationToken.None);

            Console.WriteLine("Connected to MQTT broker successfully!");

            // Subscribe to topics
            foreach (var topic in sessionInfo.SubTopics)
            {
                await _mqttClient.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce);
                Console.WriteLine($"Subscribed to topic: {topic}");
            }
        }

        private static Task HandleReceivedMessage(MqttApplicationMessageReceivedEventArgs args)
        {
            string payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);

            Console.WriteLine("");
            Console.WriteLine($"Received message on topic: {args.ApplicationMessage.Topic}");
            Console.WriteLine($"Payload: {payload}");
            Console.WriteLine("");
            Console.Write("Command (send/exit): ");

            return Task.CompletedTask;
        }

        private static async Task HandleDisconnected(MqttClientDisconnectedEventArgs args)
        {
            Console.WriteLine("Disconnected from MQTT broker!");

            if (args.Exception != null)
            {
                Console.WriteLine($"Reason: {args.Exception.Message}");
            }

            // You can implement reconnect logic here if needed
            await Task.CompletedTask;
        }

        private static async Task RunInteractiveMode(SessionInfo sessionInfo)
        {
            bool exitRequested = false;

            while (!exitRequested)
            {
                Console.WriteLine("");
                Console.Write("Command (send/exit): ");
                string command = Console.ReadLine()?.ToLower();

                switch (command)
                {
                    case "send":
                        if (sessionInfo.PubTopics.Count > 0)
                        {
                            string topic = sessionInfo.PubTopics[0]; // Use the first publish topic

                            Console.Write("Enter message to send: ");
                            string message = Console.ReadLine() ?? "";

                            await PublishMessage(topic, message);
                        }
                        else
                        {
                            Console.WriteLine("No publish topics available.");
                        }
                        break;

                    case "exit":
                        exitRequested = true;
                        break;

                    default:
                        Console.WriteLine("Unknown command. Available commands: send, exit");
                        break;
                }
            }

            // Disconnect and clean up
            if (_mqttClient?.IsConnected == true)
            {
                await _mqttClient.DisconnectAsync();
                Console.WriteLine("Disconnected from MQTT broker.");
            }
        }

        private static async Task PublishMessage(string topic, string message)
        {
            if (_mqttClient?.IsConnected != true)
            {
                Console.WriteLine("Not connected to MQTT broker!");
                return;
            }

            try
            {
                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(message)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
                Console.WriteLine($"Message sent to topic: {topic}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }
    }

    // Model classes to match the API response
    public class ApiResponse<T>
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public T Data { get; set; }
    }

    public class SessionInfo
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string accessToken { get; set; }
        public List<string> PubTopics { get; set; } = new List<string>();
        public List<string> SubTopics { get; set; } = new List<string>();

        public string RefreshToken { get; set; }
    }
}