using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Application.Services
{

    public class EmqxBrokerService : IEmqxBrokerService
    {
        private readonly ILogger<EmqxBrokerService> _logger;
        public EmqxBrokerService(
            ILogger<EmqxBrokerService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> CreateUserAsync(EmqxBrokerHost host, string username, string password)
        {
            try
            {
                _logger.LogInformation("Creating user in EMQX: {username}", username);

                using HttpClient _httpClient = getClient(host);

                var createUserRequest = new CreateUserRequest
                {
                    user_id = username,
                    password = password
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"/api/v5/authentication/password_based%3Abuilt_in_database/users",
                    createUserRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create EMQX user. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    if (errorContent.Contains("ALREADY_EXISTS"))
                    {
                        return await UpdateUserAsync(host, username, password);
                    }
                    return false;
                }

                _logger.LogInformation("Successfully created EMQX user: {username}", username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating EMQX user: {username}", username);
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(EmqxBrokerHost host, string username, string password)
        {
            try
            {
                _logger.LogInformation("Update user in EMQX: {username}", username);

                using HttpClient _httpClient = getClient(host);

                var requestBody = new
                {
                    password
                };

                var response = await _httpClient.PutAsJsonAsync(
                    $"/api/v5/authentication/password_based%3Abuilt_in_database/users/{username}",
                    requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to Update EMQX user. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Successfully Update EMQX user: {username}", username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Update EMQX user: {username}", username);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(EmqxBrokerHost host, string username)
        {
            try
            {
                _logger.LogInformation("Deleting user from EMQX: {username}", username);

                using HttpClient _httpClient = getClient(host);

                var response = await _httpClient.DeleteAsync($"/api/v5/authentication/password_based%3Abuilt_in_database/users/{username}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to delete EMQX user. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Successfully deleted EMQX user: {username}", username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting EMQX user: {username}", username);
                return false;
            }
        }

        public async Task<bool> DeleteUserRolesAsync(EmqxBrokerHost host, string username)
        {
            try
            {
                _logger.LogInformation("Deleting userRoles from EMQX: {username}", username);

                using HttpClient _httpClient = getClient(host);

                var response = await _httpClient.DeleteAsync($"/api/v5/authorization/sources/built_in_database/rules/users/{username}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to delete EMQX user. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Successfully deleted EMQX userRoles: {username}", username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting EMQX userRoles: {username}", username);
                return false;
            }
        }

        public async Task<bool> SetUserPermissionsAsync(EmqxBrokerHost host, string username, string[] pubTopics, string[] subTopics, string[] denyPubTopics, string[] denySubTopics)
        {
            try
            {
                _logger.LogInformation("Setting permissions for EMQX user: {username}", username);
                using HttpClient _httpClient = getClient(host);

                var rules = new List<AccessRight>();

                // Add publish permissions
                foreach (var topic in pubTopics)
                {
                    rules.Add(new AccessRight
                    {
                        action = "publish",
                        permission = "allow",
                        topic = topic
                    });
                }

                // Add subscribe permissions
                foreach (var topic in subTopics)
                {
                    rules.Add(new AccessRight
                    {
                        action = "subscribe",
                        permission = "allow",
                        topic = topic
                    });
                }

                // deny publish permissions
                foreach (var topic in denyPubTopics)
                {
                    rules.Add(new AccessRight
                    {
                        action = "publish",
                        permission = "deny",
                        topic = topic
                    });
                }

                // deny sub permissions
                foreach (var topic in denySubTopics)
                {
                    rules.Add(new AccessRight
                    {
                        action = "subscribe",
                        permission = "deny",
                        topic = topic
                    });
                }


                var role = new EMQXRole
                {
                    username = username,
                    rules = rules
                };

                var response = await _httpClient.PutAsJsonAsync(
                    $"/api/v5/authorization/sources/built_in_database/rules/users/{username}",
                    role);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to set EMQX user permissions. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Successfully set permissions for EMQX user: {username}", username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting permissions for EMQX user: {username}", username);
                return false;
            }
        }

        private HttpClient getClient(EmqxBrokerHost host)
        {
            HttpClient _httpClient = new HttpClient();
            var authenticationString = $"{host.UserName}:{host.Password}";
            var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            _httpClient.BaseAddress = new Uri($"http://{host.Ip}:18083");

            return _httpClient;

        }

        public async Task<EMQXMonitorResponse[]> GetMqttMonitorAsync(EmqxBrokerHost host, long lastest = 120)
        {
            using HttpClient _httpClient = getClient(host);

            var response = await _httpClient.GetAsync($"/api/v5/monitor?latest={lastest}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to set EMQX user permissions. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                return default!;
            }

            //await response.Content.ReadFromJsonAsync<EMQXMonitorResponse[]>()
            return JsonConvert.DeserializeObject<EMQXMonitorResponse[]>(await response.Content.ReadAsStringAsync()) ?? default!;
        }

        public async Task<(bool, long)> CurrentLiveConnectionAsync(EmqxBrokerHost host)
        {
            var content = await GetMqttMonitorAsync(host, 11);
            if (content?.Length > 0)
            {
                var currentLiveConnection = content.OrderByDescending(x => x.TimeStamp).FirstOrDefault()?.Connections ?? 0;
                if (currentLiveConnection > host.LimitConnections) return (false, currentLiveConnection);

                return (true, currentLiveConnection);
            }
            else
            {
                _logger.LogError("Failed to get EMQX monitor data.");
                return (false, 0);
            }
        }


    }
}