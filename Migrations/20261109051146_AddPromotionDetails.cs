using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Promotions",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountType",
                table: "Promotions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MenuCategoryId",
                table: "Promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoCode",
                table: "Promotions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Audience",
                table: "Announcements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_MenuCategoryId",
                table: "Promotions",
                column: "MenuCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_PromoCode",
                table: "Promotions",
                column: "PromoCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_MenuCategories_MenuCategoryId",
                table: "Promotions",
                column: "MenuCategoryId",
                principalTable: "MenuCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_MenuCategories_MenuCategoryId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_MenuCategoryId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_PromoCode",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "MenuCategoryId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "PromoCode",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "Announcements");
        }
    }
}
