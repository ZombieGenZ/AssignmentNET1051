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
        public DbSet<CartItemProductExtra> CartItemProductExtras { get; set; }
        public DbSet<ProductType> ProductTypes { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<VoucherUser> VoucherUsers { get; set; }
        public DbSet<VoucherProduct> VoucherProducts { get; set; }
        public DbSet<VoucherCombo> VoucherCombos { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderItemProductType> OrderItemProductTypes { get; set; }
        public DbSet<OrderItemProductExtra> OrderItemProductExtras { get; set; }
        public DbSet<OrderVoucher> OrderVouchers { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<RewardRedemption> RewardRedemptions { get; set; }
        public DbSet<RewardProduct> RewardProducts { get; set; }
        public DbSet<RewardCombo> RewardCombos { get; set; }
        public DbSet<ProductExtra> ProductExtras { get; set; }
        public DbSet<ProductExtraProduct> ProductExtraProducts { get; set; }
        public DbSet<ProductExtraCombo> ProductExtraCombos { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<ConversionUnit> ConversionUnits { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeDetail> RecipeDetails { get; set; }
        public DbSet<RecipeStep> RecipeSteps { get; set; }
        public DbSet<ReceivingNote> ReceivingNotes { get; set; }
        public DbSet<ReceivingDetail> ReceivingDetails { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }

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

            builder.Entity<Recipe>(entity =>
            {
                entity.HasOne(recipe => recipe.OutputUnit)
                      .WithMany()
                      .HasForeignKey(recipe => recipe.OutputUnitId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<RecipeDetail>(entity =>
            {
                entity.HasOne(detail => detail.Recipe)
                      .WithMany(recipe => recipe.Details)
                      .HasForeignKey(detail => detail.RecipeId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(detail => detail.Material)
                      .WithMany()
                      .HasForeignKey(detail => detail.MaterialId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(detail => detail.Unit)
                      .WithMany()
                      .HasForeignKey(detail => detail.UnitId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<RecipeStep>(entity =>
            {
                entity.HasOne(step => step.Recipe)
                      .WithMany(recipe => recipe.Steps)
                      .HasForeignKey(step => step.RecipeId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Supplier>(entity =>
            {
                entity.HasIndex(supplier => supplier.Code)
                      .IsUnique();

                entity.Property(supplier => supplier.Code)
                      .HasMaxLength(100);

                entity.Property(supplier => supplier.Name)
                      .HasMaxLength(255);

                entity.Property(supplier => supplier.ContactName)
                      .HasMaxLength(255);

                entity.Property(supplier => supplier.PhoneNumber)
                      .HasMaxLength(50);

                entity.Property(supplier => supplier.Email)
                      .HasMaxLength(255);

                entity.Property(supplier => supplier.Address)
                      .HasMaxLength(500);

                entity.Property(supplier => supplier.Notes)
                      .HasMaxLength(1000);
            });

            builder.Entity<ReceivingNote>(entity =>
            {
                entity.HasMany(note => note.Details)
                      .WithOne(detail => detail.ReceivingNote)
                      .HasForeignKey(detail => detail.ReceivingNoteId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(note => note.NoteNumber)
                      .HasMaxLength(100);

                entity.HasOne(note => note.Supplier)
                      .WithMany()
                      .HasForeignKey(note => note.SupplierId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(note => note.SupplierName)
                      .HasMaxLength(255);

                entity.Property(note => note.Status)
                      .HasConversion<int>();
            });

            builder.Entity<ReceivingDetail>(entity =>
            {
                entity.HasOne(detail => detail.Material)
                      .WithMany()
                      .HasForeignKey(detail => detail.MaterialId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(detail => detail.Unit)
                      .WithMany()
                      .HasForeignKey(detail => detail.UnitId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Inventory>(entity =>
            {
                entity.HasOne(inventory => inventory.Material)
                      .WithMany()
                      .HasForeignKey(inventory => inventory.MaterialId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(inventory => new { inventory.MaterialId, inventory.WarehouseId })
                      .IsUnique();
            });
        }
    }
}
