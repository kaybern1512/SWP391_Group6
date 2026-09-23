using DentalClinic.Web.Handlers;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add MVC with Views
builder.Services.AddControllersWithViews();

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Session configuration with distributed memory cache
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Authentication: Cookie Authentication + Google External Login
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Google Authentication configured via configuration or user-secrets
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

Action<Microsoft.AspNetCore.Authentication.Google.GoogleOptions> configureGoogle = options =>
{
    options.ClientId = (!string.IsNullOrEmpty(googleClientId) && googleClientId != "YOUR_GOOGLE_CLIENT_ID")
        ? googleClientId
        : "dummy-client-id.apps.googleusercontent.com";
    options.ClientSecret = (!string.IsNullOrEmpty(googleClientSecret) && googleClientSecret != "YOUR_GOOGLE_CLIENT_SECRET")
        ? googleClientSecret
        : "dummy-client-secret";
    options.SaveTokens = true;
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    // Explicitly extract and save id_token from Google OAuth token response
    options.Events.OnCreatingTicket = context =>
    {
        if (context.TokenResponse?.Response != null &&
            context.TokenResponse.Response.RootElement.TryGetProperty("id_token", out var idTokenProp))
        {
            var idToken = idTokenProp.GetString();
            if (!string.IsNullOrEmpty(idToken))
            {
                context.Identity?.AddClaim(new System.Security.Claims.Claim("id_token", idToken));
                var tokens = context.Properties.GetTokens().ToList();
                tokens.Add(new Microsoft.AspNetCore.Authentication.AuthenticationToken { Name = "id_token", Value = idToken });
                context.Properties.StoreTokens(tokens);
            }
        }
        return Task.CompletedTask;
    };
};

authBuilder.AddGoogle(configureGoogle);

// Register Token and Cookie Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();

// Register DelegatingHandler
builder.Services.AddTransient<ApiAuthorizationHandler>();

// Configure Base API URL
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7350";

HttpMessageHandler CreatePrimaryHttpHandler()
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
}

// Raw HttpClient for token refresh without ApiAuthorizationHandler (prevents recursion)
builder.Services.AddHttpClient("RawApiClient", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(CreatePrimaryHttpHandler);

// Auth API client (without delegating handler since auth endpoints do not need bearer tokens)
builder.Services.AddHttpClient<IAuthApiService, AuthApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(CreatePrimaryHttpHandler);

// Profile API client with ApiAuthorizationHandler (attaches Bearer and handles 401 refresh retry)
builder.Services.AddHttpClient<IProfileApiService, ProfileApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<ApiAuthorizationHandler>()
.ConfigurePrimaryHttpMessageHandler(CreatePrimaryHttpHandler);

var app = builder.Build();

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// UseSession must come BEFORE UseAuthentication and UseAuthorization
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
