using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Infrastructure.Data;
using SporcuGelisim.Infrastructure.Services;

namespace SporcuGelisim.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=SporcuGelisimPlatformu;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)),
            ServiceLifetime.Scoped);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<DbSeeder>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IAthleteProfileService, AthleteProfileService>();
        services.AddScoped<IAthleteAccessService, AthleteAccessService>();
        services.AddScoped<IAthleteRelationService, AthleteRelationService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IMotivationWordService, MotivationWordService>();
        services.AddScoped<IAthleteSessionService, AthleteSessionService>();
        services.AddScoped<IAthleteWordWorkflow, AthleteWordWorkflow>();
        services.AddScoped<ISessionWordService, SessionWordService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
