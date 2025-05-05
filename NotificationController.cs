using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Platform.KafkaClient;
using VmlMQTT.Auth.Api.Models;

namespace VmlMQTT.Auth.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly IProducer _producer;

        public NotificationController(IEnumerable<IProducer> producers)
        {
            _producer = producers.FirstOrDefault(p => p.Name == "Producer");
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] NotificationRequest request)
        {
            await _producer.ProduceMessageAsync(JsonConvert.SerializeObject(new NotificationDto()
            {
                Title = request.Title,
                Body = request.Body,
                ImageUrl = request.ImageUrl,
                To = request.To,
                MessageType = request.MessageType
            }));

            return Ok(new IOTHubResponse<object>
            {
                Code = 200,
                Msg = "Success"
            });
        }
    }
}
