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

namespace VmlMQTT.Application.Services
{
    public class EmqxBrokerOptions
    {
        public string ApiUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string AuthenId { get; set; }
    }

    public class EmqxBrokerService : IEmqxBrokerService
    {
        private readonly ILogger<EmqxBrokerService> _logger;
        private readonly HttpClient _httpClient;
        private readonly EmqxBrokerOptions _options;

        public EmqxBrokerService(
            ILogger<EmqxBrokerService> logger,
            HttpClient httpClient,
            IOptions<EmqxBrokerOptions> options)
        {
            _logger = logger;
            _httpClient = httpClient;
            _options = options.Value;

            // Configure the HttpClient
            _httpClient.BaseAddress = new Uri(_options.ApiUrl);

            // Set up basic authentication
            var authenticationString = $"{_options.Username}:{_options.Password}";
            var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
        }

        public async Task<bool> CreateUserAsync(string username, string password)
        {
            try
            {
                _logger.LogInformation("Creating user in EMQX: {username}", username);

                var createUserRequest = new CreateUserRequest
                {
                    user_id = username,
                    password = password
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"/api/v5/authentication/{_options.AuthenId}/users",
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

        public async Task<bool> DeleteUserAsync(string username)
        {
            try
            {
                _logger.LogInformation("Deleting user from EMQX: {username}", username);

                var response = await _httpClient.DeleteAsync($"/api/v5/authentication/{_options.AuthenId}/users/{username}");

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

        public async Task<bool> SetUserPermissionsAsync(string username, string[] pubTopics, string[] subTopics)
        {
            try
            {
                _logger.LogInformation("Setting permissions for EMQX user: {username}", username);

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