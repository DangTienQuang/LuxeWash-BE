using AutoWashPro.BLL.Extensions;
using AutoWashPro.BLL.Services;
using BLL.Helpers;
using BLL.Services;
using AutoWashPro.BLL.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using BLL.Services.Interface;
using CloudinaryDotNet;
using AutoWashPro.DAL.Data;
using BLL.Services.AI.Calculators;
using BLL.Services.AI.Helpers;
using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OfficeOpenXml;
using PayOS;
using QuestPDF.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==============================================================================
// 1. SYSTEM CONFIGURATION & CONTROLLERS
// ==============================================================================
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .SelectMany(entry => entry.Value?.Errors.Select(error => (
                    Field: entry.Key,
                    Message: string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Invalid input data."
                        : error.ErrorMessage
                )) ?? Enumerable.Empty<(string Field, string Message)>())
                .ToList();

            var errorMessage = errors
                .Where(error => !string.Equals(error.Field, "request", StringComparison.OrdinalIgnoreCase))
                .Select(error => error.Message)
                .FirstOrDefault()
                ?? errors.Select(error => error.Message).FirstOrDefault();

            return new BadRequestObjectResult(new
            {
                statusCode = 400,
                message = errorMessage ?? "Invalid input data.",
                details = errors.Select(error => new { field = error.Field, message = error.Message }).ToList()
            });
        };
    });

// ==============================================================================
// 2. CORS CONFIGURATION (Frontend Connection)
// ==============================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// ==============================================================================
// 3. DATABASE CONFIGURATION
// ==============================================================================
builder.Services.AddDatabaseInfrastructure(builder.Configuration);

// ==============================================================================
// 4. AUTHENTICATION & SECURITY (JWT)
// ==============================================================================
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AIChatPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                context.User.Identity?.Name
                ?? context.Connection.RemoteIpAddress?.ToString(),

            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));
});

// ==============================================================================
// 5. THIRD-PARTY & AI SERVICES (PayOS, PaddleOCR, LLM)
// ==============================================================================
// 5.1. PayOS Integration - một singleton dùng chung cho toàn bộ ứng dụng
var payOSClient = new PayOSClient(
    builder.Configuration["PayOS:ClientId"] ?? "",
    builder.Configuration["PayOS:ApiKey"] ?? "",
    builder.Configuration["PayOS:ChecksumKey"] ?? ""
);
builder.Services.AddSingleton(payOSClient);

ExcelPackage.License.SetNonCommercialPersonal("AutoWashPro");

// 5.2. AI & OCR Models Integration
var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models/license_plate.onnx");
builder.Services.AddSingleton(new DAL.Data.OnnxInferenceEngine(modelPath));


builder.Services.AddSingleton<PaddleOcrService>(sp =>
{
    var recModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models/rec_model.onnx");
    var dictPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models/dict.txt");
    var logger = sp.GetRequiredService<ILogger<PaddleOcrService>>();
    return new PaddleOcrService(recModelPath, dictPath, logger);
});

QuestPDF.Settings.License = LicenseType.Community;

// 5.3 Firebase Cloud Messaging Integration
try
{
    var credentialEnv = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
    var credentialJson = builder.Configuration["FirebaseAdmin:CredentialJson"];

    if (!string.IsNullOrEmpty(credentialEnv))
    {
        FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions()
        {
            Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(credentialEnv)
        });
        Console.WriteLine("[INFO] Firebase initialized using GOOGLE_APPLICATION_CREDENTIALS.");
    }
    else if (!string.IsNullOrEmpty(credentialJson))
    {
        FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions()
        {
            Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(credentialJson)
        });
        Console.WriteLine("[INFO] Firebase initialized using User Secrets / Configuration.");
    }
    else
    {
        Console.WriteLine("[WARNING] Firebase credentials not found in env or secrets. Push notifications will fail.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] Failed to initialize Firebase: {ex.Message}");
}

