using ECommerce.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        var adminEmail = "admin@local";
        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser is null)
        {
            adminUser = new IdentityUser
            {
                UserName = "admin",
                Email = adminEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(adminUser, "Admin123$!");
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(new[]
            {
                new Product { Id = Guid.NewGuid(), Name = "Widget A", Description = "Basic widget", Price = 9.99m, StockQuantity = 100 },
                new Product { Id = Guid.NewGuid(), Name = "Widget B", Description = "Advanced widget", Price = 19.99m, StockQuantity = 50 }
            });
            await db.SaveChangesAsync();
        }
    }
}
