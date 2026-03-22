using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehouse.Schema.Migrations
{
    /// <inheritdoc />
    public partial class AddAdjustedPriceBasisUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prices_daily_adjusted_SymbolMasterId_TradeDate_Provider",
                table: "prices_daily_adjusted");

            migrationBuilder.CreateIndex(
                name: "IX_prices_daily_adjusted_SymbolMasterId_TradeDate_Provider_AdjustmentBasis",
                table: "prices_daily_adjusted",
                columns: new[] { "SymbolMasterId", "TradeDate", "Provider", "AdjustmentBasis" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prices_daily_adjusted_SymbolMasterId_TradeDate_Provider_AdjustmentBasis",
                table: "prices_daily_adjusted");

            migrationBuilder.CreateIndex(
                name: "IX_prices_daily_adjusted_SymbolMasterId_TradeDate_Provider",
                table: "prices_daily_adjusted",
                columns: new[] { "SymbolMasterId", "TradeDate", "Provider" },
                unique: true);
        }
    }
}
