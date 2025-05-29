namespace VmlMQTT.BrokersMonitoring.Service.Services
{
    public interface IBrokersMonitorService
    {
        Task RunAsync(CancellationToken stoppingToken);
    }
}
