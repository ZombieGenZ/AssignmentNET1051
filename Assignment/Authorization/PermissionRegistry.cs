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
            new PermissionGroupDefinition("Orders & Others", new List<PermissionDefinition>
            {
                new PermissionDefinition("GetOrderAll", "Xem tất cả đơn hàng"),
                new PermissionDefinition("ChangeOrderStatusAll", "Thay đổi trạng thái tất cả đơn hàng"),
                new PermissionDefinition("ViewStatistics", "Xem báo cáo thống kê"),
                new PermissionDefinition("DeleteEvaluate", "Xóa đánh giá"),
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
