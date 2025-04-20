using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Services;
using VmlMQTT.Application.Services.Interfaces;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Infrastructure.Data;
using VmlMQTT.Infrastructure.Repositories;
using VmlMQTT.Infratructure.Data;
using VmlMQTT.Infratructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure PostgreSQL
builder.Services.AddDbContext<VmlMQTTDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories
builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
builder.Services.AddScoped<IEmqxBrokerHostRepository, EmqxBrokerHostRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Register application services
builder.Services.AddScoped<IVmlAuthService, VmlAuthService>();
builder.Services.AddScoped<IMqttAuthService, MqttAuthService>();

// Configure and register EmqxBrokerService with HttpClient
builder.Services.Configure<EmqxBrokerOptions>(
    builder.Configuration.GetSection("EmqxBroker"));

builder.Services.AddHttpClient<IEmqxBrokerService, EmqxBrokerService>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VmlMQTTDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Migrate database on startup in development
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<VmlMQTTDbContext>();
        dbContext.Database.Migrate();
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();