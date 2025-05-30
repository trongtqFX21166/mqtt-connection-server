using Microsoft.Extensions.Logging;
using MQTTnet.Client;
using MQTTnet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;

namespace VmlMQTT.Application.Services
{
    public class MqttConnectionPool : IMqttConnectionPool
    {
        private readonly ConcurrentDictionary<string, MqttConnection> _connections = new();
        private readonly ILogger<MqttConnectionPool> _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Timer _healthCheckTimer;

        public MqttConnectionPool(ILogger<MqttConnectionPool> logger)
        {
            _logger = logger;
            _healthCheckTimer = new Timer(PerformHealthCheck, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public async Task<IMqttClient> GetConnectionAsync(string host, MqttConnectionConfig config)
        {
            var connection = _connections.GetOrAdd(host, _ => new MqttConnection(host, _logger));

            if (!connection.IsConnected)
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (!connection.IsConnected)
                    {
                        await connection.ConnectAsync(config);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            return connection.Client;
        }

        public async Task ReleaseConnectionAsync(string host)
        {
            if (_connections.TryRemove(host, out var connection))
            {
                await connection.DisconnectAsync();
            }
        }

        public async Task<bool> IsConnectedAsync(string host)
        {
            return _connections.TryGetValue(host, out var connection) && connection.IsConnected;
        }

        private async void PerformHealthCheck(object state)
        {
            var disconnectedHosts = new List<string>();

            foreach (var kvp in _connections)
            {
                if (!kvp.Value.IsConnected)
                {
                    disconnectedHosts.Add(kvp.Key);
                }
            }

            foreach (var host in disconnectedHosts)
            {
                _logger.LogWarning("Removing disconnected MQTT connection for host: {Host}", host);
                if (_connections.TryRemove(host, out var connection))
                {
                    await connection.DisconnectAsync();
                }
            }
        }

        public void Dispose()
        {
            _healthCheckTimer?.Dispose();

            foreach (var connection in _connections.Values)
            {
                connection.DisconnectAsync().GetAwaiter().GetResult();
            }

            _connections.Clear();
            _semaphore?.Dispose();
        }

        private class MqttConnection
        {
            public IMqttClient Client { get; private set; }
            public bool IsConnected => Client?.IsConnected == true;
            private readonly string _host;
            private readonly ILogger _logger;

            public MqttConnection(string host, ILogger logger)
            {
                _host = host;
                _logger = logger;
            }

            public async Task ConnectAsync(MqttConnectionConfig config)
            {
                try
                {
                    Client = new MqttFactory().CreateMqttClient();

                    var options = new MqttClientOptionsBuilder()
                        .WithTcpServer(config.Host, config.Port)
                        .WithCredentials(config.Username, config.Password)
                        .WithClientId(config.ClientId ?? $"command-service-{Environment.MachineName}-{Guid.NewGuid():N}")
                        .WithKeepAlivePeriod(TimeSpan.FromSeconds(config.KeepAliveSeconds))
                        .WithCleanSession(true)
                        .Build();

                    Client.DisconnectedAsync += async args =>
                    {
                        _logger.LogWarning("MQTT client disconnected from {Host}: {Reason}",
                            _host, args.Reason);
                    };

                    await Client.ConnectAsync(options);
                    _logger.LogInformation("Connected to MQTT broker at {Host}", _host);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to MQTT broker at {Host}", _host);
                    throw;
                }
            }

            public async Task DisconnectAsync()
            {
                if (Client?.IsConnected == true)
                {
                    await Client.DisconnectAsync();
                }
                Client?.Dispose();
            }
        }
    }
}
