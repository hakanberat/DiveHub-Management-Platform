
using Microsoft.AspNetCore.Identity;

namespace DiveHub.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = "";
}
