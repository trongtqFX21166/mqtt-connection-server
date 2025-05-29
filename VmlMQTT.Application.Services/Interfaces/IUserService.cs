using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Core.Models;

namespace VmlMQTT.Application.Interfaces
{
    public interface IUserService
    {
        Task<List<UserSessionDto>> QueryOnlineSessionsAsync(string phone);
    }
}
