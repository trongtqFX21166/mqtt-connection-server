using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Core.Models;
using Platform.IOTHub.VietmapHub.Api.Models.EMQXBroker;
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

                using HttpClient _httpClient = new HttpClient();
                var authenticationString = $"{host.UserName}:{host.Password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);

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

        public async Task<bool> DeleteUserAsync(EmqxBrokerHost host, string username)
        {
            try
            {
                _logger.LogInformation("Deleting user from EMQX: {username}", username);

                using HttpClient _httpClient = new HttpClient();
                var authenticationString = $"{host.UserName}:{host.Password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);

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

        public async Task<bool> SetUserPermissionsAsync(EmqxBrokerHost host, string username, string[] pubTopics, string[] subTopics)
        {
            try
            {
                _logger.LogInformation("Setting permissions for EMQX user: {username}", username);
                using HttpClient _httpClient = new HttpClient();
                var authenticationString = $"{host.UserName}:{host.Password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);

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

                var role = new EMQXRole
                {
                    username = username,
                    rules = rules
                };

                var response = await _httpClient.PutAsJsonAsync(
                    $"/api/v5/authorization/sources/built_in_database/username/{username}",
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
    }
}   