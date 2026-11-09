using DentalClinic.Infrastructure.Services.StaffShiftProviders;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Services;
using DentalClinic.Infrastructure.BackgroundServices;
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
            ?? "Server=.\\SQLEXPRESS;Database=DentalCare_Integration_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=false";

        services.AddDbContext<DentalClinicDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddMemoryCache();

        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IDentalImageFileValidator, DentalImageFileValidator>();
        services.AddSingleton<IBlogMediaFileValidator, BlogMediaFileValidator>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        // Storage services: Cloudinary retained for Profile avatars and external media
        services.AddScoped<CloudinaryImageStorageService>();
        services.AddScoped<IImageStorageService>(sp => sp.GetRequiredService<CloudinaryImageStorageService>());
        services.AddScoped<IMediaStorageService, CloudinaryMediaStorageService>();

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
        services.AddScoped<IFollowUpSchedulingService, FollowUpSchedulingService>();
        services.AddScoped<ISupportService, SupportService>();
        services.AddScoped<IBlogService, BlogService>();
        services.AddScoped<IAppointmentReminderService, AppointmentReminderService>();
        services.AddScoped<IMedicineService, MedicineService>();
        // Workforce Attendance & Operations Services
        services.AddScoped<DentistShiftProvider>();
        services.AddScoped<ReceptionistShiftProvider>();
        services.AddScoped<IStaffShiftResolver, CompositeStaffShiftProvider>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IWorkforceReportService, WorkforceReportService>();
        services.AddScoped<IClinicOperationsService, ClinicOperationsService>();
        services.AddScoped<IAdminImpactAnalysisService, AdminImpactAnalysisService>();
        services.AddHostedService<AppointmentReminderBackgroundWorker>();

        return services;
    }
}
