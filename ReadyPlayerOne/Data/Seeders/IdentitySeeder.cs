using Microsoft.AspNetCore.Identity;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Data.Seeders
{
    public class IdentitySeeder
    {
        public static async Task SeedRolesAsync(
            IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roles = ["User", "Admin"];
            foreach (var role in roles)
            {
                if (await roleManager.RoleExistsAsync(role))
                    continue;

                var result = await roleManager.CreateAsync(new IdentityRole(role));

                if (!result.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
        public static async Task SeedAdminAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByEmailAsync("admin@example.com");
            if (user is null)
                throw new InvalidOperationException("Admin user not found.");
            if (!await userManager.IsInRoleAsync(user, "Admin"))
            {
                var result = await userManager.AddToRoleAsync(user, "Admin");

                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