// ==============================================================================
// 6. DEPENDENCY INJECTION (BLL Services)
// ==============================================================================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITierService, TierService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVehicleService, AutoWashPro.BLL.Services.VehicleService>();
builder.Services.AddScoped<IVehicleTypeService, AutoWashPro.BLL.Services.VehicleTypeService>();
builder.Services.AddScoped<ICarModelService, CarModelService>();
builder.Services.AddScoped<IServiceService, AutoWashPro.BLL.Services.ServiceService>();
builder.Services.AddScoped<IPayOsService, PayOsService>();
builder.Services.AddScoped<IBookingService, AutoWashPro.BLL.Services.BookingService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<IVoucherCampaignService, VoucherCampaignService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITimeSlotService, TimeSlotService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IOverloadSuggestionService, OverloadSuggestionService>();
builder.Services.AddScoped<IAIChatbotService, AIChatbotService>();
builder.Services.AddScoped<IAIModerationService, AIModerationService>();
builder.Services.AddHttpClient<ILLMService, GeminiAIService>();
builder.Services.AddScoped<IAIIntentService, AIIntentService>();
builder.Services.AddScoped<ILicensePlateService, LicensePlateService>();
builder.Services.Configure<BLL.Helpers.CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<ILaneService, LaneService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IManagerService, ManagerService>();
builder.Services.AddScoped<IOperationStaffService, OperationStaffService>();
builder.Services.AddScoped<IBusinessBookingService, BusinessBookingService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<ILaneSchedulerService, LaneSchedulerService>();
builder.Services.AddScoped<IFleetService, FleetService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IServiceMaterialUsageService, ServiceMaterialUsageService>();
builder.Services.AddScoped<IInventoryTransferService, InventoryTransferService>();
builder.Services.AddScoped<IBookingMaterialUsageService, BookingMaterialUsageService>();
builder.Services.AddScoped<IInventoryReportService, InventoryReportService>();

// ==============================================================================
// 7. BACKGROUND WORKERS
// ==============================================================================
builder.Services.AddScoped<IStaffManagementService, StaffManagementService>();
builder.Services.AddScoped<ICRMCampaignService, CRMCampaignService>();
builder.Services.AddHttpClient<IWeatherService, WeatherService>();
builder.Services.AddScoped<IOccupancyService, OccupancyService>();
builder.Services.AddScoped<IBranchRevenueAnalyticsService, BranchRevenueAnalyticsService>();
builder.Services.AddScoped<IAnnualTierService, AnnualTierService>();
builder.Services.AddScoped<IFeatureGenerationService, FeatureGenerationService>();
builder.Services.AddScoped<IScenarioEvaluationService, ScenarioEvaluationService>();
builder.Services.AddSingleton<ICarDetectionService, CarDetectionService>();
builder.Services.AddSingleton<ICarClassificationService, CarClassificationService>();
builder.Services.AddScoped<ICarModelMatchingService, CarModelMatchingService>();
builder.Services.AddScoped<ICarRecognitionService, CarRecognitionService>();

builder.Services.AddScoped<IVisitFeatureCalculator, VisitFeatureCalculator>();
builder.Services.AddScoped<IVehicleFeatureCalculator, VehicleFeatureCalculator>();
builder.Services.AddScoped<ISpendingFeatureCalculator, SpendingFeatureCalculator>();
builder.Services.AddScoped<IPromotionFeatureCalculator, PromotionFeatureCalculator>();
builder.Services.AddScoped<IServicePreferenceCalculator, ServicePreferenceCalculator>();
builder.Services.AddScoped<IBranchPreferenceCalculator, BranchPreferenceCalculator>();
builder.Services.AddScoped<IEngagementFeatureCalculator, EngagementFeatureCalculator>();
builder.Services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddScoped<IReflectionHelper, ReflectionHelper>();
builder.Services.AddScoped<IConfidenceCalculator, ConfidenceCalculator>();
builder.Services.AddScoped<IScenarioExecutionLogger, ScenarioExecutionLogger>();

builder.Services.AddHostedService<AutoWashPro.API.Workers.AnnualTierResetWorker>();
builder.Services.AddHostedService<AutoWashPro.API.Workers.CRMCampaignWorker>();
builder.Services.AddHostedService<AutoWashPro.API.Workers.AutoWashCompletionWorker>();

// ==============================================================================
// 8. SWAGGER CONFIGURATION
// ==============================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Paste the JWT token here. The system will automatically prepend 'Bearer '.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// ==============================================================================
// 9. BUILD APP & MIDDLEWARE PIPELINE
// ==============================================================================
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IBookingAttendanceService, BookingAttendanceService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.Configure<BLL.Helpers.CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

// Note: AnnualTierResetWorker is already registered at line 247; duplicate removed.

builder.Services.AddSingleton(provider =>
{
    var settings =
        provider.GetRequiredService<
            IOptions<BLL.Helpers.CloudinarySettings>>()
        .Value;

    var account = new Account(
        settings.CloudName,
        settings.ApiKey,
        settings.ApiSecret);

    return new Cloudinary(account);
});

var app = builder.Build();

app.UseMiddleware<AutoWashPro.API.Middlewares.ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

// ==============================================================================
// 10. DATABASE MIGRATION ON STARTUP
// ==============================================================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AutoWashDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();
