
using Amazon.MediaConvert;
using Amazon.Rekognition;
using Amazon.S3;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Infrastructure;
using OboxSteam.Infrastructure.Commons;
using OboxSteam.Infrastructure.Persistence;
using OboxSteam.Infrastructure.Services;
using Resend;
using System.Text;

namespace OboxSteam.API.Architecture;

public static class IocContainer
{
    public static IServiceCollection SetupIocContainer(this IServiceCollection services)
    {
        var configuration = GetConfiguration();

        // Add DbContext
        services.SetupDbContext(configuration);

        // Add Swagger
        services.SetupSwagger();

        // Add HttpContextAccessor (required for ClaimsService)
        services.AddHttpContextAccessor();

        // Named HttpClient used by AwsWebhookController to:
        //   1. Confirm SNS subscriptions (SubscribeURL)
        //   2. Fetch SNS signing certificates (SigningCertURL)
        services.AddHttpClient("sns", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Add Infrastructure services
        services.AddScoped<ICurrentTime, CurrentTime>();
        services.AddScoped<IClaimsService, ClaimsService>();
        services.AddScoped<IFaceRecognitionService, FaceRecognitionService>();
        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<ICsvQuestionParserService, CsvQuestionParserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IVideoConverterService, VideoConverterService>();

        // In-process queue (singleton) shared between the personal-video trigger and its worker.
        services.AddSingleton<IPersonalVideoQueue, PersonalVideoQueue>();


        // Add Unit of Work (repositories are lazy-loaded inside)
        services.AddScoped<OboxSteam.Domain.Interfaces.IUnitOfWork, UnitOfWork>();

        // Add Business services
        services.SetupBusinessServicesLayer();

        // Background Services
        services.AddHostedService<PendingEnrollmentCleanupService>();
        services.AddHostedService<OpenClassAutoStartService>();
        services.AddHostedService<PersonalVideoGenerationWorker>();

        // Add JWT Authentication
        services.SetupJwt(configuration);

        // 3rd party services
        services.SetupAwsS3();
        //services.SetupRedis();
        services.SetupReSendService(configuration);
        services.SetupAwsRekognition();
        services.SetupAwsMediaConvert();
        services.SetupBedrockMantle();
        services.SetupPaymentGateways(configuration);

        return services;
    }

    public static IServiceCollection SetupReSendService(this IServiceCollection services, IConfiguration configuration)
    {
        var apiToken = configuration["RESEND_APITOKEN"];
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            throw new InvalidOperationException("RESEND_APITOKEN is not configured.");
        }

        services.AddOptions();
        services.AddHttpClient<ResendClient>();
        services.Configure<ResendClientOptions>(o => o.ApiToken = apiToken);
        services.AddTransient<IResend, ResendClient>();

        return services;
    }

    public static IServiceCollection SetupAwsS3(this IServiceCollection services)
    {
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY")
            ?? throw new InvalidOperationException("AWS_ACCESS_KEY not found in environment variables.");
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_KEY")
            ?? throw new InvalidOperationException("AWS_SECRET_KEY not found in environment variables.");
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";

        services.AddSingleton<IAmazonS3>(_ =>
            new AmazonS3Client(
                accessKey,
                secretKey,
                Amazon.RegionEndpoint.GetBySystemName(region)));

        return services;
    }

    //public static IServiceCollection SetupRedis(this IServiceCollection services)
    //{
    //    var redisConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Redis");

    //    if (string.IsNullOrWhiteSpace(redisConnectionString))
    //        throw new InvalidOperationException("Redis connection string not found in environment variables.");

    //    services.AddSingleton<IConnectionMultiplexer>(sp =>
    //        ConnectionMultiplexer.Connect(redisConnectionString));

    //    services.AddScoped<IRedisService, RedisService>();

    //    return services;
    //}

