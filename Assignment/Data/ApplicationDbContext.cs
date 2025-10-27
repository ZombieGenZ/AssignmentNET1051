using Assignment.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Assignment.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string, IdentityUserClaim<string>, IdentityUserRole<string>, IdentityUserLogin<string>, IdentityRoleClaim<string>, IdentityUserToken<string>>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Combo> Combos { get; set; }
        public DbSet<ComboItem> ComboItems { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<CartItemProductType> CartItemProductTypes { get; set; }
        public DbSet<ProductType> ProductTypes { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<VoucherUser> VoucherUsers { get; set; }
        public DbSet<VoucherProduct> VoucherProducts { get; set; }
        public DbSet<VoucherCombo> VoucherCombos { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderItemProductType> OrderItemProductTypes { get; set; }
        public DbSet<OrderVoucher> OrderVouchers { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<RewardRedemption> RewardRedemptions { get; set; }
        public DbSet<RewardProduct> RewardProducts { get; set; }
        public DbSet<RewardCombo> RewardCombos { get; set; }
        public DbSet<ProductExtra> ProductExtras { get; set; }
        public DbSet<ProductExtraProduct> ProductExtraProducts { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<ConversionUnit> ConversionUnits { get; set; }
        public DbSet<Material> Materials { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ConversionUnit>(entity =>
            {
                entity.HasOne(conversion => conversion.FromUnit)
                      .WithMany(unit => unit.Conversions)
                      .HasForeignKey(conversion => conversion.FromUnitId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(conversion => conversion.ToUnit)
                      .WithMany(unit => unit.ConvertedFrom)
                      .HasForeignKey(conversion => conversion.ToUnitId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
