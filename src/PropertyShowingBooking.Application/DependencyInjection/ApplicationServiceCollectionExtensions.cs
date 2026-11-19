using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PropertyShowingBooking.Application.Behaviors;

namespace PropertyShowingBooking.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(ApplicationAssembly).Assembly));

        services.AddValidatorsFromAssembly(typeof(ApplicationAssembly).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
