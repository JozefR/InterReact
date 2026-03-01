using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehouse.Schema.Migrations
{
    /// <inheritdoc />
    public partial class InitialCanonicalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Pipeline = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequestParametersJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    RowsRead = table.Column<int>(type: "INTEGER", nullable: false),
                    RowsInserted = table.Column<int>(type: "INTEGER", nullable: false),
                    RowsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "symbol_master",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ExchangeMic = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AssetType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ListedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DelistedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbol_master", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "qa_results",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IngestionRunId = table.Column<long>(type: "INTEGER", nullable: true),
                    CheckName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AffectedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qa_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qa_results_ingestion_runs_IngestionRunId",
                        column: x => x.IngestionRunId,
                        principalTable: "ingestion_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "corporate_actions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SymbolMasterId = table.Column<long>(type: "INTEGER", nullable: false),
                    ActionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corporate_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_corporate_actions_symbol_master_SymbolMasterId",
                        column: x => x.SymbolMasterId,
                        principalTable: "symbol_master",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "index_constituents_pit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IndexCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SymbolMasterId = table.Column<long>(type: "INTEGER", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Weight = table.Column<decimal>(type: "TEXT", precision: 12, scale: 8, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_index_constituents_pit", x => x.Id);
                    table.CheckConstraint("CK_index_constituents_pit_effective_range", "EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom");
                    table.ForeignKey(
                        name: "FK_index_constituents_pit_symbol_master_SymbolMasterId",
                        column: x => x.SymbolMasterId,
                        principalTable: "symbol_master",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prices_daily_adjusted",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SymbolMasterId = table.Column<long>(type: "INTEGER", nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Open = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    High = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Low = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Close = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    AdjustedClose = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    AdjustmentFactor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    Volume = table.Column<long>(type: "INTEGER", nullable: false),
                    AdjustmentBasis = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IngestionRunId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prices_daily_adjusted", x => x.Id);
                    table.CheckConstraint("CK_prices_daily_adjusted_factor_non_negative", "AdjustmentFactor >= 0");
                    table.CheckConstraint("CK_prices_daily_adjusted_positive_volume", "Volume >= 0");
                    table.CheckConstraint("CK_prices_daily_adjusted_price_order", "High >= Low AND Open >= Low AND Open <= High AND Close >= Low AND Close <= High AND AdjustedClose >= 0");
                    table.ForeignKey(
                        name: "FK_prices_daily_adjusted_ingestion_runs_IngestionRunId",
                        column: x => x.IngestionRunId,
                        principalTable: "ingestion_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prices_daily_adjusted_symbol_master_SymbolMasterId",
                        column: x => x.SymbolMasterId,
                        principalTable: "symbol_master",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prices_daily_raw",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SymbolMasterId = table.Column<long>(type: "INTEGER", nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Open = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    High = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Low = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Close = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Volume = table.Column<long>(type: "INTEGER", nullable: false),
                    Vwap = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IngestionRunId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prices_daily_raw", x => x.Id);
                    table.CheckConstraint("CK_prices_daily_raw_positive_volume", "Volume >= 0");
                    table.CheckConstraint("CK_prices_daily_raw_price_order", "High >= Low AND Open >= Low AND Open <= High AND Close >= Low AND Close <= High");
                    table.ForeignKey(
                        name: "FK_prices_daily_raw_ingestion_runs_IngestionRunId",
                        column: x => x.IngestionRunId,
                        principalTable: "ingestion_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prices_daily_raw_symbol_master_SymbolMasterId",
                        column: x => x.SymbolMasterId,
                        principalTable: "symbol_master",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "symbol_mapping",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SymbolMasterId = table.Column<long>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProviderSymbol = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbol_mapping", x => x.Id);
                    table.CheckConstraint("CK_symbol_mapping_effective_range", "EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom");
                    table.ForeignKey(
                        name: "FK_symbol_mapping_symbol_master_SymbolMasterId",
                        column: x => x.SymbolMasterId,
                        principalTable: "symbol_master",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_corporate_actions_SymbolMasterId_ActionDate",
                table: "corporate_actions",
                columns: new[] { "SymbolMasterId", "ActionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_corporate_actions_SymbolMasterId_ActionDate_ActionType_Provider_Value",
                table: "corporate_actions",
                columns: new[] { "SymbolMasterId", "ActionDate", "ActionType", "Provider", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_index_constituents_pit_IndexCode_EffectiveFrom",
                table: "index_constituents_pit",
                columns: new[] { "IndexCode", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_index_constituents_pit_IndexCode_SymbolMasterId_EffectiveFrom",
                table: "index_constituents_pit",
                columns: new[] { "IndexCode", "SymbolMasterId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_index_constituents_pit_SymbolMasterId",
                table: "index_constituents_pit",
                column: "SymbolMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_runs_Pipeline_StartedAtUtc",
                table: "ingestion_runs",
                columns: new[] { "Pipeline", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_runs_RunId",
                table: "ingestion_runs",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_runs_Status",
                table: "ingestion_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_prices_daily_adjusted_IngestionRunId",
                table: "prices_daily_adjusted",
                column: "IngestionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_prices_daily_adjusted_SymbolMasterId_TradeDate",
                table: "prices_daily_adjusted",
                columns: new[] { "SymbolMasterId", "TradeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_prices_daily_adjusted_SymbolMasterId_TradeDate_Provider",
                table: "prices_daily_adjusted",
                columns: new[] { "SymbolMasterId", "TradeDate", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prices_daily_raw_IngestionRunId",
                table: "prices_daily_raw",
                column: "IngestionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_prices_daily_raw_SymbolMasterId_TradeDate",
                table: "prices_daily_raw",
                columns: new[] { "SymbolMasterId", "TradeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_prices_daily_raw_SymbolMasterId_TradeDate_Provider",
                table: "prices_daily_raw",
                columns: new[] { "SymbolMasterId", "TradeDate", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_results_IngestionRunId",
                table: "qa_results",
                column: "IngestionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_qa_results_Severity_Status_CreatedUtc",
                table: "qa_results",
                columns: new[] { "Severity", "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_symbol_mapping_Provider_ProviderSymbol_EffectiveFrom",
                table: "symbol_mapping",
                columns: new[] { "Provider", "ProviderSymbol", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_symbol_mapping_SymbolMasterId_EffectiveFrom",
                table: "symbol_mapping",
                columns: new[] { "SymbolMasterId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_symbol_master_IsActive",
                table: "symbol_master",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_symbol_master_Symbol",
                table: "symbol_master",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "corporate_actions");

            migrationBuilder.DropTable(
                name: "index_constituents_pit");

            migrationBuilder.DropTable(
                name: "prices_daily_adjusted");

            migrationBuilder.DropTable(
                name: "prices_daily_raw");

            migrationBuilder.DropTable(
                name: "qa_results");

            migrationBuilder.DropTable(
                name: "symbol_mapping");

            migrationBuilder.DropTable(
                name: "ingestion_runs");

            migrationBuilder.DropTable(
                name: "symbol_master");
        }
    }
}
