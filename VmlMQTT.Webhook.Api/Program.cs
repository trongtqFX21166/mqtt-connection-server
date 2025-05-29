using Microsoft.Extensions.Options;
using Platform.KafkaClient;
using Platform.KafkaClient.Models;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ProducerConfig>("Producer", builder.Configuration.GetSection("Producer"));
builder.Services.AddSingleton<IProducer>(s =>
{
    var settings = s.GetRequiredService<IOptionsMonitor<ProducerConfig>>().Get("Producer");
    var logger = s.GetRequiredService<ILogger<Producer>>();

    return new Producer(logger, settings);
});

builder.Services.AddScoped<IWebHookService, WebHookService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
