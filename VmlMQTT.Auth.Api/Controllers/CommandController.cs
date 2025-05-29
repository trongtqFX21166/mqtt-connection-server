using Microsoft.AspNetCore.Mvc;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Auth.Api.Models;

namespace VmlMQTT.Auth.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommandController : ControllerBase
    {
        private readonly IVmMQTTCommandService _vmMQTTCommandService;
        public CommandController(IVmMQTTCommandService vmMQTTCommandService)
        {
            _vmMQTTCommandService = vmMQTTCommandService;
        }

        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IOTHubResponse<string>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(IOTHubResponse<string>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(IOTHubResponse<string>))]
        [HttpPost("Send")]
        public async Task<IActionResult> SendCommand(SendCommandRequest request)
        {
            var resultCmd = await _vmMQTTCommandService.SendCommand(new Application.Models.SendCommandRequest
            {
                SessionId = request.SessionId,
                ResponseCommands = HostedService.ResponseCommands,
                DeviceId = request.DeviceId,
                Phone = request.Phone,
                RequestId = request.RequestId
            });
            return Ok(resultCmd);
        }
    }
}
