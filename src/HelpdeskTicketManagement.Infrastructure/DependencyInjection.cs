using HelpdeskTicketManagement.Application.Abstractions;
using HelpdeskTicketManagement.Infrastructure.Data;
using HelpdeskTicketManagement.Infrastructure.Options;
using HelpdeskTicketManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HelpdeskTicketManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AttachmentStorageOptions>(configuration.GetSection(AttachmentStorageOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<HelpdeskDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));
        services.AddScoped<IHelpdeskDbContext>(provider => provider.GetRequiredService<HelpdeskDbContext>());

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
