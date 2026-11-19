using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;

namespace OnlineEgitim.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>Registers the business services. Repositories must be registered separately.</summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPasswordHasher>(_ => new PasswordHasher());
        services.TryAddSingleton<IAuditLog>(NullAuditLog.Instance);
        services.TryAddSingleton<ISetupKeyStore>(_ => FileSetupKeyStore.CreateDefault());
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ISetupService, SetupService>();
        services.AddSingleton<IRecoveryService, RecoveryService>();
        services.AddSingleton<ICourseService, CourseService>();
        services.AddSingleton<IEnrollmentService, EnrollmentService>();
        return services;
    }
}
