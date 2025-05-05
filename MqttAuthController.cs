using Microsoft.AspNetCore.Mvc;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Auth.Api.Models;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Auth.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MqttAuthController : ControllerBase
    {
        private readonly ILogger<MqttAuthController> _logger;
        private readonly IMqttAuthService _mqttAuthService;
        private readonly IUserRepository _userRepository;

        public MqttAuthController(
            ILogger<MqttAuthController> logger,
            IMqttAuthService mqttAuthService,
            IUserRepository userRepository)
        {
            _logger = logger;
            _mqttAuthService = mqttAuthService;
            _userRepository = userRepository;
        }

        [HttpPost("start-session")]
        public async Task<IActionResult> StartSession([FromBody] MqttStartSessionRequest request)
        {
            _logger.LogInformation("Starting MQTT session for user {UserId}", request.UserId);

            // 1. Check if user exists, create if not
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                // Create new user entity
                user = new VmlMQTT.Core.Entities.User
                {
                    VMLUserId = request.UserId,
                    Phone = request.Phone 
                };

                await _userRepository.AddAsync(user);
                _logger.LogInformation("Created new user {UserId}", request.UserId);
            }

            // Add device ID if provided
            if (!string.IsNullOrEmpty(request.DeviceInfo))
            {
                await _userRepository.AddDeviceIdAsync(request.UserId, request.DeviceInfo);
            }

            // 2 & 3 & 4. Start the session (handled by MqttAuthService)
            var sessionInfo = await _mqttAuthService.StartSessionAsync(new Application.DTOs.MqttStartSessionRequest
            {
                RefreshToken = request.RefreshToken,
                DeviceInfo = request.DeviceInfo,
                UserId = request.UserId,
                AccessToken = request.AccessToken
            });
            if (sessionInfo == null)
            {
                _logger.LogError("Failed to start MQTT session for user {UserId}", request.UserId);
                return BadRequest(new IOTHubResponse<string>
                {
                    Code = 400,
                    Msg = "Failed to start MQTT session",
                    Data = null
                });
            }

            _logger.LogInformation("Successfully started MQTT session for user {UserId}", request.UserId);

            return Ok(new IOTHubResponse<MqttStartSessionResponse>
            {
                Code = 200,
                Msg = "Success",
                Data = new MqttStartSessionResponse
                {
                    AccessToken = sessionInfo.AccessKey,
                    Host = sessionInfo.BrokerHost,
                    Port = sessionInfo.BrokerPort,
                    PubTopics = sessionInfo.PublishTopics,
                    SubTopics = sessionInfo.SubscribeTopics,
                }
            });
        }

        [HttpPost("release-session")]
        public async Task<IActionResult> ReleaseSession([FromBody] ReleaseSessionRequest request)
        {
            if (string.IsNullOrEmpty(request.UserName))
            {
                return BadRequest(new IOTHubResponse<string>
                {
                    Code = 400,
                    Msg = "RefreshToken is required",
                    Data = null
                });
            }

            _logger.LogInformation("Releasing MQTT session with token {RefreshToken}", request.UserName);


            // 2 & 3. End the session (handled by MqttAuthService)
            var result = await _mqttAuthService.EndSessionAsync(request.UserName);
            if (!result)
            {
                _logger.LogError("Failed to release MQTT session with userName {UserName}", request.UserName);
                return BadRequest(new IOTHubResponse<string>
                {
                    Code = 400,
                    Msg = "Failed to release MQTT session",
                    Data = null
                });
            }

            _logger.LogInformation("Successfully released MQTT session with userName {UserName}", request.UserName);

            return Ok(new IOTHubResponse<string>
            {
                Code = 200,
                Msg = "Success",
                Data = "Session released successfully"
            });
        }

        [HttpPost("update-session")]
        public async Task<IActionResult> UpdateSession([FromBody] UpdateSessionRequest request)
        {
            _logger.LogInformation("UpdateSession MQTT session with token {RefreshToken}", request.UserName);

            // 2 & 3. End the session (handled by MqttAuthService)
            var result = await _mqttAuthService.UpdateSessionAsync(request.AccessToken, request.UserName);
            if (!result)
            {
                _logger.LogError("Failed to update MQTT session with userName {UserName}", request.UserName);
                return BadRequest(new IOTHubResponse<string>
                {
                    Code = 400,
                    Msg = "Failed to update MQTT session",
                    Data = null
                });
            }

            _logger.LogInformation("Successfully update MQTT session with userName {UserName}", request.UserName);

            return Ok(new IOTHubResponse<string>
            {
                Code = 200,
                Msg = "Success",
                Data = "Session update successfully"
            });
        }
    }
}