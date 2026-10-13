using System.Text.Json;
using System.Text.Json.Serialization;
using Banking.Api.Errors;
using Banking.Api.Security;
using Banking.Business.Services;
using Banking.Data.Repositories;
using Banking.Domain.Repositories;
using Banking.Domain.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Banking.Api;

public class Program
{
    public static void Main(string[] args) => CreateApplication(args).Run();

    public static WebApplication CreateApplication(string[] args, Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(Program).Assembly.FullName
        });

        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false)));
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
        builder.Services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
        builder.Services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        builder.Services.AddSingleton<ICustomerService, CustomerService>();
        builder.Services.AddSingleton<IAccountService, AccountService>();
        builder.Services.AddSingleton<IAccessService, AccessService>();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentSession, CurrentSession>();
        builder.Services.AddAuthentication(BearerSessionHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BearerSessionHandler>(BearerSessionHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        configureBuilder?.Invoke(builder);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            await next(context);
        });
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        // The React development proxy targets HTTP, even when the HTTPS profile is selected.
        if (!app.Environment.IsDevelopment())
            app.UseHttpsRedirection();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            // Match Java: a supplied invalid token is rejected even on public routes.
            var result = await context.AuthenticateAsync();
            if (result.Failure is not null)
            {
                await context.ChallengeAsync();
                return;
            }
            await next(context);
        });
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }
}
