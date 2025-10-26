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

    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        var role = new ApplicationRole
        {
            Name = adminRole,
            NormalizedName = adminRole.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System",
        };
        await roleManager.CreateAsync(role);

        var claims = new List<Claim>
        {
            new Claim("GetCategoryAll", "true"),
            new Claim("CreateCategory", "true"),
            new Claim("UpdateCategoryAll", "true"),
            new Claim("DeleteCategoryAll", "true"),
            new Claim("GetProductAll", "true"),
            new Claim("CreateProduct", "true"),
            new Claim("UpdateProductAll", "true"),
            new Claim("DeleteProductAll", "true"),
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

        foreach (var claim in claims)
        {
            await roleManager.AddClaimAsync(role, claim);
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
END;";

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
