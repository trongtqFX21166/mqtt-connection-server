using VmlMQTT.Application.DTOs;

namespace VmlMQTT.Application.Interfaces
{
    public interface IWebHookService
    {
        void ReceiveMqttEventHandler(ReceiveMqttEvent body);
    }
}
