using System;
using System.Collections.Generic;
using System.Text;

namespace Entities;

public sealed class AppUser
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = "";
    public string NormalizedUserName { get; set; } = "";

    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public bool EmailConfirmed { get; set; }

    public string PasswordHash { get; set; } = ""; // ※自前認証するなら。外部ID連携だけなら不要

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
}
