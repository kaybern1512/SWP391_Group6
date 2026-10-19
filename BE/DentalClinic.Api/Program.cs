using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using DentalClinic.Api.Middleware;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Validators;
using DentalClinic.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using DentalClinic.Infrastructure.Hubs;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Infrastructure & Application Services
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddSignalR();

// 2. Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// 3. Add Controllers with JsonOptions
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value!.Errors.Select(x => !string.IsNullOrEmpty(x.ErrorMessage) ? x.ErrorMessage : "Dữ liệu không hợp lệ."))
            .ToList();

        var response = ApiResponse.Fail("Dữ liệu đầu vào không hợp lệ.", errors);
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
    };
});

// 4. Configure CORS for Frontend MVC (https://localhost:7129)
var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"] ?? "https://localhost:7129";
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        policy.WithOrigins(frontendBaseUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 5. Configure JWT Authentication
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "DentalClinicSuperSecretSecurityKey_GraduationProject_2026_SWP391_Group6#";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "DentalClinicApi";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "DentalClinicWeb";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Allow local dev HTTPS
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";

            var response = ApiResponse.Fail("Bạn cần đăng nhập để thực hiện thao tác này.");
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await context.Response.WriteAsync(json);
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.ContentType = "application/json";

            var response = ApiResponse.Fail("Bạn không có quyền truy cập tài nguyên này.");
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await context.Response.WriteAsync(json);
        }
    };
});

builder.Services.AddAuthorization();

// 6. Configure Swagger with JWT Bearer Definition
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DentalCare Management Web API",
        Version = "v1",
        Description = "API documentation for DentalCare Multi-Specialty Dental Clinic Management System"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
});

var app = builder.Build();

// 7. Configure Middleware Pipeline
app.UseMiddleware<GlobalExceptionHandler>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DentalCare API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// Ensure wwwroot/uploads/avatars directory exists
var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var avatarsPath = Path.Combine(wwwrootPath, "uploads", "avatars");
if (!Directory.Exists(avatarsPath))
{
    Directory.CreateDirectory(avatarsPath);
}

// BẢO MẬT HỒ SƠ Y TẾ: Chặn truy cập trực tiếp các tệp phim chụp qua static files (Task 08 Security Remediation)
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (!string.IsNullOrEmpty(path) && path.StartsWith("/uploads/imaging", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        var response = ApiResponse.Fail("Truy cập trực tiếp thư mục hình ảnh y tế công khai bị nghiêm cấm vì lý do bảo mật hồ sơ bệnh án.");
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
        return;
    }

    await next();
});

app.UseStaticFiles(); // Serves files from wwwroot

app.UseRouting();

app.UseCors("FrontendCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LiveChatHub>("/hubs/livechat");
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

// For WebApplicationFactory in integration tests
public partial class Program { }
