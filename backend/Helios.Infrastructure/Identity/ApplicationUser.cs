
using Microsoft.AspNetCore.Identity;

namespace Helios.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
