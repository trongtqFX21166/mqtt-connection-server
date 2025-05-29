using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Platform.KafkaClient;
using VmlMQTT.HandleVmlEvent.Service.Model;

namespace VmlMQTT.HandleVmlEvent.Service
{
    public class HostedService : BackgroundService
    {
        private ILogger<HostedService> _logger;
        private IConsumer _eventConsumer;
        private readonly HttpClient _client;

        public HostedService(ILogger<HostedService> logger
            , IConsumer eventConsumer,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;

            _eventConsumer = eventConsumer;
            _eventConsumer.Consume += eventConsumer_Consume;
            _client = clientFactory.CreateClient("VmlMQTTAuthApi");
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
                var eventModel = JsonConvert.DeserializeObject<VMLEventModel>(consumeResult.Value);

                if (eventModel.Type != "MqttAuth")
                {
                    return;
                }

                if (eventModel.Event == "Login")
                {

                    var eEvent = eventModel.Datas[0].ToObject<string>();

                    await ReleaseSessionAsync(eEvent);

                    return;
                }

                if (eventModel.Event == "RefreshTokens")
                {
                    var eEvent = eventModel.Datas[0].ToObject<UpdateSessionDto>();

                    await UpdateSessionAsync(eEvent);

                    return;
                }    
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task ReleaseSessionAsync(string refreshToken)
        {
            var httpResponse = await _client.PostAsync("/api/MqttAuth/release-session",
                new StringContent(JsonConvert.SerializeObject(new
                {
                    userName = refreshToken
                }), System.Text.Encoding.UTF8, "application/json"));

            var content = await httpResponse.Content.ReadAsStringAsync();

            var data = JsonConvert.DeserializeObject<IOTHubResponse<object>>(content);

            _logger.LogInformation($"ReleaseSession refreshToken = {refreshToken} || code = {data.Code} || msg = {data.Msg}");
        }

        public async Task UpdateSessionAsync(UpdateSessionDto request)
        {
            var httpResponse = await _client.PostAsync("/api/MqttAuth/update-session",
                new StringContent(JsonConvert.SerializeObject(new
                {
                    userName = request.RefreshToken,
                    accessToken = request.AccessToken
                }), System.Text.Encoding.UTF8, "application/json"));

            var content = await httpResponse.Content.ReadAsStringAsync();

            var data = JsonConvert.DeserializeObject<IOTHubResponse<object>>(content);

            _logger.LogInformation($"UpdateSession refreshToken = {request.RefreshToken} || code = {data.Code} || msg = {data.Msg}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.WhenAll(Task.Factory.StartNew(() => _eventConsumer.RegisterConsume(stoppingToken)));
        }
    }
}
