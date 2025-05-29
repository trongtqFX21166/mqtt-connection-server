using VmlMQTT.BrokersMonitoring.Service.Services;

namespace VmlMQTT.BrokersMonitoring.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    _logger.LogInformation("Timer ticked at: {Time}", DateTimeOffset.Now);
                    // Add your recurring task logic here
                    using var scope = _serviceProvider.CreateScope();
                    var brokersMonitorService = scope.ServiceProvider.GetRequiredService<IBrokersMonitorService>();
                    await brokersMonitorService.RunAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation(ex, "Execution stopped.");
            }
        }
    }
}
