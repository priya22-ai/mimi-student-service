using Microsoft.AspNetCore.Identity;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roles = { "Student", "Staff" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            string staffEmail = "mimi221@iubat.edu";
            string staffPassword = "M!M!221";
            string oldStaffEmail = "shakkhorpaul50@gmail.com";

            var existingStaff = await userManager.FindByEmailAsync(staffEmail);
            if (existingStaff == null)
            {
                // Try to migrate old staff account if it exists
                var oldStaff = await userManager.FindByEmailAsync(oldStaffEmail);
                if (oldStaff != null)
                {
                    oldStaff.UserName = staffEmail;
                    oldStaff.Email = staffEmail;
                    oldStaff.FirstName = "Mimi";
                    oldStaff.LastName = "Staff";
                    oldStaff.EmailConfirmed = true;
                    oldStaff.NormalizedEmail = staffEmail.ToUpperInvariant();
                    oldStaff.NormalizedUserName = staffEmail.ToUpperInvariant();
                    await userManager.UpdateAsync(oldStaff);
                    // Reset password
                    var token = await userManager.GeneratePasswordResetTokenAsync(oldStaff);
                    await userManager.ResetPasswordAsync(oldStaff, token, staffPassword);
                    if (!await userManager.IsInRoleAsync(oldStaff, "Staff"))
                        await userManager.AddToRoleAsync(oldStaff, "Staff");
                }
                else
                {
                    var staffUser = new ApplicationUser
                    {
                        UserName = staffEmail,
                        Email = staffEmail,
                        FirstName = "Mimi",
                        LastName = "Staff",
                        EmailConfirmed = true
                    };
                    var result = await userManager.CreateAsync(staffUser, staffPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(staffUser, "Staff");
                    }
                }
            }
            else
            {
                // Ensure existing staff has correct password and role
                if (!await userManager.IsInRoleAsync(existingStaff, "Staff"))
                    await userManager.AddToRoleAsync(existingStaff, "Staff");

                // Reset password to required value (idempotent)
                var token = await userManager.GeneratePasswordResetTokenAsync(existingStaff);
                await userManager.ResetPasswordAsync(existingStaff, token, staffPassword);
                existingStaff.FirstName = "Mimi";
                existingStaff.LastName = "Staff";
                await userManager.UpdateAsync(existingStaff);
            }

            // Cleanup: remove old staff account if both exist (avoid duplicate)
            var leftoverOld = await userManager.FindByEmailAsync(oldStaffEmail);
            if (leftoverOld != null && staffEmail != oldStaffEmail)
            {
                // If old account still exists separately, delete it
                await userManager.DeleteAsync(leftoverOld);
            }
        }
    }
}
