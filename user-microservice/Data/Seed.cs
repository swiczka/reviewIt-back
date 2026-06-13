using Microsoft.AspNetCore.Identity;
using user_microservice.Models;

namespace user_microservice.Data
{
    public class Seed
    {
        public static async Task SeedUsersAndRolesAsync(IApplicationBuilder applicationBuilder)
        {
            using (var serviceScope = applicationBuilder.ApplicationServices.CreateScope())
            {
                //Roles
                //tutaj komentarz na potrzebę testów
                var roleManager = serviceScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
                if (!await roleManager.RoleExistsAsync(UserRoles.User))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.User));
                if (!await roleManager.RoleExistsAsync(UserRoles.Moderator))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.Moderator));

                var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

                if(userManager.Users.Any())
                {
                    return;
                }

                string adminUserEmail = "admin@mail.com";

                var adminUser = await userManager.FindByEmailAsync(adminUserEmail);
                if (adminUser == null)
                {
                    var newAdminUser = new AppUser()
                    {
                        UserName = "admin",
                        Email = adminUserEmail,
                        EmailConfirmed = true
                    };
                    await userManager.CreateAsync(newAdminUser, "Abc-1234");
                    await userManager.AddToRoleAsync(newAdminUser, UserRoles.Admin);
                }

                string appUserEmail = "user@mail.com";

                var appUser = await userManager.FindByEmailAsync(appUserEmail);
                if (appUser == null)
                {
                    var newAppUser = new AppUser()
                    {
                        UserName = "user",
                        Email = appUserEmail,
                        EmailConfirmed = true,
                    };
                    await userManager.CreateAsync(newAppUser, "Abc-1234");
                    await userManager.AddToRoleAsync(newAppUser, UserRoles.User);
                }

                string modEmail = "mod@mail.com";

                var mod = await userManager.FindByEmailAsync(modEmail);
                if (mod == null)
                {
                    var newMod = new AppUser()
                    {
                        UserName = "mod",
                        Email = modEmail,
                        EmailConfirmed = true,
                    };
                    await userManager.CreateAsync(newMod, "Abc-12345");
                    await userManager.AddToRoleAsync(newMod, UserRoles.User);
                    await userManager.AddToRoleAsync(newMod, UserRoles.Moderator);
                }
            }
        }
    }
}
