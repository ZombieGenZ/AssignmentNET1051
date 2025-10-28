using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Assignment.Authorization
{
    public sealed class PermissionDefinition
    {
        public PermissionDefinition(string key, string displayName)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        }

        public string Key { get; }

        public string DisplayName { get; }
    }

    public sealed class PermissionGroupDefinition
    {
        public PermissionGroupDefinition(string name, IReadOnlyList<PermissionDefinition> permissions)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        }

        public string Name { get; }

        public IReadOnlyList<PermissionDefinition> Permissions { get; }
    }

    public static class PermissionRegistry
    {
        private static readonly IReadOnlyList<PermissionGroupDefinition> _groups = new List<PermissionGroupDefinition>
        {
            new PermissionGroupDefinition("Categories", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetCategoryAll", "Xem tất cả danh mục"),
                new PermissionDefinition("GetCategory", "Xem danh mục của bản thân"),
                new PermissionDefinition("CreateCategory", "Tạo danh mục"),
                new PermissionDefinition("UpdateCategoryAll", "Sửa bất kỳ danh mục"),
                new PermissionDefinition("UpdateCategory", "Sửa danh mục của bản thân"),
                new PermissionDefinition("DeleteCategoryAll", "Xóa bất kỳ danh mục"),
                new PermissionDefinition("DeleteCategory", "Xóa danh mục của bản thân"),
            }),
            new PermissionGroupDefinition("Products", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetProductAll", "Xem tất cả sản phẩm"),
                new PermissionDefinition("GetProduct", "Xem sản phẩm của bản thân"),
                new PermissionDefinition("CreateProduct", "Tạo sản phẩm"),
                new PermissionDefinition("UpdateProductAll", "Sửa bất kỳ sản phẩm"),
                new PermissionDefinition("UpdateProduct", "Sửa sản phẩm của bản thân"),
                new PermissionDefinition("DeleteProductAll", "Xóa bất kỳ sản phẩm"),
                new PermissionDefinition("DeleteProduct", "Xóa sản phẩm của bản thân"),
            }),
            new PermissionGroupDefinition("Units", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetUnitAll", "Xem tất cả đơn vị"),
                new PermissionDefinition("GetUnit", "Xem đơn vị của bản thân"),
                new PermissionDefinition("CreateUnit", "Tạo đơn vị"),
                new PermissionDefinition("UpdateUnitAll", "Sửa bất kỳ đơn vị"),
                new PermissionDefinition("UpdateUnit", "Sửa đơn vị của bản thân"),
                new PermissionDefinition("DeleteUnitAll", "Xóa bất kỳ đơn vị"),
                new PermissionDefinition("DeleteUnit", "Xóa đơn vị của bản thân"),
            }),
            new PermissionGroupDefinition("Materials", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetMaterialAll", "Xem tất cả nguyên vật liệu"),
                new PermissionDefinition("GetMaterial", "Xem nguyên vật liệu của bản thân"),
                new PermissionDefinition("CreateMaterial", "Tạo nguyên vật liệu"),
                new PermissionDefinition("UpdateMaterialAll", "Sửa bất kỳ nguyên vật liệu"),
                new PermissionDefinition("UpdateMaterial", "Sửa nguyên vật liệu của bản thân"),
                new PermissionDefinition("DeleteMaterialAll", "Xóa bất kỳ nguyên vật liệu"),
                new PermissionDefinition("DeleteMaterial", "Xóa nguyên vật liệu của bản thân"),
            }),
            new PermissionGroupDefinition("Recipes", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetRecipeAll", "Xem tất cả công thức"),
                new PermissionDefinition("GetRecipe", "Xem công thức của bản thân"),
                new PermissionDefinition("CreateRecipe", "Tạo công thức"),
                new PermissionDefinition("UpdateRecipeAll", "Sửa bất kỳ công thức"),
                new PermissionDefinition("UpdateRecipe", "Sửa công thức của bản thân"),
                new PermissionDefinition("DeleteRecipeAll", "Xóa bất kỳ công thức"),
                new PermissionDefinition("DeleteRecipe", "Xóa công thức của bản thân"),
            }),
            new PermissionGroupDefinition("Product Extras", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetProductExtraAll", "Xem tất cả sản phẩm bổ sung"),
                new PermissionDefinition("GetProductExtra", "Xem sản phẩm bổ sung của bản thân"),
                new PermissionDefinition("CreateProductExtra", "Tạo sản phẩm bổ sung"),
                new PermissionDefinition("UpdateProductExtraAll", "Sửa bất kỳ sản phẩm bổ sung"),
                new PermissionDefinition("UpdateProductExtra", "Sửa sản phẩm bổ sung của bản thân"),
                new PermissionDefinition("DeleteProductExtraAll", "Xóa bất kỳ sản phẩm bổ sung"),
                new PermissionDefinition("DeleteProductExtra", "Xóa sản phẩm bổ sung của bản thân"),
            }),
            new PermissionGroupDefinition("Combos", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetComboAll", "Xem tất cả combo"),
                new PermissionDefinition("GetCombo", "Xem combo của bản thân"),
                new PermissionDefinition("CreateCombo", "Tạo combo"),
                new PermissionDefinition("UpdateComboAll", "Sửa bất kỳ combo"),
                new PermissionDefinition("UpdateCombo", "Sửa combo của bản thân"),
                new PermissionDefinition("DeleteComboAll", "Xóa bất kỳ combo"),
                new PermissionDefinition("DeleteCombo", "Xóa combo của bản thân"),
            }),
            new PermissionGroupDefinition("Vouchers", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetVoucherAll", "Xem tất cả voucher"),
                new PermissionDefinition("GetVoucher", "Xem voucher của bản thân"),
                new PermissionDefinition("CreateVoucher", "Tạo voucher"),
                new PermissionDefinition("UpdateVoucherAll", "Sửa bất kỳ voucher"),
                new PermissionDefinition("UpdateVoucher", "Sửa voucher của bản thân"),
                new PermissionDefinition("DeleteVoucherAll", "Xóa bất kỳ voucher"),
                new PermissionDefinition("DeleteVoucher", "Xóa voucher của bản thân"),
            }),
            new PermissionGroupDefinition("Rewards", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetRewardAll", "Xem tất cả vật phẩm đổi thưởng"),
                new PermissionDefinition("GetReward", "Xem vật phẩm đổi thưởng của bản thân"),
                new PermissionDefinition("CreateReward", "Tạo vật phẩm đổi thưởng"),
                new PermissionDefinition("UpdateRewardAll", "Sửa bất kỳ vật phẩm đổi thưởng"),
                new PermissionDefinition("UpdateReward", "Sửa vật phẩm đổi thưởng của bản thân"),
                new PermissionDefinition("DeleteRewardAll", "Xóa bất kỳ vật phẩm đổi thưởng"),
                new PermissionDefinition("DeleteReward", "Xóa vật phẩm đổi thưởng của bản thân"),
            }),
            new PermissionGroupDefinition("Orders & Others", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetOrderAll", "Xem tất cả đơn hàng"),
                new PermissionDefinition("ChangeOrderStatusAll", "Thay đổi trạng thái tất cả đơn hàng"),
                new PermissionDefinition("ViewStatistics", "Xem báo cáo thống kê"),
                new PermissionDefinition("DeleteEvaluate", "Xóa đánh giá"),
            }),
            new PermissionGroupDefinition("Customers", new List<PermissionDefinition>
            {
                new PermissionDefinition("ViewCustomerAll", "Xem danh sách khách hàng"),
                new PermissionDefinition("ViewTopUserAll", "Xem bảng xếp hạng khách hàng"),
            }),
            new PermissionGroupDefinition("Inventory", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetReceivingAll", "Xem tất cả phiếu nhập"),
                new PermissionDefinition("CreateReceiving", "Tạo phiếu nhập kho"),
                new PermissionDefinition("ViewInventoryAll", "Xem tất cả tồn kho"),
                new PermissionDefinition("ViewInventory", "Xem tồn kho được phân quyền"),
            }),
            new PermissionGroupDefinition("Warehouses", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetWarehouseAll", "Xem tất cả kho"),
                new PermissionDefinition("GetWarehouse", "Xem kho của bản thân"),
                new PermissionDefinition("CreateWarehouse", "Tạo kho"),
                new PermissionDefinition("UpdateWarehouseAll", "Sửa tất cả kho"),
                new PermissionDefinition("UpdateWarehouse", "Sửa kho của bản thân"),
                new PermissionDefinition("DeleteWarehouseAll", "Xóa tất cả kho"),
                new PermissionDefinition("DeleteWarehouse", "Xóa kho của bản thân"),
            }),
            new PermissionGroupDefinition("Suppliers", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetSupplierAll", "Xem tất cả nhà cung cấp"),
                new PermissionDefinition("GetSupplier", "Xem nhà cung cấp của bản thân"),
                new PermissionDefinition("CreateSupplier", "Tạo nhà cung cấp"),
                new PermissionDefinition("UpdateSupplierAll", "Sửa bất kỳ nhà cung cấp"),
                new PermissionDefinition("UpdateSupplier", "Sửa nhà cung cấp của bản thân"),
                new PermissionDefinition("DeleteSupplierAll", "Xóa bất kỳ nhà cung cấp"),
                new PermissionDefinition("DeleteSupplier", "Xóa nhà cung cấp của bản thân"),
            }),
        };

        private static readonly IReadOnlyCollection<string> _allPermissionKeys = new ReadOnlyCollection<string>(_groups
            .SelectMany(group => group.Permissions)
            .Select(permission => permission.Key)
            .ToList());

        public static IReadOnlyList<PermissionGroupDefinition> Groups => _groups;

        public static IReadOnlyCollection<string> AllPermissionKeys => _allPermissionKeys;
    }
}
