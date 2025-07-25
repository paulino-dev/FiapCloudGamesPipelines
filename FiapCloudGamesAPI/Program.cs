using AutenticacaoEAutorizacaoCorreto.Services;
using AutenticacaoEAutorizacaoCorreto.Services.IService;
using FiapCloudGamesAPI.Context;
using FiapCloudGamesAPI.Infra;
using FiapCloudGamesAPI.Infra.Middleware;
using FiapCloudGamesAPI.Models.Configuration;
using FiapCloudGamesAPI.Services;
using FiapCloudGamesAPI.Services.IService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(80));

#region JWT
builder.Services.AddCors();
builder.Services.AddControllers();

var key = Encoding.ASCII.GetBytes(builder.Configuration["ConfigSecret:Secret"]);
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
#endregion

builder.Services.AddAuthorization(options =>
{
    options.PoliticasCustomizadas();
});

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.Configure<ConfigSecret>(builder.Configuration.GetSection("ConfigSecret"));

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Por favor, insira 'Bearer' [espa�o] e o token JWT",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
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
            new String[]{}
        }
    });

    c.EnableAnnotations();
});

#region Service Injection
// Add services to the container.
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICacheService, MemCacheService>();
builder.Services.AddCorrelationIdGenerator();
builder.Services.AddTransient(typeof(BaseLogger<>));
builder.Services.AddHttpContextAccessor();
//builder.Services.AddScoped(IUsuarioService, UsuarioService);
#endregion

builder.Services.AddMemoryCache();

var app = builder.Build();

app.MapGet("/", () => Results.Text("Bem-vindo � FiapCloudGamesAPI!", "text/plain"));

// Exibe Swagger em todos os ambientes
if (app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FiapCloudGamesAPI v1");
    });
}
;

#region [Middler]
app.UseCorrelationMiddleware();
app.UseInfoUsuarioMiddleware();
app.UseTratamentoDeErrosMiddleware();
#endregion

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
