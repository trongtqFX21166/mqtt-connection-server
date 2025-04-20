using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.IOTHub.VietmapHub.Api.Models.EMQXBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Core.Models;

namespace VmlMQTT.Application.Services
{
    public class EmqxBrokerService : IEmqxBrokerService
    {
        private readonly ILogger<EmqxBrokerService> _logger;
        private readonly HttpClient _client;
        private readonly EmqxBrokerOptions _options;

        public EmqxBrokerService(
            ILogger<EmqxBrokerService> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;

            _client = clientFactory.CreateClient("EMQXBrokerApi");
        }

        public async Task<Models.User> AddUser(string clientId, string passwordHash)
        {
            try
            {
                var resp = await _client.PostAsync($"/api/v5/authentication/{_config.AuthenId}/users"
                    , new StringContent(JsonConvert.SerializeObject(new CreateUserRequest
                    {
                        user_id = clientId,
                        password = passwordHash
                    }), Encoding.UTF8, "application/json"));

                var data = await resp.Content.ReadAsStringAsync();
                if (!@resp.IsSuccessStatusCode)
                {
                    var error = JsonConvert.DeserializeObject<Models.EMQXBroker.ErrorResponse>(data);
                    tryPushKafkaLog(new kafkaLog
                    {
                        Category = nameof(kafkaLogMqttCategory.BrokerApi),
                        Imei = clientId,
                        Device = "companion",
                        ResponseCode = "400",
                        ResponseData = $"AddUser {clientId} error : {data}",
                        Time = DateTime.Now.ToString(),
                        TimeStamp = DateTime.UtcNow.ToSecondsUnix(),
                        ServiceName = "emqx-api"
                    });
                    throw new IOTHubException((int)HttpStatusCode.BadRequest, error.Message);
                }

                return JsonConvert.DeserializeObject<Models.User>(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            return null;
        }

        public async Task DeleteUser(string clientId)
        {
            try
            {
                var resp = await _client.DeleteAsync($"/api/v5/authentication/{_config.AuthenId}/users/{clientId}");

                if (!@resp.IsSuccessStatusCode)
                {
                    var error = JsonConvert.DeserializeObject<Models.EMQXBroker.ErrorResponse>(data);
                    throw new IOTHubException((int)HttpStatusCode.BadRequest, error.Message);
                }

                return JsonConvert.DeserializeObject<Models.User>(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

        }

        public async Task AddRoles(string userName, IList<EMQXRole> roles)
        {
            try
            {
                var rule = roles.FirstOrDefault();
                var resp = await _client.PutAsync($"/api/v5/authorization/sources/built_in_database/username/{userName}"
                    , new StringContent(JsonConvert.SerializeObject(rule), Encoding.UTF8, "application/json"));

                var data = await resp.Content.ReadAsStringAsync();
                if (!@resp.IsSuccessStatusCode)
                {
                    var error = JsonConvert.DeserializeObject<Models.EMQXBroker.ErrorResponse>(data);
                    throw new IOTHubException((int)HttpStatusCode.BadRequest, error.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

    }

}
