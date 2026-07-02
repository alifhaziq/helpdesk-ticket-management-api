using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;
using HelpDeskPro.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelpDeskPro.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<HelpDeskProDbContext>();
        var databaseOptions = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var adminOptions = services.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();
        var logger = services.GetRequiredService<ILogger<HelpDeskProDbContext>>();

        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (databaseOptions.EnsureCreated)
                {
                    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                }
                else
                {
                    await dbContext.Database.MigrateAsync(cancellationToken);
                }

                break;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database initialization failed on attempt {Attempt}/{MaxAttempts}; retrying.",
                    attempt,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        if (!adminOptions.Enabled)
        {
            return;
        }

        var email = adminOptions.Email.Trim().ToLowerInvariant();
        var adminExists = await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);
        if (adminExists)
        {
            return;
        }

        var admin = new AppUser
        {
            FullName = adminOptions.FullName.Trim(),
            Email = email,
            Role = UserRole.Admin,
            IsActive = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, adminOptions.Password);

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded default admin account {Email}.", admin.Email);
    }
}
