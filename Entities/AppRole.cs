using System;
using System.Collections.Generic;
using System.Text;

namespace Entities;
public sealed class AppRole
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = [];
}