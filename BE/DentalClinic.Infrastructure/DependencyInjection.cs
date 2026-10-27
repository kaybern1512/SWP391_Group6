using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DentalClinicDatabase")
            ?? "Server=.\\SQLEXPRESS;Database=DentalClinicManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        services.AddDbContext<DentalClinicDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddMemoryCache();

        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IDentalImageFileValidator, DentalImageFileValidator>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // Storage services: Cloudinary retained for Profile avatars and external media
        services.AddScoped<CloudinaryImageStorageService>();
        services.AddScoped<IImageStorageService>(sp => sp.GetRequiredService<CloudinaryImageStorageService>());

        // Storage services: Dedicated Medical Imaging Storage (Defaults to Local)
        services.AddScoped<LocalImageStorageService>();
        services.AddScoped<IMedicalImagingStorageService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var provider = config["MedicalImagingStorage:Provider"] ?? "Local";
            if (string.Equals(provider, "Cloudinary", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<CloudinaryImageStorageService>();
            }
            return sp.GetRequiredService<LocalImageStorageService>();
        });

        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IDentistScheduleService, DentistScheduleService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IMedicalRecordService>(sp => new MedicalRecordService(
            sp.GetRequiredService<DentalClinicDbContext>(),
            sp.GetRequiredService<IMedicalImagingStorageService>(),
            sp.GetRequiredService<IDentalImageFileValidator>(),
            sp.GetRequiredService<ILogger<MedicalRecordService>>()));
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<ITreatmentService, TreatmentService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPostCareService, PostCareService>();
        services.AddScoped<ISupportService, SupportService>();

        return services;
    }
}
