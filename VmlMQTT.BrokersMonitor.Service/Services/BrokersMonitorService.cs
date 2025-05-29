using Newtonsoft.Json;
using Platform.KafkaClient;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;

namespace VmlMQTT.BrokersMonitoring.Service.Services
{
    internal sealed class BrokersMonitorService : IBrokersMonitorService
    {
        private static readonly string[] KEY_MONITOR = ["Connections", "Subscriptions", "Topics", "LiveConnections"];

        private readonly IProducer _producer;
        private readonly IEmqxBrokerHostRepository _brokerHostRepository;
        private readonly IEmqxBrokerService _emqxBrokerService;

        public BrokersMonitorService(IProducer producer, IEmqxBrokerHostRepository brokerHostRepository, IEmqxBrokerService emqxBrokerService)
        {
            _producer = producer;
            _brokerHostRepository = brokerHostRepository;
            _emqxBrokerService = emqxBrokerService;
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            // Logic to run the service
            var brokers = await _brokerHostRepository.GetAllAsync();
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var broker in brokers)
            {
                _ = MonitorInfo(broker, currentTime);
            }
        }

        public async Task MonitorInfo(EmqxBrokerHost brokerHost, long timeStamp)
        {
            var monitor = (await _emqxBrokerService.GetMqttMonitorAsync(brokerHost, 19)).OrderByDescending(x => x.TimeStamp).FirstOrDefault();
            var getDict = monitor?.GetDictionaryByListKey(KEY_MONITOR);

            if (getDict is null)
            {
                var message = new VmlBrokerLog
                {
                    SystemTimstamp = timeStamp,
                    BrokerId = brokerHost.Id,
                    BrokerIp = brokerHost.Ip,
                    Category = "monitor.checkfailed",
                    TimeStamp = monitor?.TimeStamp ?? 0,
                    Data = "0"
                };
                await PushMonitorEvent(message);
            }
            else
            {
                foreach (var element in getDict)
                {
                    var message = new VmlBrokerLog
                    {
                        SystemTimstamp = timeStamp,
                        BrokerId = brokerHost.Id,
                        BrokerIp = brokerHost.Ip,
                        Category = $"monitor.{element.Key.ToLower()}",
                        TimeStamp = monitor?.TimeStamp ?? 0,
                        Data = $"{element.Value}"
                    };
                    await PushMonitorEvent(message);
                }
            }
        }

        private async Task PushMonitorEvent(VmlBrokerLog brokerLog)
        {
            await _producer.ProduceMessageAsync(JsonConvert.SerializeObject(brokerLog));
        }
    }
}
