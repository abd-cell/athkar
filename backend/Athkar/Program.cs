using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using Athkar;
using Athkar.Areas.Domain.Staff;
using Athkar.Areas.Services.Notifications;
using Athkar.Areas.Services.Quran;
using Athkar.DataAccess;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.Seeders;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Json;
using Athkar.Shareds.Middlewares;
using Athkar.Shareds.Models.Config;
using Athkar.Shareds.Security;
using Athkar.Shareds.Security.Token;

var builder = WebApplication.CreateBuilder(args);

// ── Structured logging (Serilog) ──
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ── Configuration models ──
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<FcmSettings>(builder.Configuration.GetSection("Fcm"));
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<QuranMcpSettings>(builder.Configuration.GetSection("QuranMcp"));
builder.Services.Configure<SwaggerSettings>(builder.Configuration.GetSection("Swagger"));

// Read once, up front, like jwt below — AddSwaggerGen runs before the DI
// container exists to hand out an IOptions<SwaggerSettings>.
var swagger = builder.Configuration.GetSection("Swagger").Get<SwaggerSettings>() ?? new SwaggerSettings();
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

// The signing key is not in appsettings.json — that file is public. Fail here,
// loudly, rather than let HMAC-SHA256 run on an empty or short key: a weak key
// forges every token in the system, and nothing downstream would report it.
// 32 bytes is the output size of SHA-256; below that the key adds no strength.
if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret is missing or shorter than 32 bytes. Supply it out of band:\n" +
        "  development  dotnet user-secrets set \"Jwt:Secret\" \"<32+ random bytes>\"\n" +
        "  deployment   environment variable Jwt__Secret");
}

// ── Framework ──
builder.Services.AddControllers(options => options.Filters.Add<ValidateModelAttribute>())
    // Inbound DateTimes are normalised to UTC — see UtcDateTimeConverter for why.
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        o.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
    });

// Our ValidateModel filter owns invalid-model responses, so every failure —
// validation included — leaves as the same envelope.
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(SwaggerSchemaIds.For);
    c.SwaggerDoc("v1", new OpenApiInfo { Title = swagger.Title, Version = swagger.Version });

    // Every admin endpoint sits behind [AppAuthorize], so without this a
    // Swagger tester has no way to try one short of pasting a header by hand
    // into every request. "Authorize" once here and it rides along on all of
    // them. The requirement is built from the document Swashbuckle hands
    // back, not from the scheme object above — that is what
    // OpenApiSecuritySchemeReference needs to resolve "Bearer" against.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT bearer token. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
    });
    c.AddSecurityRequirement(doc =>
    {
        var requirement = new OpenApiSecurityRequirement();
        requirement.Add(new OpenApiSecuritySchemeReference("Bearer", doc, null), []);
        return requirement;
    });

    // The numbers are the contract across all three stacks (see CLAUDE.md) —
    // this is what makes a reviewer reading /swagger see "1 = Editor,
    // 2 = Admin, 3 = SuperAdmin" instead of a bare integer. A schema filter,
    // not a document filter: it gets the CLR enum type straight from the
    // context, so it still matches after CustomSchemaIds above renames the
    // component key away from the bare type name.
    c.SchemaFilter<SwaggerEnumDescriptions>();
});

// ── CORS (the CMS, and the app when it runs on the web) ──
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ── Database ──
builder.Services.AddDbContext<DatabaseService>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ── Data access + convention-based DI ──
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.RegisterTypes();

// The canonical Qur'an source. Named so the timeout lives beside the setting
// that sets it; the service itself refuses to call out when QuranMcp:Enabled is
// false, so a deployment that never configured this makes no outbound request.
builder.Services.AddHttpClient(QuranMcpClient.HttpClientName, (provider, client) =>
{
    var quran = provider.GetRequiredService<IOptions<QuranMcpSettings>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(quran.TimeoutSeconds, 5, 120));
});

// ── Background work ──
// Convention-based DI covers service interfaces only, so the hosted services
// that run off a timer are registered here.
builder.Services.AddHostedService<ReminderMaterialiserWorker>();
builder.Services.AddHostedService<PushSenderWorker>();
builder.Services.AddHostedService<BroadcastWorker>();

