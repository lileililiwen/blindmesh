using Blindmesh.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Notifications;

/// <summary>
/// Registers the services of the notification module. The store, the access
/// reader and the service are scoped, so they share the scoped database context
/// of the request or of the worker cycle. The mail sender is a singleton,
/// because it holds no state of a request.
/// </summary>
public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationStore, EfNotificationStore>();
        services.AddScoped<INotificationAccessReader, NotificationAccessReader>();
        services.AddScoped<NotificationService>();
        services.AddSingleton<IMailSender, SmtpMailSender>();

        return services;
    }
}
