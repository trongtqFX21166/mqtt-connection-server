using VmlMQTT.ConsumerNotifyMessage.Service.Entities;

namespace VmlMQTT.ConsumerNotifyMessage.Service.Repositories
{
    public interface INotificationRepo
    {
        Task AddAsync(Notification notification);
    }
}