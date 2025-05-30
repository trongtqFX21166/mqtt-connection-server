using Microsoft.AspNetCore.Mvc;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;
using VmlMQTT.Auth.Api.Models;

namespace VmlMQTT.Auth.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommandController : ControllerBase
    {
        private readonly ICommandService _commandService;
        private readonly ILogger<CommandController> _logger;

        public CommandController(ICommandService commandService, ILogger<CommandController> logger)
        {
            _commandService = commandService;
            _logger = logger;
        }

        [HttpPost("send")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IOTHubResponse<CommandResult>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(IOTHubResponse<string>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(IOTHubResponse<string>))]
        public async Task<IActionResult> SendCommand([FromBody] SendCommandRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Generate RequestId if not provided
                if (string.IsNullOrEmpty(request.RequestId))
                {
                    request.RequestId = Guid.NewGuid().ToString("N");
                }

                _logger.LogInformation("Received command request {RequestId} for device {DeviceId}",
                    request.RequestId, request.DeviceId);

                var result = await _commandService.SendCommandAsync(request, cancellationToken);

                var response = new IOTHubResponse<CommandResult>
                {
                    Code = result.Success ? 200 : result.Code,
                    Msg = result.Success ? "Success" : result.Message,
                    Data = result
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in SendCommand");

                return StatusCode(500, new IOTHubResponse<string>
                {
                    Code = 500,
                    Msg = "Internal server error",
                    Data = null
                });
            }
        }

        [HttpGet("status/{requestId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCommandStatus(string requestId)
        {
            // This could be extended to check command status
            // For now, just return that the endpoint exists
            return Ok(new { RequestId = requestId, Status = "Completed or Expired" });
        }
    }
}
