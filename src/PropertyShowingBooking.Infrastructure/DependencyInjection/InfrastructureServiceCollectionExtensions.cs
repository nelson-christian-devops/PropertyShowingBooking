using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropertyShowingBooking.Application.Repositories;
using PropertyShowingBooking.Infrastructure.Persistence;
using PropertyShowingBooking.Infrastructure.Repositories;

namespace PropertyShowingBooking.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PropertyShowingBooking")
            ?? throw new InvalidOperationException("The PropertyShowingBooking connection string is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IShowingSlotRepository, ShowingSlotRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingHistoryRepository, BookingHistoryRepository>();

        return services;
    }
}
