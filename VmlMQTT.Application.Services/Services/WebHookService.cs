using Newtonsoft.Json;
using Platform.KafkaClient;
using System.Collections;
using VmlMQTT.Application.DTOs;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;

namespace VmlMQTT.Application.Services
{
    public sealed class WebHookService : IWebHookService
    {
        private readonly IProducer _producer;

        public WebHookService(IEnumerable<IProducer> producers)
        {
            _producer = producers.FirstOrDefault(p => p.Name == "Producer") ?? throw new ArgumentException("Producer not found");
        }

        public void ReceiveMqttEventHandler(ReceiveMqttEvent body)
        {
            Hashtable data = new()
            {
                { "Reason", body.Reason },
            };

            var message = new VmlBrokerLog
            {
                Category = body.Event,
                TimeStamp = body.TimeStamp,
                BrokerIp = body.BrokerIp,
                ClientIp = body.Peername,
                ClientId = body.ClientId,
                Username = body.Username,
                Data = JsonConvert.SerializeObject(data)
            };

            _producer.ProduceMessageAsync(JsonConvert.SerializeObject(message)).Wait();
        }
    }
}
