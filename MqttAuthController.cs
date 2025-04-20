using Microsoft.AspNetCore.Mvc;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Auth.Api.Models;

namespace VmlMQTT.Auth.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MqttAuthController : ControllerBase
    {
        private readonly ILogger<MqttAuthController> _logger;
        private readonly IMqttAuthService _mqttAuthService;

        public MqttAuthController(
            ILogger<MqttAuthController> logger,
            IMqttAuthService mqttAuthService)
        {
            _logger = logger;
            _mqttAuthService = mqttAuthService;
        }

        [HttpPost("start-session")]
        public async Task<IActionResult> StartSession([FromBody] MqttStartSessionRequest request)
        {
            // todo:
           //1. Check userId not existed, create new user

           //2. Get randoom MQTT Broker Host

           //3. Generate UserSession with Host Info

           //4. Call MQTT Broker Host Api
           //4.1 Create Account
           //4.2 Assign Roles

            return Ok(new IOTHubResponse<MqttStartSessionResponse>
            {

            });
        }

        [HttpPost("release-session")]
        public async Task<IActionResult> ReleaseSession([FromBody] ReleaseSessionRequest request)
        {
            //todo:
            //1. Check session with refreshToken

            //2. Call MQTT Broker Host Api
            //2.1 delete account
            //2.2 remove roles

            //3. update UserSession to IsActive = false 

            return Ok(new IOTHubResponse<string>
            {

            });
        }
    }
}
