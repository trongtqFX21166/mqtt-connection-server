using VmlMQTT.ConsumerNotifyMessage.Service.Entities;

namespace VmlMQTT.ConsumerNotifyMessage.Service.Repositories
{
    public class NotificationRepo : INotificationRepo
    {
        private readonly NotificationDbContext _dbContext;

        public NotificationRepo(NotificationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(Notification notification)
        {
            await _dbContext.Notification.AddAsync(notification);
            await _dbContext.SaveChangesAsync();
        }
    }
}
