using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;

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
        services.TryAddSingleton(_ => SetupKeyEnvironment.FromProcess());
        services.TryAddSingleton(SetupKeyThrottle.Default);
        services.TryAddSingleton<SetupKeyGate>();
        services.AddSingleton<IValidator<RegistrationInput>, RegistrationInputValidator>();
        services.AddSingleton<IValidator<SignInInput>, SignInInputValidator>();
        services.AddSingleton<IValidator<FirstTeacherInput>, FirstTeacherInputValidator>();
        services.AddSingleton<IValidator<NewPasswordInput>, NewPasswordInputValidator>();
        services.AddSingleton<IValidator<StudentNameInput>, StudentNameInputValidator>();
        services.AddSingleton<IValidator<CourseInput>, CourseInputValidator>();
        services.AddSingleton<IValidator<NoteInput>, NoteInputValidator>();
        services.AddSingleton<IValidator<CourseSearch>, CourseSearchValidator>();
        services.AddSingleton<IValidator<TeacherAccountInput>, TeacherAccountInputValidator>();
        services.AddSingleton<IValidator<StudentAccountInput>, StudentAccountInputValidator>();
        services.AddSingleton<IValidator<AccountSearch>, AccountSearchValidator>();
        services.AddSingleton<IValidator<ChangePasswordInput>, ChangePasswordInputValidator>();
        services.AddSingleton<ISessionGuard, SessionGuard>();
        services.AddSingleton<IAdministrationService, AdministrationService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ISetupService, SetupService>();
        services.AddSingleton<IRecoveryService, RecoveryService>();
        services.AddSingleton<ICourseService, CourseService>();
        services.AddSingleton<IEnrollmentService, EnrollmentService>();
        return services;
    }
}