// ── Authentication (JWT; session validated against UserLogin) ──
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            // Default is five minutes of grace, which would quietly stretch every
            // access token past its stated expiry — and with it the window in
            // which a revoked session keeps working. The same server issues and
            // validates these, so there are no clocks to reconcile.
            ClockSkew = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var sessionKey = ctx.Principal?.FindFirst(AppClaims.SessionKey)?.Value;
                if (string.IsNullOrEmpty(sessionKey)) { ctx.Fail("no session"); return; }

                var db = ctx.HttpContext.RequestServices.GetRequiredService<DatabaseService>();
                var exists = await db.Set<UserLogin>()
                    .AnyAsync(l => l.SessionKey == sessionKey && !l.IsDeleted);
                if (!exists) ctx.Fail("session revoked");
            },
            // Without these the framework answers 401/403 with an empty body and
            // the client has nothing to show the person in front of it.
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                await AuthResponseWriter.WriteUnauthorizedAsync(ctx.HttpContext);
            },
            OnForbidden = ctx => AuthResponseWriter.WriteForbiddenAsync(ctx.HttpContext),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

AppHttpContext.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

// ── Pipeline ──
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();

// Records one row per /api/ request. Ahead of auth so rejected (401/403) calls
// are captured too; the acting user is read after the pipeline unwinds.
app.UseMiddleware<ApiLoggerMiddleware>();

// Config-driven, not an IsDevelopment() check — appsettings.json defaults
// this off and appsettings.Development.json switches it on, so a deployment
// decides the same way it decides push and the Qur'an sync, without a
// recompile in either direction.
if (swagger.Enabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint(swagger.Path, $"{swagger.Title} {swagger.Version}"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Migrations and seed on startup, so a fresh clone is one `dotnet run` from a
// working system.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (db.Database.GetPendingMigrations().Any())
        db.Database.Migrate();

    await DataSeeder.SeedAsync(db, logger);
}

app.Run();

/// <summary>
/// Builds unique, readable Swagger schema ids. Generic types render as
/// "WrapperOfArg" (recursively); non-generic types are qualified by the trailing
/// namespace segment so identically named DTOs in different features do not clash.
/// </summary>
internal static class SwaggerSchemaIds
{
    public static string For(Type type)
    {
        if (type.IsGenericType)
        {
            var baseName = type.Name[..type.Name.IndexOf('`')];
            var args = string.Join("And", type.GetGenericArguments().Select(For));
            return $"{baseName}Of{args}";
        }

        var ns = type.Namespace;
        var lastSegment = ns?[(ns.LastIndexOf('.') + 1)..];

        // "Models" is the shared leaf folder for most DTOs — step up one level so
        // the id carries the feature name (Content, Reminders, …) instead.
        if (lastSegment == "Models" && ns!.LastIndexOf('.') is var i and > 0)
        {
            var parent = ns[..i];
            lastSegment = parent[(parent.LastIndexOf('.') + 1)..];
        }

        return string.IsNullOrEmpty(lastSegment) ? type.Name : $"{lastSegment}{type.Name}";
    }
}

/// <summary>
/// Appends "1 = Editor, 2 = Admin, ..." to every enum schema's description, so
/// a value on screen in Swagger is never a bare integer someone has to go
/// cross-reference against the C# source. Every enum in this codebase crosses
/// three stacks with the numbers as the contract (see CLAUDE.md) — this is
/// the one place that fact is worth restating for whoever is reading, not
/// writing, the code.
/// </summary>
internal sealed class SwaggerEnumDescriptions : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;

        var members = Enum.GetValues(context.Type)
            .Cast<object>()
            .Select(value => $"{Convert.ToInt64(value)} = {value}");

        var legend = string.Join(", ", members);
        schema.Description = string.IsNullOrEmpty(schema.Description) ? legend : $"{schema.Description} ({legend})";
    }
}

/// <summary>Named so the test project can reference the host. Never instantiated.</summary>
public partial class Program;
