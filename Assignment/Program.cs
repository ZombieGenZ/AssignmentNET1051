using Assignment.Models;
using Assignment.Data;
using Assignment.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Assignment.Extensions;
using Assignment.Options;
using Assignment.Services.Payments;
using Assignment.Services.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddRazorPages();

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

builder.Services.Configure<PayOsOptions>(builder.Configuration.GetSection("PayOs"));
builder.Services.AddHttpClient<IPayOsService, PayOsService>();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
    })
    .AddFacebook(options =>
    {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"] ?? string.Empty;
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ?? string.Empty;
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"] ?? string.Empty;
    })
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Discord:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"] ?? string.Empty;
        options.Scope.Add("identify");
        options.Scope.Add("email");
    });

builder.Services.AddHttpClient("AssignmentApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:443/");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireClaim("superadmin", "true"));

    options.AddPolicy("GetCategoryPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("GetCategoryAll") ||
            (ctx.User.HasPermission("GetCategory") &&
             ctx.Resource is Category cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateCategoryPolicy", policy =>
        policy.RequireClaim("CreateCategory")
    );

    options.AddPolicy("UpdateCategoryPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateCategoryAll") ||
            (ctx.User.HasPermission("UpdateCategory") &&
             ctx.Resource is Category cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("DeleteCategoryPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteCategoryAll") ||
            (ctx.User.HasPermission("DeleteCategory") &&
             ctx.Resource is Category cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("GetProductPolicy", policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasPermission("GetProductAll") ||
        (ctx.User.HasPermission("GetProduct") &&
         ctx.Resource is Product cat &&
         cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
    )
);

    options.AddPolicy("CreateProductPolicy", policy =>
        policy.RequireClaim("CreateProduct")
    );

    options.AddPolicy("UpdateProductPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateProductAll") ||
            (ctx.User.HasPermission("UpdateProduct") &&
             ctx.Resource is Product cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("DeleteProductPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteProductAll") ||
            (ctx.User.HasPermission("DeleteProduct") &&
             ctx.Resource is Product cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("GetUnitPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("GetUnitAll") ||
            (ctx.User.HasPermission("GetUnit") &&
             ctx.Resource is Unit unit &&
             unit.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateUnitPolicy", policy =>
        policy.RequireClaim("CreateUnit")
    );

    options.AddPolicy("UpdateUnitPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateUnitAll") ||
            (ctx.User.HasPermission("UpdateUnit") &&
             ctx.Resource is Unit unit &&
             unit.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("DeleteUnitPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteUnitAll") ||
            (ctx.User.HasPermission("DeleteUnit") &&
             ctx.Resource is Unit unit &&
             unit.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("GetProductExtraPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("GetProductExtraAll") ||
            (ctx.User.HasPermission("GetProductExtra") &&
             ctx.Resource is ProductExtra extra &&
             extra.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateProductExtraPolicy", policy =>
        policy.RequireClaim("CreateProductExtra")
    );

    options.AddPolicy("UpdateProductExtraPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateProductExtraAll") ||
            (ctx.User.HasPermission("UpdateProductExtra") &&
             ctx.Resource is ProductExtra extra &&
             extra.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("DeleteProductExtraPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteProductExtraAll") ||
            (ctx.User.HasPermission("DeleteProductExtra") &&
             ctx.Resource is ProductExtra extra &&
             extra.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("GetMaterialPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("GetMaterialAll") ||
            (ctx.User.HasPermission("GetMaterial") &&
             ctx.Resource is Material material &&
             material.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateMaterialPolicy", policy =>
        policy.RequireClaim("CreateMaterial")
    );

    options.AddPolicy("UpdateMaterialPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateMaterialAll") ||
            (ctx.User.HasPermission("UpdateMaterial") &&
             ctx.Resource is Material material &&
             material.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateReceivingPolicy", policy =>
        policy.RequireClaim("CreateReceiving")
    );

    options.AddPolicy("ViewInventoryPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("ViewInventoryAll") ||
            ctx.User.HasPermission("ViewInventory")
        )
    );

    options.AddPolicy("DeleteMaterialPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteMaterialAll") ||
            (ctx.User.HasPermission("DeleteMaterial") &&
             ctx.Resource is Material material &&
             material.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("GetRecipePolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("GetRecipeAll") ||
            (ctx.User.HasPermission("GetRecipe") &&
             ctx.Resource is Recipe recipe &&
             recipe.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateRecipePolicy", policy =>
        policy.RequireClaim("CreateRecipe")
    );

    options.AddPolicy("UpdateRecipePolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateRecipeAll") ||
            (ctx.User.HasPermission("UpdateRecipe") &&
             ctx.Resource is Recipe recipe &&
             recipe.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("DeleteRecipePolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteRecipeAll") ||
            (ctx.User.HasPermission("DeleteRecipe") &&
             ctx.Resource is Recipe recipe &&
             recipe.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("GetComboPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("GetComboAll") ||
            (ctx.User.HasPermission("GetCombo") &&
             ctx.Resource is Combo cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateComboPolicy", policy =>
        policy.RequireClaim("CreateCombo")
    );

    options.AddPolicy("UpdateComboPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateComboAll") ||
            (ctx.User.HasPermission("UpdateCombo") &&
             ctx.Resource is Combo cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("DeleteComboPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteComboAll") ||
            (ctx.User.HasPermission("DeleteCombo") &&
             ctx.Resource is Combo cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("GetVoucherPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("GetVoucherAll") ||
            (ctx.User.HasPermission("GetVoucher") &&
             ctx.Resource is Voucher cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateVoucherPolicy", policy =>
        policy.RequireClaim("CreateVoucher")
    );

    options.AddPolicy("UpdateVoucherPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateVoucherAll") ||
            (ctx.User.HasPermission("UpdateVoucher") &&
             ctx.Resource is Voucher cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("DeleteVoucherPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteVoucherAll") ||
            (ctx.User.HasPermission("DeleteVoucher") &&
             ctx.Resource is Voucher cat &&
             cat.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("GetRewardPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("GetRewardAll") ||
            (ctx.User.HasPermission("GetReward") &&
             ctx.Resource is Reward reward &&
             reward.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("CreateRewardPolicy", policy =>
        policy.RequireClaim("CreateReward")
    );

    options.AddPolicy("UpdateRewardPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("UpdateRewardAll") ||
            (ctx.User.HasPermission("UpdateReward") &&
             ctx.Resource is Reward reward &&
             reward.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("DeleteRewardPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasPermission("DeleteRewardAll") ||
            (ctx.User.HasPermission("DeleteReward") &&
             ctx.Resource is Reward reward &&
             reward.CreateBy == ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
        )
    );

    options.AddPolicy("ViewStatisticsPolicy", policy =>
        policy.RequireClaim("ViewStatistics")
    );
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while applying database migrations.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCodeHandler", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await EnsureRoleMetadataColumnsAsync(dbContext);
    await EnsureVoucherMinimumRankColumnAsync(dbContext);
    await EnsureProductTypeSelectionTablesAsync(dbContext);
    await EnsureProductExtraTablesAsync(dbContext);
    await EnsureMaterialManagementTablesAsync(dbContext);
    await EnsureRecipeManagementTablesAsync(dbContext);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var usersWithoutSecurityStamp = await userManager.Users
        .Where(u => string.IsNullOrEmpty(u.SecurityStamp))
        .ToListAsync();

    foreach (var user in usersWithoutSecurityStamp)
    {
        await userManager.UpdateSecurityStampAsync(user);
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var adminRole = "Admin";

    var requiredClaims = new List<Claim>
    {
        new Claim("GetCategoryAll", "true"),
        new Claim("CreateCategory", "true"),
        new Claim("UpdateCategoryAll", "true"),
        new Claim("DeleteCategoryAll", "true"),
        new Claim("GetProductAll", "true"),
        new Claim("CreateProduct", "true"),
        new Claim("UpdateProductAll", "true"),
        new Claim("DeleteProductAll", "true"),
        new Claim("GetProductExtraAll", "true"),
        new Claim("CreateProductExtra", "true"),
        new Claim("UpdateProductExtraAll", "true"),
        new Claim("DeleteProductExtraAll", "true"),
        new Claim("GetRecipeAll", "true"),
        new Claim("CreateRecipe", "true"),
        new Claim("UpdateRecipeAll", "true"),
        new Claim("DeleteRecipeAll", "true"),
        new Claim("GetComboAll", "true"),
        new Claim("CreateCombo", "true"),
        new Claim("UpdateComboAll", "true"),
        new Claim("DeleteComboAll", "true"),
        new Claim("GetVoucherAll", "true"),
        new Claim("CreateVoucher", "true"),
        new Claim("UpdateVoucherAll", "true"),
        new Claim("DeleteVoucherAll", "true"),
        new Claim("GetOrderAll", "true"),
        new Claim("ChangeOrderStatusAll", "true"),
        new Claim("ViewStatistics", "true"),
        new Claim("DeleteEvaluate", "true"),
        new Claim("GetRewardAll", "true"),
        new Claim("CreateReward", "true"),
        new Claim("UpdateRewardAll", "true"),
        new Claim("DeleteRewardAll", "true"),
        new Claim("ViewTopUserAll", "true"),
        new Claim("ViewCustomerAll", "true"),
    };

    var adminRoleEntity = await roleManager.FindByNameAsync(adminRole);
    if (adminRoleEntity == null)
    {
        adminRoleEntity = new ApplicationRole
        {
            Name = adminRole,
            NormalizedName = adminRole.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System",
        };

        await roleManager.CreateAsync(adminRoleEntity);
    }

    var existingClaims = await roleManager.GetClaimsAsync(adminRoleEntity);
    foreach (var claim in requiredClaims)
    {
        var hasClaim = existingClaims.Any(existing =>
            string.Equals(existing.Type, claim.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Value, claim.Value, StringComparison.OrdinalIgnoreCase));

        if (!hasClaim)
        {
            await roleManager.AddClaimAsync(adminRoleEntity, claim);
        }
    }
}

app.Run();

static async Task EnsureProductTypeSelectionTablesAsync(ApplicationDbContext context)
{
    const string ensureSql = @"
IF OBJECT_ID(N'dbo.CartItemProductTypes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CartItemProductTypes]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        [CartItemId] BIGINT NOT NULL,
        [ProductTypeId] BIGINT NOT NULL,
        [Quantity] INT NOT NULL,
        [UnitPrice] FLOAT NOT NULL,
        CONSTRAINT [PK_CartItemProductTypes] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_CartItemProductTypes_CartItemId] ON [dbo].[CartItemProductTypes]([CartItemId]);
    CREATE INDEX [IX_CartItemProductTypes_ProductTypeId] ON [dbo].[CartItemProductTypes]([ProductTypeId]);

    ALTER TABLE [dbo].[CartItemProductTypes] WITH CHECK
        ADD CONSTRAINT [FK_CartItemProductTypes_CartItems_CartItemId]
        FOREIGN KEY([CartItemId]) REFERENCES [dbo].[CartItems]([Id]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[CartItemProductTypes] WITH CHECK
        ADD CONSTRAINT [FK_CartItemProductTypes_ProductTypes_ProductTypeId]
        FOREIGN KEY([ProductTypeId]) REFERENCES [dbo].[ProductTypes]([Id]) ON DELETE CASCADE;
END;

IF OBJECT_ID(N'dbo.OrderItemProductTypes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderItemProductTypes]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        [OrderItemId] BIGINT NOT NULL,
        [ProductTypeId] BIGINT NOT NULL,
        [Quantity] INT NOT NULL,
        [UnitPrice] FLOAT NOT NULL,
        CONSTRAINT [PK_OrderItemProductTypes] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_OrderItemProductTypes_OrderItemId] ON [dbo].[OrderItemProductTypes]([OrderItemId]);
    CREATE INDEX [IX_OrderItemProductTypes_ProductTypeId] ON [dbo].[OrderItemProductTypes]([ProductTypeId]);

    ALTER TABLE [dbo].[OrderItemProductTypes] WITH CHECK
        ADD CONSTRAINT [FK_OrderItemProductTypes_OrderItems_OrderItemId]
        FOREIGN KEY([OrderItemId]) REFERENCES [dbo].[OrderItems]([Id]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[OrderItemProductTypes] WITH CHECK
        ADD CONSTRAINT [FK_OrderItemProductTypes_ProductTypes_ProductTypeId]
        FOREIGN KEY([ProductTypeId]) REFERENCES [dbo].[ProductTypes]([Id]) ON DELETE CASCADE;
END;

IF COL_LENGTH(N'dbo.ComboItems', N'ProductTypeId') IS NULL
BEGIN
    ALTER TABLE [dbo].[ComboItems]
    ADD [ProductTypeId] BIGINT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ComboItems_ProductTypeId'
            AND object_id = OBJECT_ID(N'dbo.ComboItems')
    )
    BEGIN
        CREATE INDEX [IX_ComboItems_ProductTypeId]
        ON [dbo].[ComboItems]([ProductTypeId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_ComboItems_ProductTypes_ProductTypeId'
            AND parent_object_id = OBJECT_ID(N'dbo.ComboItems')
    )
    BEGIN
        ALTER TABLE [dbo].[ComboItems] WITH CHECK
            ADD CONSTRAINT [FK_ComboItems_ProductTypes_ProductTypeId]
            FOREIGN KEY([ProductTypeId]) REFERENCES [dbo].[ProductTypes]([Id]);
    END;
END;";

    await context.Database.ExecuteSqlRawAsync(ensureSql);
}

static async Task EnsureProductExtraTablesAsync(ApplicationDbContext context)
{
    const string ensureSql = @"
IF OBJECT_ID(N'dbo.ProductExtras', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductExtras]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [ImageUrl] NVARCHAR(1000) NULL,
        [Price] DECIMAL(18,2) NOT NULL,
        [Stock] INT NOT NULL,
        [DiscountType] INT NOT NULL,
        [Discount] DECIMAL(18,2) NULL,
        [Calories] INT NOT NULL,
        [Ingredients] NVARCHAR(2000) NOT NULL,
        [IsSpicy] BIT NOT NULL,
        [IsVegetarian] BIT NOT NULL,
        [IsPublish] BIT NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_ProductExtras] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;

IF OBJECT_ID(N'dbo.ProductExtraProducts', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductExtraProducts]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [ProductExtraId] BIGINT NOT NULL,
        [ProductId] BIGINT NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_ProductExtraProducts] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_ProductExtraProducts_ProductExtraId] ON [dbo].[ProductExtraProducts]([ProductExtraId]);
    CREATE INDEX [IX_ProductExtraProducts_ProductId] ON [dbo].[ProductExtraProducts]([ProductId]);
    CREATE UNIQUE INDEX [IX_ProductExtraProducts_ProductExtraId_ProductId] ON [dbo].[ProductExtraProducts]([ProductExtraId], [ProductId]);

    ALTER TABLE [dbo].[ProductExtraProducts] WITH CHECK
        ADD CONSTRAINT [FK_ProductExtraProducts_ProductExtras_ProductExtraId]
        FOREIGN KEY([ProductExtraId]) REFERENCES [dbo].[ProductExtras]([Id]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[ProductExtraProducts] WITH CHECK
        ADD CONSTRAINT [FK_ProductExtraProducts_Products_ProductId]
        FOREIGN KEY([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE;
END;

IF OBJECT_ID(N'dbo.ProductExtraCombos', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductExtraCombos]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [ProductExtraId] BIGINT NOT NULL,
        [ComboId] BIGINT NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_ProductExtraCombos] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_ProductExtraCombos_ProductExtraId] ON [dbo].[ProductExtraCombos]([ProductExtraId]);
    CREATE INDEX [IX_ProductExtraCombos_ComboId] ON [dbo].[ProductExtraCombos]([ComboId]);
    CREATE UNIQUE INDEX [IX_ProductExtraCombos_ProductExtraId_ComboId] ON [dbo].[ProductExtraCombos]([ProductExtraId], [ComboId]);

    ALTER TABLE [dbo].[ProductExtraCombos] WITH CHECK
        ADD CONSTRAINT [FK_ProductExtraCombos_ProductExtras_ProductExtraId]
        FOREIGN KEY([ProductExtraId]) REFERENCES [dbo].[ProductExtras]([Id]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[ProductExtraCombos] WITH CHECK
        ADD CONSTRAINT [FK_ProductExtraCombos_Combos_ComboId]
        FOREIGN KEY([ComboId]) REFERENCES [dbo].[Combos]([Id]) ON DELETE CASCADE;
END;
";

    await context.Database.ExecuteSqlRawAsync(ensureSql);
}

static async Task EnsureMaterialManagementTablesAsync(ApplicationDbContext context)
{
    const string ensureSql = @"
IF OBJECT_ID(N'dbo.Units', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Units]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(255) NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Units] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;

IF OBJECT_ID(N'dbo.ConversionUnits', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ConversionUnits]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [FromUnitId] BIGINT NOT NULL,
        [ToUnitId] BIGINT NOT NULL,
        [ConversionRate] DECIMAL(18,6) NOT NULL,
        [Description] NVARCHAR(255) NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_ConversionUnits] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_ConversionUnits_FromUnitId] ON [dbo].[ConversionUnits]([FromUnitId]);
    CREATE INDEX [IX_ConversionUnits_ToUnitId] ON [dbo].[ConversionUnits]([ToUnitId]);
    CREATE UNIQUE INDEX [IX_ConversionUnits_FromUnitId_ToUnitId] ON [dbo].[ConversionUnits]([FromUnitId], [ToUnitId]);

    ALTER TABLE [dbo].[ConversionUnits] WITH CHECK
        ADD CONSTRAINT [FK_ConversionUnits_Units_FromUnitId]
        FOREIGN KEY([FromUnitId]) REFERENCES [dbo].[Units]([Id]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[ConversionUnits] WITH CHECK
        ADD CONSTRAINT [FK_ConversionUnits_Units_ToUnitId]
        FOREIGN KEY([ToUnitId]) REFERENCES [dbo].[Units]([Id]) ON DELETE CASCADE;
END;

IF OBJECT_ID(N'dbo.Materials', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Materials]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [Code] NVARCHAR(100) NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [UnitId] BIGINT NOT NULL,
        [MinStockLevel] DECIMAL(18,4) NOT NULL,
        [Price] DECIMAL(18,2) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Materials] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_Materials_Code] ON [dbo].[Materials]([Code]);
    CREATE INDEX [IX_Materials_Name] ON [dbo].[Materials]([Name]);
    CREATE INDEX [IX_Materials_UnitId] ON [dbo].[Materials]([UnitId]);

    ALTER TABLE [dbo].[Materials] WITH CHECK
        ADD CONSTRAINT [FK_Materials_Units_UnitId]
        FOREIGN KEY([UnitId]) REFERENCES [dbo].[Units]([Id]) ON DELETE CASCADE;
END;

IF OBJECT_ID(N'dbo.ReceivingNotes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ReceivingNotes]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [NoteNumber] NVARCHAR(100) NOT NULL,
        [Date] DATE NOT NULL,
        [SupplierId] NVARCHAR(100) NULL,
        [SupplierName] NVARCHAR(255) NULL,
        [WarehouseId] BIGINT NULL,
        [Status] INT NOT NULL,
        [IsStockApplied] BIT NOT NULL,
        [CompletedAt] DATETIME2 NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_ReceivingNotes] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_ReceivingNotes_NoteNumber] ON [dbo].[ReceivingNotes]([NoteNumber]);
END;

IF OBJECT_ID(N'dbo.ReceivingDetails', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ReceivingDetails]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [ReceivingNoteId] BIGINT NOT NULL,
        [MaterialId] BIGINT NOT NULL,
        [Quantity] DECIMAL(18,4) NOT NULL,
        [UnitId] BIGINT NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [BaseQuantity] DECIMAL(18,4) NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_ReceivingDetails] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_ReceivingDetails_ReceivingNoteId] ON [dbo].[ReceivingDetails]([ReceivingNoteId]);
    CREATE INDEX [IX_ReceivingDetails_MaterialId] ON [dbo].[ReceivingDetails]([MaterialId]);
    CREATE INDEX [IX_ReceivingDetails_UnitId] ON [dbo].[ReceivingDetails]([UnitId]);

    ALTER TABLE [dbo].[ReceivingDetails] WITH CHECK
        ADD CONSTRAINT [FK_ReceivingDetails_ReceivingNotes_ReceivingNoteId]
        FOREIGN KEY([ReceivingNoteId]) REFERENCES [dbo].[ReceivingNotes]([Id]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[ReceivingDetails] WITH CHECK
        ADD CONSTRAINT [FK_ReceivingDetails_Materials_MaterialId]
        FOREIGN KEY([MaterialId]) REFERENCES [dbo].[Materials]([Id]);

    ALTER TABLE [dbo].[ReceivingDetails] WITH CHECK
        ADD CONSTRAINT [FK_ReceivingDetails_Units_UnitId]
        FOREIGN KEY([UnitId]) REFERENCES [dbo].[Units]([Id]);
END;

IF OBJECT_ID(N'dbo.Inventories', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Inventories]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [MaterialId] BIGINT NOT NULL,
        [WarehouseId] BIGINT NULL,
        [CurrentStock] DECIMAL(18,4) NOT NULL,
        [LastUpdated] DATETIME2 NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Inventories] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE INDEX [IX_Inventories_MaterialId_WarehouseId] ON [dbo].[Inventories]([MaterialId], [WarehouseId]);

    ALTER TABLE [dbo].[Inventories] WITH CHECK
        ADD CONSTRAINT [FK_Inventories_Materials_MaterialId]
        FOREIGN KEY([MaterialId]) REFERENCES [dbo].[Materials]([Id]);
END;";

    await context.Database.ExecuteSqlRawAsync(ensureSql);

    const string ensureMaterialDescriptionColumnSql = @"
IF COL_LENGTH(N'dbo.Materials', N'Description') IS NULL
BEGIN
    ALTER TABLE [dbo].[Materials]
        ADD [Description] NVARCHAR(500) NULL;
END;";

    await context.Database.ExecuteSqlRawAsync(ensureMaterialDescriptionColumnSql);
}

static async Task EnsureRecipeManagementTablesAsync(ApplicationDbContext context)
{
    const string ensureSql = @"
IF OBJECT_ID(N'dbo.Recipes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Recipes]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [OutputUnitId] BIGINT NOT NULL,
        [PreparationTime] INT NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Recipes] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_Recipes_Name] ON [dbo].[Recipes]([Name]);
    CREATE INDEX [IX_Recipes_OutputUnitId] ON [dbo].[Recipes]([OutputUnitId]);

    ALTER TABLE [dbo].[Recipes] WITH CHECK
        ADD CONSTRAINT [FK_Recipes_Units_OutputUnitId]
        FOREIGN KEY([OutputUnitId]) REFERENCES [dbo].[Units]([Id]);
END;

IF OBJECT_ID(N'dbo.RecipeDetails', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RecipeDetails]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [RecipeId] BIGINT NOT NULL,
        [MaterialId] BIGINT NOT NULL,
        [Quantity] DECIMAL(18,4) NOT NULL,
        [UnitId] BIGINT NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_RecipeDetails] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_RecipeDetails_RecipeId] ON [dbo].[RecipeDetails]([RecipeId]);
    CREATE INDEX [IX_RecipeDetails_MaterialId] ON [dbo].[RecipeDetails]([MaterialId]);
    CREATE INDEX [IX_RecipeDetails_UnitId] ON [dbo].[RecipeDetails]([UnitId]);

    ALTER TABLE [dbo].[RecipeDetails] WITH CHECK
        ADD CONSTRAINT [FK_RecipeDetails_Recipes_RecipeId]
        FOREIGN KEY([RecipeId]) REFERENCES [dbo].[Recipes]([Id]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[RecipeDetails] WITH CHECK
        ADD CONSTRAINT [FK_RecipeDetails_Materials_MaterialId]
        FOREIGN KEY([MaterialId]) REFERENCES [dbo].[Materials]([Id]);

    ALTER TABLE [dbo].[RecipeDetails] WITH CHECK
        ADD CONSTRAINT [FK_RecipeDetails_Units_UnitId]
        FOREIGN KEY([UnitId]) REFERENCES [dbo].[Units]([Id]);
END;

IF OBJECT_ID(N'dbo.RecipeSteps', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RecipeSteps]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [RecipeId] BIGINT NOT NULL,
        [StepOrder] INT NOT NULL,
        [Description] NVARCHAR(2000) NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_RecipeSteps] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_RecipeSteps_RecipeId] ON [dbo].[RecipeSteps]([RecipeId]);
    CREATE INDEX [IX_RecipeSteps_StepOrder] ON [dbo].[RecipeSteps]([StepOrder]);

    ALTER TABLE [dbo].[RecipeSteps] WITH CHECK
        ADD CONSTRAINT [FK_RecipeSteps_Recipes_RecipeId]
        FOREIGN KEY([RecipeId]) REFERENCES [dbo].[Recipes]([Id]) ON DELETE CASCADE;
END;
";

    await context.Database.ExecuteSqlRawAsync(ensureSql);
}

static async Task EnsureRoleMetadataColumnsAsync(ApplicationDbContext context)
{
    const string ensureRoleMetadataSql = @"
IF COL_LENGTH(N'dbo.AspNetRoles', N'CreatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [CreatedAt] datetime2 NOT NULL
        CONSTRAINT [DF_AspNetRoles_CreatedAt] DEFAULT (SYSUTCDATETIME()) WITH VALUES;
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [CreatedBy] nvarchar(450) NULL;
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [IsDeleted] bit NOT NULL
        CONSTRAINT [DF_AspNetRoles_IsDeleted] DEFAULT ((0)) WITH VALUES;
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [UpdatedAt] datetime2 NULL;
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'UpdatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [UpdatedBy] nvarchar(450) NULL;
END";

    await context.Database.ExecuteSqlRawAsync(ensureRoleMetadataSql);
}

static async Task EnsureVoucherMinimumRankColumnAsync(ApplicationDbContext context)
{
    const string ensureVoucherMinimumRankSql = @"
IF COL_LENGTH(N'dbo.Vouchers', N'MinimumRank') IS NULL
BEGIN
    ALTER TABLE [dbo].[Vouchers]
    ADD [MinimumRank] int NULL;
END";

    await context.Database.ExecuteSqlRawAsync(ensureVoucherMinimumRankSql);
}
