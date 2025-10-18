using Assignment.Models;
using Assignment.Data;
using Assignment.Services;
using Assignment.Services.PayOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Assignment.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddRazorPages();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddTransient<IEmailSender, EmailSender>();

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

builder.Services.Configure<PayOsOptions>(builder.Configuration.GetSection(PayOsOptions.SectionName));
builder.Services.PostConfigure<PayOsOptions>(options =>
{
    options.ClientId = ResolvePayOsSetting(builder.Configuration, nameof(PayOsOptions.ClientId), options.ClientId);
    options.ApiKey = ResolvePayOsSetting(builder.Configuration, nameof(PayOsOptions.ApiKey), options.ApiKey);
    options.ChecksumKey = ResolvePayOsSetting(builder.Configuration, nameof(PayOsOptions.ChecksumKey), options.ChecksumKey);
    options.BaseUrl = ResolvePayOsSetting(builder.Configuration, nameof(PayOsOptions.BaseUrl), options.BaseUrl, PayOsOptions.DefaultBaseUrl);
});
builder.Services.AddHttpClient<IPayOsService, PayOsService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PayOsOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? PayOsOptions.DefaultBaseUrl : options.BaseUrl;
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    if (!string.IsNullOrWhiteSpace(options.ClientId))
    {
        client.DefaultRequestHeaders.Remove("x-client-id");
        client.DefaultRequestHeaders.Add("x-client-id", options.ClientId);
    }

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.Remove("x-api-key");
        client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
    }
});

static string Normalize(string? value)
    => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

static string ResolvePayOsSetting(IConfiguration configuration, string key, string? currentValue, string? fallback = null)
{
    var normalized = Normalize(currentValue);
    if (!string.IsNullOrWhiteSpace(normalized))
    {
        return normalized;
    }

    var configurationKeys = new[]
    {
        $"{PayOsOptions.SectionName}:{key}",
        $"{PayOsOptions.SectionName}:{key}".ToUpperInvariant(),
        $"PayOS:{key}",
        $"PayOs:{key}"
    };

    foreach (var configurationKey in configurationKeys)
    {
        var value = configuration[configurationKey];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Normalize(value);
        }
    }

    var environmentKeys = new[]
    {
        $"PAYOS_{key}".ToUpperInvariant(),
        $"{PayOsOptions.SectionName}_{key}".ToUpperInvariant()
    };

    foreach (var environmentKey in environmentKeys)
    {
        var value = Environment.GetEnvironmentVariable(environmentKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Normalize(value);
        }
    }

    return Normalize(fallback);
}

builder.Services.AddAuthorization(options =>
{
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
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

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
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var usersWithoutSecurityStamp = await userManager.Users
        .Where(u => string.IsNullOrEmpty(u.SecurityStamp))
        .ToListAsync();

    foreach (var user in usersWithoutSecurityStamp)
    {
        await userManager.UpdateSecurityStampAsync(user);
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var adminRole = "Admin";

    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        var role = new IdentityRole(adminRole);
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
        };

        foreach (var claim in claims)
        {
            await roleManager.AddClaimAsync(role, claim);
        }
    }
}

app.Run();
