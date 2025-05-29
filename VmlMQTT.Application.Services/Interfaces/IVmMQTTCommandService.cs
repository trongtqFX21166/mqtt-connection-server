using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Application.Models;

namespace VmlMQTT.Application.Interfaces
{
    public interface IVmMQTTCommandService
    {
        Task<IOTHubResponse<string>> SendCommand(SendCommandRequest requestBody);
    }
}
