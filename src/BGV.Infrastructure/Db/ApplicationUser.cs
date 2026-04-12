using Microsoft.AspNetCore.Identity;

namespace BGV.Infrastructure.Db;

public class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
    // Add additional properties as needed
}