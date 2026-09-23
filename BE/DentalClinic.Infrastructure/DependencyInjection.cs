using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DentalClinicDatabase")
            ?? "Server=.\\SQLEXPRESS;Database=DentalClinicManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        services.AddDbContext<DentalClinicDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IImageStorageService, CloudinaryImageStorageService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();

        return services;
    }
}
