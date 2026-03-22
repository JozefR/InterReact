using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehouse.Schema.Migrations
{
    /// <inheritdoc />
    public partial class FixSqlitePriceConstraintCasting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_prices_daily_raw_price_order",
                table: "prices_daily_raw");

            migrationBuilder.DropCheckConstraint(
                name: "CK_prices_daily_adjusted_factor_non_negative",
                table: "prices_daily_adjusted");

            migrationBuilder.DropCheckConstraint(
                name: "CK_prices_daily_adjusted_price_order",
                table: "prices_daily_adjusted");

            migrationBuilder.AddCheckConstraint(
                name: "CK_prices_daily_raw_price_order",
                table: "prices_daily_raw",
                sql: "CAST(High AS REAL) >= CAST(Low AS REAL) AND CAST(Open AS REAL) >= CAST(Low AS REAL) AND CAST(Open AS REAL) <= CAST(High AS REAL) AND CAST(Close AS REAL) >= CAST(Low AS REAL) AND CAST(Close AS REAL) <= CAST(High AS REAL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_prices_daily_adjusted_factor_non_negative",
                table: "prices_daily_adjusted",
                sql: "CAST(AdjustmentFactor AS REAL) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_prices_daily_adjusted_price_order",
                table: "prices_daily_adjusted",
                sql: "CAST(High AS REAL) >= CAST(Low AS REAL) AND CAST(Open AS REAL) >= CAST(Low AS REAL) AND CAST(Open AS REAL) <= CAST(High AS REAL) AND CAST(Close AS REAL) >= CAST(Low AS REAL) AND CAST(Close AS REAL) <= CAST(High AS REAL) AND CAST(AdjustedClose AS REAL) >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_prices_daily_raw_price_order",
                table: "prices_daily_raw");

            migrationBuilder.DropCheckConstraint(
                name: "CK_prices_daily_adjusted_factor_non_negative",
                table: "prices_daily_adjusted");

            migrationBuilder.DropCheckConstraint(
                name: "CK_prices_daily_adjusted_price_order",
                table: "prices_daily_adjusted");

            migrationBuilder.AddCheckConstraint(
                name: "CK_prices_daily_raw_price_order",
                table: "prices_daily_raw",
                sql: "High >= Low AND Open >= Low AND Open <= High AND Close >= Low AND Close <= High");

            migrationBuilder.AddCheckConstraint(
                name: "CK_prices_daily_adjusted_factor_non_negative",
                table: "prices_daily_adjusted",
                sql: "AdjustmentFactor >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_prices_daily_adjusted_price_order",
                table: "prices_daily_adjusted",
                sql: "High >= Low AND Open >= Low AND Open <= High AND Close >= Low AND Close <= High AND AdjustedClose >= 0");
        }
    }
}
