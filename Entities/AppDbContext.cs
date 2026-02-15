using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Entities;

//dotnet ef migrations add InitialUserSchema `
//  --project Entities `
//  --context AppDbContext `
//  --output-dir Migrations
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(b =>
        {
            b.ToTable("Users");
            b.HasKey(x => x.Id);

            b.Property(x => x.UserName).HasMaxLength(128).IsRequired();
            b.Property(x => x.NormalizedUserName).HasMaxLength(128).IsRequired();

            b.Property(x => x.Email).HasMaxLength(256);
            b.Property(x => x.NormalizedEmail).HasMaxLength(256);

            b.Property(x => x.PasswordHash).HasMaxLength(512);

            b.Property(x => x.IsActive).IsRequired();

            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasIndex(x => x.NormalizedUserName).IsUnique();
            b.HasIndex(x => x.NormalizedEmail);
        });

        modelBuilder.Entity<AppRole>(b =>
        {
            b.ToTable("Roles");
            b.HasKey(x => x.Id);

            b.Property(x => x.Name).HasMaxLength(128).IsRequired();
            b.Property(x => x.NormalizedName).HasMaxLength(128).IsRequired();

            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<AppUserRole>(b =>
        {
            b.ToTable("UserRoles");
            b.HasKey(x => new { x.UserId, x.RoleId });

            b.Property(x => x.CreatedAt).IsRequired();

            b.HasOne(x => x.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(x => x.UserId);

            b.HasOne(x => x.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(x => x.RoleId);
        });
    }
}