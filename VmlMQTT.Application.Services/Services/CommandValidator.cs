using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;

namespace VmlMQTT.Application.Services
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
    }

    public class CommandValidator : ICommandValidator
    {
        private readonly HashSet<string> _allowedCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "status", "restart", "config_update", "location", "diagnostic",
            "emergency_stop", "lock", "unlock", "set_parameter"
        };

        private readonly Dictionary<string, HashSet<string>> _requiredParameters = new()
        {
            ["set_parameter"] = new() { "parameter_name", "parameter_value" },
            ["config_update"] = new() { "config_section" }
        };

        public Task<ValidationResult> ValidateAsync(SendCommandRequest request)
        {
            var errors = new List<string>();

            // Basic validation
            if (string.IsNullOrEmpty(request.SessionId))
                errors.Add("SessionId is required");

            if (string.IsNullOrEmpty(request.DeviceId))
                errors.Add("DeviceId is required");

            if (string.IsNullOrEmpty(request.Phone))
                errors.Add("Phone is required");

            if (string.IsNullOrEmpty(request.RequestId))
                errors.Add("RequestId is required");

            if (string.IsNullOrEmpty(request.Command))
                errors.Add("Command is required");

            // Phone format validation
            if (!string.IsNullOrEmpty(request.Phone) && !IsValidPhoneFormat(request.Phone))
                errors.Add("Invalid phone format");

            // Command validation
            if (!string.IsNullOrEmpty(request.Command) && !_allowedCommands.Contains(request.Command))
                errors.Add($"Command '{request.Command}' is not allowed");

            // Parameter validation
            if (!string.IsNullOrEmpty(request.Command) && _requiredParameters.ContainsKey(request.Command))
            {
                var required = _requiredParameters[request.Command];
                var missing = required.Where(p => !request.Parameters.ContainsKey(p)).ToList();
                if (missing.Any())
                    errors.Add($"Missing required parameters for '{request.Command}': {string.Join(", ", missing)}");
            }

            // Timeout validation
            if (request.TimeoutSeconds <= 0 || request.TimeoutSeconds > 300)
                errors.Add("TimeoutSeconds must be between 1 and 300");

            return Task.FromResult(errors.Any()
                ? ValidationResult.Failure(errors.ToArray())
                : ValidationResult.Success());
        }

        private bool IsValidPhoneFormat(string phone)
        {
            // Basic phone validation - adjust based on your requirements
            return phone.All(char.IsDigit) && phone.Length >= 9 && phone.Length <= 15;
        }
    }
}
