using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using TransactionOutboxDemo.Db;
using TransactionOutboxDemo.Services;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Configure PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(new DomainEventInterceptor());
});

// Register Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Configure RabbitMQ
var rabbitMqHostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitMqPort = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
var rabbitMqUserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
var rabbitMqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory
    {
        HostName = rabbitMqHostName,
        Port = rabbitMqPort,
        UserName = rabbitMqUserName,
        Password = rabbitMqPassword,
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
    };
    return factory.CreateConnection();
});

// Register Outbox Processor Service
builder.Services.AddHostedService<OutboxProcessorService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Transaction Outbox Demo API",
        Version = "v1",
        Description = "API demonstrating the Transaction Outbox pattern"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
      app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction Outbox Demo API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();
app.MapControllers();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
