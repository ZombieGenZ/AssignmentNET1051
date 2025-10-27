using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Assignment.Data;
using Assignment.Extensions;
using Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/recipes")]
    [Authorize]
    public class RecipeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public RecipeController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetRecipes([FromQuery] RecipeQuery query)
        {
            var canGetAll = User.HasPermission("GetRecipeAll");
            var canGetOwn = User.HasPermission("GetRecipe");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            query ??= new RecipeQuery();

            IQueryable<Recipe> recipesQuery = _context.Recipes
                .AsNoTracking()
                .Include(r => r.OutputUnit)
                .Include(r => r.Details)
                    .ThenInclude(d => d.Material)
                        .ThenInclude(m => m.Unit)
                .Include(r => r.Details)
                    .ThenInclude(d => d.Unit)
                .Where(r => !r.IsDeleted);

            if (!canGetAll)
            {
                recipesQuery = recipesQuery.Where(r => r.CreateBy == CurrentUserId);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var normalizedSearch = query.Search.Trim().ToLowerInvariant();
                recipesQuery = recipesQuery.Where(r =>
                    r.Name.ToLower().Contains(normalizedSearch) ||
                    (r.Description != null && r.Description.ToLower().Contains(normalizedSearch)));
            }

            if (query.OutputUnitId.HasValue)
            {
                recipesQuery = recipesQuery.Where(r => r.OutputUnitId == query.OutputUnitId.Value);
            }

            var recipes = await recipesQuery
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .ToListAsync();

            var responses = await MapToResponsesAsync(recipes);
            return Ok(responses);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetRecipe(long id)
        {
            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.OutputUnit)
                .Include(r => r.Details)
                    .ThenInclude(d => d.Material)
                        .ThenInclude(m => m.Unit)
                .Include(r => r.Details)
                    .ThenInclude(d => d.Unit)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (recipe == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, recipe, "GetRecipePolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var response = await MapToResponseAsync(recipe);
            return Ok(response);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateRecipePolicy")]
        public async Task<IActionResult> CreateRecipe([FromBody] RecipeRequest request)
        {
            var validation = await ValidateRecipeRequestAsync(request);
            if (!ModelState.IsValid || validation == null)
            {
                return ValidationProblem(ModelState);
            }

            var recipe = new Recipe
            {
                Name = validation.Name,
                Description = validation.Description,
                OutputUnitId = validation.OutputUnitId!.Value,
                PreparationTime = validation.PreparationTime,
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            foreach (var detail in validation.Details)
            {
                var recipeDetail = new RecipeDetail
                {
                    MaterialId = detail.Material.Id,
                    UnitId = detail.Unit.Id,
                    Quantity = detail.Quantity,
                    CreateBy = CurrentUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = null,
                    DeletedAt = null,
                    IsDeleted = false
                };

                recipe.Details.Add(recipeDetail);
            }

            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();

            await LoadRecipeAsync(recipe);
            var response = await MapToResponseAsync(recipe);

            return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, response);
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRecipe(long id, [FromBody] RecipeRequest request)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Details)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (recipe == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, recipe, "UpdateRecipePolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var validation = await ValidateRecipeRequestAsync(request, recipe);
            if (!ModelState.IsValid || validation == null)
            {
                return ValidationProblem(ModelState);
            }

            recipe.Name = validation.Name;
            recipe.Description = validation.Description;
            recipe.OutputUnitId = validation.OutputUnitId!.Value;
            recipe.PreparationTime = validation.PreparationTime;
            recipe.UpdatedAt = DateTime.Now;

            SyncRecipeDetails(recipe, validation.Details);

            await _context.SaveChangesAsync();

            await LoadRecipeAsync(recipe);
            var response = await MapToResponseAsync(recipe);
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRecipe(long id)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Details)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (recipe == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, recipe, "DeleteRecipePolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            recipe.IsDeleted = true;
            recipe.DeletedAt = DateTime.Now;
            recipe.UpdatedAt = DateTime.Now;

            foreach (var detail in recipe.Details.Where(d => !d.IsDeleted))
            {
                detail.IsDeleted = true;
                detail.DeletedAt = DateTime.Now;
                detail.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("calculate-cost")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculateRecipeCost([FromBody] RecipeRequest request)
        {
            var hasPermission = User.HasAnyPermission(
                "CreateRecipe",
                "UpdateRecipe",
                "UpdateRecipeAll",
                "GetRecipe",
                "GetRecipeAll");

            if (!hasPermission)
            {
                return Forbid();
            }

            var validation = await ValidateRecipeRequestAsync(request, skipMetadataValidation: true);
            if (!ModelState.IsValid || validation == null)
            {
                return ValidationProblem(ModelState);
            }

            var costResponse = BuildCostPreview(validation);
            return Ok(costResponse);
        }

        private async Task<RecipeResponse> MapToResponseAsync(Recipe recipe)
        {
            var responses = await MapToResponsesAsync(new List<Recipe> { recipe });
            return responses.First();
        }

        private async Task<List<RecipeResponse>> MapToResponsesAsync(IReadOnlyCollection<Recipe> recipes)
        {
            var responseList = new List<RecipeResponse>();
            if (recipes.Count == 0)
            {
                return responseList;
            }

            var allDetails = recipes
                .SelectMany(r => r.Details.Where(d => !d.IsDeleted))
                .ToList();

            var conversionPairs = allDetails
                .Where(d => d.Material != null && d.UnitId != (d.Material?.UnitId ?? 0))
                .Select(d => (From: d.UnitId, To: d.Material!.UnitId))
                .Distinct()
                .ToList();

            var fromUnitIds = conversionPairs.Select(p => p.From).Distinct().ToList();
            var toUnitIds = conversionPairs.Select(p => p.To).Distinct().ToList();

            var conversions = new List<ConversionUnit>();
            if (fromUnitIds.Count > 0 && toUnitIds.Count > 0)
            {
                conversions = await _context.ConversionUnits
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted && fromUnitIds.Contains(c.FromUnitId) && toUnitIds.Contains(c.ToUnitId))
                    .ToListAsync();
            }

            var conversionLookup = conversions
                .ToDictionary(c => (c.FromUnitId, c.ToUnitId), c => c.ConversionRate);

            foreach (var recipe in recipes)
            {
                decimal totalCost = 0m;
                var details = new List<RecipeDetailResponse>();

                foreach (var detail in recipe.Details.Where(d => !d.IsDeleted))
                {
                    if (detail.Material == null || detail.Unit == null || detail.Material.Unit == null)
                    {
                        continue;
                    }

                    var conversionRate = 1m;
                    if (detail.UnitId != detail.Material.UnitId)
                    {
                        if (!conversionLookup.TryGetValue((detail.UnitId, detail.Material.UnitId), out conversionRate))
                        {
                            conversionRate = 0m;
                        }
                    }

                    var detailResponse = CreateDetailResponse(detail.Id, detail.Material, detail.Unit, detail.Quantity, conversionRate);
                    details.Add(detailResponse);
                    totalCost += detailResponse.Cost;
                }

                responseList.Add(new RecipeResponse
                {
                    Id = recipe.Id,
                    Name = recipe.Name,
                    Description = recipe.Description,
                    OutputUnitId = recipe.OutputUnitId,
                    OutputUnitName = recipe.OutputUnit?.Name ?? string.Empty,
                    PreparationTime = recipe.PreparationTime,
                    TotalCost = totalCost,
                    CreatedAt = recipe.CreatedAt,
                    UpdatedAt = recipe.UpdatedAt,
                    Details = details
                        .OrderBy(d => d.MaterialName)
                        .ThenBy(d => d.Id)
                        .ToList()
                });
            }

            return responseList
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .ToList();
        }

        private RecipeDetailResponse CreateDetailResponse(long id, Material material, Unit unit, decimal quantity, decimal conversionRate)
        {
            var convertedQuantity = quantity * conversionRate;
            var cost = convertedQuantity * material.Price;

            return new RecipeDetailResponse
            {
                Id = id,
                MaterialId = material.Id,
                MaterialCode = material.Code,
                MaterialName = material.Name,
                Quantity = quantity,
                UnitId = unit.Id,
                UnitName = unit.Name,
                ConversionRate = conversionRate,
                ConvertedQuantity = convertedQuantity,
                BaseUnitId = material.UnitId,
                BaseUnitName = material.Unit?.Name ?? string.Empty,
                MaterialPrice = material.Price,
                Cost = cost
            };
        }

        private async Task LoadRecipeAsync(Recipe recipe)
        {
            await _context.Entry(recipe)
                .Reference(r => r.OutputUnit)
                .LoadAsync();

            await _context.Entry(recipe)
                .Collection(r => r.Details)
                .Query()
                .Include(d => d.Material)
                    .ThenInclude(m => m.Unit)
                .Include(d => d.Unit)
                .LoadAsync();
        }

        private void SyncRecipeDetails(Recipe recipe, IReadOnlyCollection<ValidatedRecipeDetail> validatedDetails)
        {
            var existingDetails = recipe.Details
                .Where(d => !d.IsDeleted)
                .ToDictionary(d => d.Id);

            var processedExistingIds = new HashSet<long>();

            foreach (var detail in validatedDetails)
            {
                if (detail.ExistingDetail != null)
                {
                    var existing = detail.ExistingDetail;
                    existing.MaterialId = detail.Material.Id;
                    existing.UnitId = detail.Unit.Id;
                    existing.Quantity = detail.Quantity;
                    existing.UpdatedAt = DateTime.Now;
                    existing.DeletedAt = null;
                    existing.IsDeleted = false;
                    processedExistingIds.Add(existing.Id);
                    continue;
                }

                var newDetail = new RecipeDetail
                {
                    MaterialId = detail.Material.Id,
                    UnitId = detail.Unit.Id,
                    Quantity = detail.Quantity,
                    RecipeId = recipe.Id,
                    CreateBy = CurrentUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = null,
                    DeletedAt = null,
                    IsDeleted = false
                };

                recipe.Details.Add(newDetail);
            }

            foreach (var existing in existingDetails.Values)
            {
                if (!processedExistingIds.Contains(existing.Id))
                {
                    existing.IsDeleted = true;
                    existing.DeletedAt = DateTime.Now;
                    existing.UpdatedAt = DateTime.Now;
                }
            }
        }

        private async Task<ValidatedRecipeRequest?> ValidateRecipeRequestAsync(
            RecipeRequest? request,
            Recipe? existingRecipe = null,
            bool skipMetadataValidation = false)
        {
            request ??= new RecipeRequest();
            var validation = new ValidatedRecipeRequest();

            var existingDetailsById = existingRecipe?.Details
                .Where(d => !d.IsDeleted)
                .ToDictionary(d => d.Id) ?? new Dictionary<long, RecipeDetail>();

            if (!skipMetadataValidation)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    ModelState.AddModelError(nameof(RecipeRequest.Name), "Tên công thức là bắt buộc.");
                }
                else
                {
                    var trimmedName = request.Name!.Trim();
                    if (trimmedName.Length > 200)
                    {
                        ModelState.AddModelError(nameof(RecipeRequest.Name), "Tên công thức không được vượt quá 200 ký tự.");
                    }
                    else
                    {
                        validation.Name = trimmedName;
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.Name))
            {
                var trimmedName = request.Name!.Trim();
                if (trimmedName.Length > 200)
                {
                    ModelState.AddModelError(nameof(RecipeRequest.Name), "Tên công thức không được vượt quá 200 ký tự.");
                }
                else
                {
                    validation.Name = trimmedName;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                var trimmedDescription = request.Description!.Trim();
                if (trimmedDescription.Length > 1000)
                {
                    ModelState.AddModelError(nameof(RecipeRequest.Description), "Mô tả không được vượt quá 1000 ký tự.");
                }
                else
                {
                    validation.Description = trimmedDescription;
                }
            }
            else
            {
                validation.Description = null;
            }

            var preparationTime = request.PreparationTime ?? 0;
            if (preparationTime < 0)
            {
                ModelState.AddModelError(nameof(RecipeRequest.PreparationTime), "Thời gian chuẩn bị không được âm.");
            }
            else
            {
                validation.PreparationTime = preparationTime;
            }

            var detailRequests = request.Details ?? new List<RecipeDetailRequest>();
            if (detailRequests.Count == 0)
            {
                ModelState.AddModelError(nameof(RecipeRequest.Details), "Cần ít nhất một nguyên vật liệu trong công thức.");
            }

            var materialIds = new HashSet<long>();
            var unitIds = new HashSet<long>();
            var normalizedDetailRequests = new List<(RecipeDetailRequest Request, RecipeDetail? Existing)>();

            for (var i = 0; i < detailRequests.Count; i++)
            {
                var detail = detailRequests[i] ?? new RecipeDetailRequest();
                RecipeDetail? existingDetail = null;

                if (detail.Id.HasValue)
                {
                    if (existingRecipe == null)
                    {
                        ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{i}].Id", "Không thể cập nhật chi tiết công thức khi công thức chưa tồn tại.");
                    }
                    else if (!existingDetailsById.TryGetValue(detail.Id.Value, out existingDetail))
                    {
                        ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{i}].Id", "Chi tiết công thức không tồn tại hoặc đã bị xóa.");
                    }
                }

                if (!detail.MaterialId.HasValue)
                {
                    ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{i}].MaterialId", "Nguyên vật liệu là bắt buộc.");
                }
                else
                {
                    materialIds.Add(detail.MaterialId.Value);
                }

                if (!detail.UnitId.HasValue)
                {
                    ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{i}].UnitId", "Đơn vị là bắt buộc.");
                }
                else
                {
                    unitIds.Add(detail.UnitId.Value);
                }

                if (!detail.Quantity.HasValue || detail.Quantity.Value <= 0)
                {
                    ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{i}].Quantity", "Số lượng phải lớn hơn 0.");
                }

                normalizedDetailRequests.Add((detail, existingDetail));
            }

            if (request.OutputUnitId.HasValue)
            {
                unitIds.Add(request.OutputUnitId.Value);
            }

            var materials = materialIds.Count == 0
                ? new Dictionary<long, Material>()
                : await _context.Materials
                    .AsNoTracking()
                    .Include(m => m.Unit)
                    .Where(m => materialIds.Contains(m.Id) && !m.IsDeleted)
                    .ToDictionaryAsync(m => m.Id);

            var units = unitIds.Count == 0
                ? new Dictionary<long, Unit>()
                : await _context.Units
                    .AsNoTracking()
                    .Where(u => unitIds.Contains(u.Id) && !u.IsDeleted)
                    .ToDictionaryAsync(u => u.Id);

            var conversionPairs = new HashSet<(long From, long To)>();

            foreach (var detail in normalizedDetailRequests)
            {
                if (!detail.Request.MaterialId.HasValue || !detail.Request.UnitId.HasValue)
                {
                    continue;
                }

                if (!materials.TryGetValue(detail.Request.MaterialId.Value, out var material) || material.Unit == null)
                {
                    continue;
                }

                if (!units.ContainsKey(detail.Request.UnitId.Value))
                {
                    continue;
                }

                if (detail.Request.UnitId.Value != material.UnitId)
                {
                    conversionPairs.Add((detail.Request.UnitId.Value, material.UnitId));
                }
            }

            var conversionLookup = new Dictionary<(long, long), decimal>();
            if (conversionPairs.Count > 0)
            {
                var fromUnitIds = conversionPairs.Select(p => p.From).Distinct().ToList();
                var toUnitIds = conversionPairs.Select(p => p.To).Distinct().ToList();

                var conversions = await _context.ConversionUnits
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted && fromUnitIds.Contains(c.FromUnitId) && toUnitIds.Contains(c.ToUnitId))
                    .ToListAsync();

                conversionLookup = conversions.ToDictionary(c => (c.FromUnitId, c.ToUnitId), c => c.ConversionRate);
            }

            foreach (var kvp in normalizedDetailRequests.Select((value, index) => (value, index)))
            {
                var (detail, index) = (kvp.value, kvp.index);
                if (!detail.Request.MaterialId.HasValue || !detail.Request.UnitId.HasValue || !detail.Request.Quantity.HasValue)
                {
                    continue;
                }

                if (!materials.TryGetValue(detail.Request.MaterialId.Value, out var material))
                {
                    ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{index}].MaterialId", "Nguyên vật liệu không tồn tại hoặc đã bị xóa.");
                    continue;
                }

                if (material.Unit == null)
                {
                    ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{index}].MaterialId", "Nguyên vật liệu chưa được cấu hình đơn vị cơ bản.");
                    continue;
                }

                if (!units.TryGetValue(detail.Request.UnitId.Value, out var unit))
                {
                    ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{index}].UnitId", "Đơn vị không tồn tại hoặc đã bị xóa.");
                    continue;
                }

                var conversionRate = 1m;
                if (detail.Request.UnitId.Value != material.UnitId)
                {
                    if (!conversionLookup.TryGetValue((detail.Request.UnitId.Value, material.UnitId), out conversionRate) || conversionRate <= 0)
                    {
                        ModelState.AddModelError($"{nameof(RecipeRequest.Details)}[{index}].UnitId", "Không thể chuyển đổi đơn vị đã chọn về đơn vị cơ bản của nguyên vật liệu.");
                        continue;
                    }
                }

                validation.Details.Add(new ValidatedRecipeDetail
                {
                    ExistingDetail = detail.Existing,
                    Material = material,
                    Unit = unit,
                    Quantity = detail.Request.Quantity!.Value,
                    ConversionRate = conversionRate
                });
            }

            if (detailRequests.Count > 0 && validation.Details.Count == 0)
            {
                ModelState.AddModelError(nameof(RecipeRequest.Details), "Không có chi tiết công thức hợp lệ.");
            }

            if (!skipMetadataValidation)
            {
                if (!request.OutputUnitId.HasValue)
                {
                    ModelState.AddModelError(nameof(RecipeRequest.OutputUnitId), "Đơn vị đầu ra là bắt buộc.");
                }
                else if (!units.TryGetValue(request.OutputUnitId.Value, out var outputUnit))
                {
                    ModelState.AddModelError(nameof(RecipeRequest.OutputUnitId), "Đơn vị đầu ra không tồn tại hoặc đã bị xóa.");
                }
                else
                {
                    validation.OutputUnitId = outputUnit.Id;
                }
            }
            else if (request.OutputUnitId.HasValue)
            {
                if (!units.ContainsKey(request.OutputUnitId.Value))
                {
                    ModelState.AddModelError(nameof(RecipeRequest.OutputUnitId), "Đơn vị đầu ra không tồn tại hoặc đã bị xóa.");
                }
                else
                {
                    validation.OutputUnitId = request.OutputUnitId.Value;
                }
            }

            if (!skipMetadataValidation && validation.Name == string.Empty)
            {
                ModelState.AddModelError(nameof(RecipeRequest.Name), "Tên công thức là bắt buộc.");
            }

            return ModelState.IsValid ? validation : null;
        }

        private RecipeCostPreviewResponse BuildCostPreview(ValidatedRecipeRequest validation)
        {
            decimal totalCost = 0m;
            var details = new List<RecipeDetailResponse>();

            foreach (var detail in validation.Details)
            {
                var response = CreateDetailResponse(detail.ExistingDetail?.Id ?? 0, detail.Material, detail.Unit, detail.Quantity, detail.ConversionRate);
                details.Add(response);
                totalCost += response.Cost;
            }

            return new RecipeCostPreviewResponse
            {
                TotalCost = totalCost,
                Details = details
            };
        }

        public class RecipeRequest
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public long? OutputUnitId { get; set; }
            public int? PreparationTime { get; set; }
            public List<RecipeDetailRequest> Details { get; set; } = new();
        }

        public class RecipeDetailRequest
        {
            public long? Id { get; set; }
            public long? MaterialId { get; set; }
            public decimal? Quantity { get; set; }
            public long? UnitId { get; set; }
        }

        public class RecipeQuery
        {
            public string? Search { get; set; }
            public long? OutputUnitId { get; set; }
        }

        private sealed class RecipeResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public long OutputUnitId { get; set; }
            public string OutputUnitName { get; set; } = string.Empty;
            public int PreparationTime { get; set; }
            public decimal TotalCost { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public List<RecipeDetailResponse> Details { get; set; } = new();
        }

        private sealed class RecipeDetailResponse
        {
            public long Id { get; set; }
            public long MaterialId { get; set; }
            public string MaterialCode { get; set; } = string.Empty;
            public string MaterialName { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public long UnitId { get; set; }
            public string UnitName { get; set; } = string.Empty;
            public decimal ConversionRate { get; set; }
            public decimal ConvertedQuantity { get; set; }
            public long BaseUnitId { get; set; }
            public string BaseUnitName { get; set; } = string.Empty;
            public decimal MaterialPrice { get; set; }
            public decimal Cost { get; set; }
        }

        private sealed class RecipeCostPreviewResponse
        {
            public decimal TotalCost { get; set; }
            public List<RecipeDetailResponse> Details { get; set; } = new();
        }

        private sealed class ValidatedRecipeRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public long? OutputUnitId { get; set; }
            public int PreparationTime { get; set; }
            public List<ValidatedRecipeDetail> Details { get; } = new();
        }

        private sealed class ValidatedRecipeDetail
        {
            public RecipeDetail? ExistingDetail { get; set; }
            public Material Material { get; set; } = null!;
            public Unit Unit { get; set; } = null!;
            public decimal Quantity { get; set; }
            public decimal ConversionRate { get; set; }
        }
    }
}
