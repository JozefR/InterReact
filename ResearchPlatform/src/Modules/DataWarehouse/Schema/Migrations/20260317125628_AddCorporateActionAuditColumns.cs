using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehouse.Schema.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateActionAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentFactor",
                table: "corporate_actions",
                type: "TEXT",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                table: "corporate_actions",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IngestionRunId",
                table: "corporate_actions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedProviderSymbol",
                table: "corporate_actions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_corporate_actions_IngestionRunId",
                table: "corporate_actions",
                column: "IngestionRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_corporate_actions_ingestion_runs_IngestionRunId",
                table: "corporate_actions",
                column: "IngestionRunId",
                principalTable: "ingestion_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_corporate_actions_ingestion_runs_IngestionRunId",
                table: "corporate_actions");

            migrationBuilder.DropIndex(
                name: "IX_corporate_actions_IngestionRunId",
                table: "corporate_actions");

            migrationBuilder.DropColumn(
                name: "AdjustmentFactor",
                table: "corporate_actions");

            migrationBuilder.DropColumn(
                name: "AttributesJson",
                table: "corporate_actions");

            migrationBuilder.DropColumn(
                name: "IngestionRunId",
                table: "corporate_actions");

            migrationBuilder.DropColumn(
                name: "RelatedProviderSymbol",
                table: "corporate_actions");
        }
    }
}
