using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.KafkaClient;
using Platform.KafkaClient.Models;
using Platfrom.MQTTnet;
using Platfrom.MQTTnet.Models;
using VietmapCloud.Shared.Redis;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Services;
using VmlMQTT.Auth.Api;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Core.Models;
using VmlMQTT.Infratructure.Data;
using VmlMQTT.Infratructure.Repositories;
using Platform.KafkaClient;
using Microsoft.Extensions.Options;
using Platform.KafkaClient.Models;
using VmlMQTT.Auth.Api;
using Platfrom.MQTTnet.Models;
using Platfrom.MQTTnet;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

builder.Services.Configure<ProducerConfig>("Producer", builder.Configuration.GetSection("Producer"));
builder.Services.AddSingleton<IProducer>(s =>
{
    var settings = s.GetRequiredService<IOptionsMonitor<ProducerConfig>>().Get("Producer");
    var logger = s.GetRequiredService<ILogger<Producer>>();

    return new Producer(logger, settings);
});

// Configure PostgreSQL
builder.Services.AddDbContext<VmlMQTTDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// Register repositories
builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
builder.Services.AddScoped<IEmqxBrokerHostRepository, EmqxBrokerHostRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
//builder.Services.AddHostedService<HostedService>();

builder.Services.Configure<MQTTConfig>(builder.Configuration.GetSection("MQTT"));
builder.Services.AddSingleton<IMQTTSubcribe, MQTTSubcribe>();
builder.Services.AddSingleton<IMQTTPublish, MQTTPublish>();

// Register application services
builder.Services.AddScoped<IMqttAuthService, MqttAuthService>();
builder.Services.AddScoped<IEmqxBrokerService, EmqxBrokerService>();

// Core services
builder.Services.AddScoped<ICommandService, CommandService>();
builder.Services.AddScoped<ICommandValidator, CommandValidator>();
builder.Services.AddScoped<IUserSessionService, UserSessionService>();

// Singletons
builder.Services.AddSingleton<IMqttConnectionPool, MqttConnectionPool>();

// Hosted services
builder.Services.AddHostedService<ResponseManager>();
builder.Services.AddHostedService<ResponseHostedService>();

// Memory cache
services.AddMemoryCache();


// Configure settings
builder.Services.Configure<ClientSetting>(
    builder.Configuration.GetSection("ClientSetting"));
builder.Services.Configure<ServerSetting>(
    builder.Configuration.GetSection("ServerSetting"));

builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("RedisSettings"));
builder.Services.AddSingleton<IRedisCache, RedisCache>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();