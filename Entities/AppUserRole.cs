using System;
using System.Collections.Generic;
using System.Text;

namespace Entities;
public sealed class AppUserRole
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public Guid RoleId { get; set; }
    public AppRole Role { get; set; } = default!;
    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}