    private static IConfiguration GetConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static IServiceCollection SetupDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json");
        }

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

        services.AddDbContext<OboxSteamDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(OboxSteamDbContext).Assembly.FullName);
                // Built-in retry logic - tự động retry khi connection fail
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 10,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            })
        );

        return services;
    }

    public static IServiceCollection SetupBusinessServicesLayer(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISeedService, SeedService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IProgramService, ProgramService>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IExpertService, ExpertService>();
        services.AddScoped<IParentService, ParentService>();
        services.AddScoped<IPersonalVideoService, PersonalVideoService>();
        services.AddScoped<IStrengthMatchService, BedrockMantleStrengthMatchService>();
        services.AddScoped<IProgramEnrollmentService, ProgramEnrollmentService>();
        services.AddScoped<IEnrollmentCurriculumService, EnrollmentCurriculumService>();
        services.AddScoped<IModuleEnrollmentService, ModuleEnrollmentService>();
        services.AddScoped<IActivityProgressService, ActivityProgressService>();
        services.AddScoped<IClassEnrollmentService, ClassEnrollmentService>();
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IClassSessionService, ClassSessionService>();
        services.AddScoped<ISessionAttendanceService, SessionAttendanceService>();
        services.AddScoped<IQuestionBankService, QuestionBankService>();
        services.AddScoped<IBankQuestionService, BankQuestionService>();
        services.AddScoped<IProgramReviewService, ProgramReviewService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IResearchMilestoneService, ResearchMilestoneService>();
        services.AddScoped<IResearchSubmissionService, ResearchSubmissionService>();
        services.AddScoped<IAssignmentSubmissionService, AssignmentSubmissionService>();
        services.AddScoped<IQuizAttemptService, QuizAttemptService>();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        return services;
    }

    public static IServiceCollection SetupPaymentGateways(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind settings from environment variables
        services.Configure<OboxSteam.Application.Commons.StripeSettings>(opts =>
        {
            opts.SecretKey = configuration["STRIPE_SECRET_KEY"] ?? string.Empty;
            opts.PublishableKey = configuration["STRIPE_PUBLISHABLE_KEY"] ?? string.Empty;
            opts.WebhookSecret = configuration["STRIPE_WEBHOOK_SECRET"] ?? string.Empty;
        });

        return services;
    }

    public static IServiceCollection SetupAwsRekognition(this IServiceCollection services)
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";

        services.AddSingleton<IAmazonRekognition>(_ =>
            new AmazonRekognitionClient(
                Environment.GetEnvironmentVariable("AWS_ACCESS_KEY"),
                Environment.GetEnvironmentVariable("AWS_SECRET_KEY"),
                Amazon.RegionEndpoint.GetBySystemName(region)));

        return services;
    }

    public static IServiceCollection SetupAwsMediaConvert(this IServiceCollection services)
    {
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY")
            ?? throw new InvalidOperationException("AWS_ACCESS_KEY not found in environment variables.");
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_KEY")
            ?? throw new InvalidOperationException("AWS_SECRET_KEY not found in environment variables.");
        var endpoint = Environment.GetEnvironmentVariable("AWS_MEDIACONVERT_ENDPOINT")
            ?? throw new InvalidOperationException("AWS_MEDIACONVERT_ENDPOINT not found in environment variables.");

        services.AddSingleton<IAmazonMediaConvert>(_ =>
            new AmazonMediaConvertClient(
                accessKey,
                secretKey,
                new AmazonMediaConvertConfig
                {
                    ServiceURL = endpoint
                }));

        return services;
    }

    public static IServiceCollection SetupBedrockMantle(this IServiceCollection services)
    {
        var apiKey = Environment.GetEnvironmentVariable("BEDROCK_API_KEY")
            ?? throw new InvalidOperationException("BEDROCK_API_KEY not found in environment variables.");

        // Bedrock Mantle region — defaults to ap-southeast-2 (Sydney).
        // Supported Mantle regions: us-east-1, us-east-2, us-west-2, ap-southeast-2/3,
        // ap-south-1, ap-northeast-1, eu-central-1, eu-west-1/2, eu-south-1, eu-north-1.
        var mantleRegion = Environment.GetEnvironmentVariable("BEDROCK_MANTLE_REGION") ?? "ap-southeast-2";
        var endpoint = new Uri($"https://bedrock-mantle.{mantleRegion}.api.aws/v1");

        // OpenAI SDK pointed at the Bedrock Mantle OpenAI-compatible endpoint.
        // Auth via Bedrock API Key (generated in AWS Bedrock console, not IAM).
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = endpoint });

        // Moonshot Kimi K2.5 on AWS Bedrock (ap-southeast-2)
        var chatClient = openAiClient.GetChatClient("moonshotai.kimi-k2.5");

        // Singleton: OpenAIClient / ChatClient are thread-safe.
        services.AddSingleton(chatClient);

        return services;
    }



    private static IServiceCollection SetupSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.UseInlineDefinitionsForEnums();

            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "OboxSteam API",
                Version = "v1",
                Description = @"API for OboxSteam.",
                Contact = new OpenApiContact
                {
                    Name = "OboxSteam Team",
                    Email = "support@oboxsteam.com"
                }
            });

            // JWT Authentication configuration for Swagger
            var jwtSecurityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter 'Bearer' [space] and then your valid JWT token in the text input below.\n\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            c.AddSecurityDefinition("Bearer", jwtSecurityScheme);

            var securityRequirement = new OpenApiSecurityRequirement
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
            };

            c.AddSecurityRequirement(securityRequirement);

            c.UseAllOfForInheritance();
            c.EnableAnnotations();

            c.MapType<DateTime>(() => new OpenApiSchema
            {
                Type = "string",
                Example = new OpenApiString("15/06/2026 14:30:00")
            });
            c.MapType<DateTime?>(() => new OpenApiSchema
            {
                Type = "string",
                Nullable = true,
                Example = new OpenApiString("15/06/2026 14:30:00")
            });

            // Add file upload operation filter
            c.OperationFilter<FileUploadOperationFilter>();
        });

        return services;
    }

    private static IServiceCollection SetupJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var secretKey = configuration["JWT:SecretKey"];
        var issuer = configuration["JWT:Issuer"];
        var audience = configuration["JWT:Audience"];

        if (string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException("JWT:SecretKey not found in appsettings.json");
        }

        if (string.IsNullOrEmpty(issuer))
        {
            throw new InvalidOperationException("JWT:Issuer not found in appsettings.json");
        }

        if (string.IsNullOrEmpty(audience))
        {
            throw new InvalidOperationException("JWT:Audience not found in appsettings.json");
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = false; // Set to true in production with HTTPS
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero // Remove delay of token when expire
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("SuperAdminPolicy", policy =>
                policy.RequireRole("SuperAdmin"));

            options.AddPolicy("ManagerPolicy", policy =>
                policy.RequireRole("Manager"));

            options.AddPolicy("MentorPolicy", policy =>
                policy.RequireRole("Mentor"));

            options.AddPolicy("ParentPolicy", policy =>
                policy.RequireRole("Parent"));

            options.AddPolicy("StudentPolicy", policy =>
                policy.RequireRole("Student"));
        });

        return services;
    }
}
