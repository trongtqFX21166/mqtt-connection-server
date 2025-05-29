using Microsoft.AspNetCore.Mvc;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Auth.Api.Models;
using VmlMQTT.Core.Models;

namespace VmlMQTT.Auth.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("query-online-sessions")]
        public async Task<IActionResult> QueryOnlineSessions(string phone)
        {
            var data = await _userService.QueryOnlineSessionsAsync(RemoveVietNamAreacode(phone));

            return Ok(new IOTHubResponse<List<UserSessionDto>>
            {
                Code = 200,
                Msg = "Success",
                Data = data
            });
        }

        private static string RemoveVietNamAreacode(string phone)
        {
            try
            {
                if (phone.StartsWith("0"))
                {
                    phone = phone.Remove(0, 1);
                }
                //else if (phone.StartsWith("84"))
                //{
                //    phone = phone.Remove(0, 2);
                //}
                else if (phone.StartsWith("+84"))
                {
                    phone = phone.Remove(0, 3);
                }
            }
            catch (Exception)
            {
            }

            return phone;
        }
    }
}
