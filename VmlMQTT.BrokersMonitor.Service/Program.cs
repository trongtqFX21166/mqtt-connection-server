using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.KafkaClient;
using Platform.KafkaClient.Models;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Services;
using VmlMQTT.BrokersMonitoring.Service;
using VmlMQTT.BrokersMonitoring.Service.Services;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Infratructure.Data;
using VmlMQTT.Infratructure.Repositories;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// Configure PostgreSQL
builder.Services.AddDbContext<VmlMQTTDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<ProducerConfig>("Producer", builder.Configuration.GetSection("Producer"));
builder.Services.AddSingleton<IProducer>(s =>
{
    var settings = s.GetRequiredService<IOptionsMonitor<ProducerConfig>>().Get("Producer");
    var logger = s.GetRequiredService<ILogger<Producer>>();

    return new Producer(logger, settings);
});

builder.Services.AddScoped<IEmqxBrokerHostRepository, EmqxBrokerHostRepository>();
builder.Services.AddScoped<IEmqxBrokerService, EmqxBrokerService>();
builder.Services.AddScoped<IBrokersMonitorService, BrokersMonitorService>();

var host = builder.Build();
await host.RunAsync();
