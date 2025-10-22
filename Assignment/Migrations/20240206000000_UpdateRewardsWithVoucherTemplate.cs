using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    public partial class UpdateRewardsWithVoucherTemplate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Rewards",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsValidityUnlimited",
                table: "Rewards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VoucherProductScope",
                table: "Rewards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VoucherComboScope",
                table: "Rewards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VoucherDiscountType",
                table: "Rewards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "VoucherDiscount",
                table: "Rewards",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "VoucherMinimumRequirements",
                table: "Rewards",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "VoucherUnlimitedPercentageDiscount",
                table: "Rewards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "VoucherMaximumPercentageReduction",
                table: "Rewards",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VoucherHasCombinedUsageLimit",
                table: "Rewards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VoucherMaxCombinedUsageCount",
                table: "Rewards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VoucherIsForNewUsersOnly",
                table: "Rewards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LoyaltyRewardsApplied",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "VoucherId",
                table: "RewardRedemptions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ValidTo",
                table: "RewardRedemptions",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_VoucherId",
                table: "RewardRedemptions",
                column: "VoucherId");

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_Vouchers_VoucherId",
                table: "RewardRedemptions",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_Vouchers_VoucherId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_VoucherId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "IsValidityUnlimited",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherProductScope",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherComboScope",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherDiscountType",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherDiscount",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherMinimumRequirements",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherUnlimitedPercentageDiscount",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherMaximumPercentageReduction",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherHasCombinedUsageLimit",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherMaxCombinedUsageCount",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherIsForNewUsersOnly",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "LoyaltyRewardsApplied",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherId",
                table: "RewardRedemptions");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ValidTo",
                table: "RewardRedemptions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
