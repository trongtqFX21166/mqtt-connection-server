using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using VmlMQTT.Application.DTOs;
using VmlMQTT.Application.Interfaces;

namespace VmlMQTT.Webhook.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MqttWebhookController : ControllerBase
    {
        private readonly ILogger<MqttWebhookController> _logger;
        private readonly IWebHookService _webHookService;

        public MqttWebhookController(ILogger<MqttWebhookController> logger, IWebHookService webHookService)
        {
            _logger = logger;
            _webHookService = webHookService;
        }

        [HttpPost("events")]
        public Task<IActionResult> ReceiveEvent([FromBody] ReceiveMqttEvent data)
        {
            _logger.LogInformation("Received event {Event} from MQTT broker {Data}", data.Event, JsonSerializer.Serialize(data));

            _webHookService.ReceiveMqttEventHandler(data);

            return Task.FromResult<IActionResult>(Ok(new
            {
                Code = 200,
                Msg = "Success"
            }));
        }
    }
}
