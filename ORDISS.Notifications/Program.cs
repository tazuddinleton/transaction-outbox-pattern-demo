using ORDISS.Notifications.Endpoints;
using ORDISS.Notifications.Repositories;
using ORDISS.Notifications.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services.
builder.Services.AddScoped<INotificationRepository, InMemoryNotificationRepository>();
builder.Services.AddSingleton<NotificationEventBus>();
builder.Services.AddScoped<ILongPollNotificationService, LongPollNotificationService>();

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Notifications API"));
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapNotificationEndpoints();

app.Run();
