using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlpacaMedic.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttackTechniques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TechniqueId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Tactic = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    MitreUrl = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttackTechniques", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessProcesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Confidentiality = table.Column<int>(type: "INTEGER", nullable: false),
                    Integrity = table.Column<int>(type: "INTEGER", nullable: false),
                    Availability = table.Column<int>(type: "INTEGER", nullable: false),
                    Consequence = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessProcesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CisControls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CisControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    AssetType = table.Column<string>(type: "TEXT", nullable: false),
                    Zone = table.Column<string>(type: "TEXT", nullable: false),
                    BlastRadius = table.Column<decimal>(type: "TEXT", nullable: false),
                    BlastRadiusReason = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BusinessProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessAssets_BusinessProcesses_BusinessProcessId",
                        column: x => x.BusinessProcessId,
                        principalTable: "BusinessProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueControls",
                columns: table => new
                {
                    AttackTechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    CisControlId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueControls", x => new { x.AttackTechniqueId, x.CisControlId });
                    table.ForeignKey(
                        name: "FK_TechniqueControls_AttackTechniques_AttackTechniqueId",
                        column: x => x.AttackTechniqueId,
                        principalTable: "AttackTechniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechniqueControls_CisControls_CisControlId",
                        column: x => x.CisControlId,
                        principalTable: "CisControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemAccess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceSystemAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetSystemAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemAccess_SystemAssets_SourceSystemAssetId",
                        column: x => x.SourceSystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SystemAccess_SystemAssets_TargetSystemAssetId",
                        column: x => x.TargetSystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemThreats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SystemAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackTechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    Scenario = table.Column<string>(type: "TEXT", nullable: false),
                    Likelihood = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemThreats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemThreats_AttackTechniques_AttackTechniqueId",
                        column: x => x.AttackTechniqueId,
                        principalTable: "AttackTechniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SystemThreats_SystemAssets_SystemAssetId",
                        column: x => x.SystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BusinessAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemAssetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetMappings_BusinessAssets_BusinessAssetId",
                        column: x => x.BusinessAssetId,
                        principalTable: "BusinessAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetMappings_SystemAssets_SystemAssetId",
                        column: x => x.SystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThreatControls",
                columns: table => new
                {
                    SystemThreatId = table.Column<int>(type: "INTEGER", nullable: false),
                    CisControlId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRecommended = table.Column<bool>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreatControls", x => new { x.SystemThreatId, x.CisControlId });
                    table.ForeignKey(
                        name: "FK_ThreatControls_CisControls_CisControlId",
                        column: x => x.CisControlId,
                        principalTable: "CisControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ThreatControls_SystemThreats_SystemThreatId",
                        column: x => x.SystemThreatId,
                        principalTable: "SystemThreats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetMappings_BusinessAssetId_SystemAssetId",
                table: "AssetMappings",
                columns: new[] { "BusinessAssetId", "SystemAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetMappings_SystemAssetId",
                table: "AssetMappings",
                column: "SystemAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAssets_BusinessProcessId",
                table: "BusinessAssets",
                column: "BusinessProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAccess_SourceSystemAssetId_TargetSystemAssetId",
                table: "SystemAccess",
                columns: new[] { "SourceSystemAssetId", "TargetSystemAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemAccess_TargetSystemAssetId",
                table: "SystemAccess",
                column: "TargetSystemAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemThreats_AttackTechniqueId",
                table: "SystemThreats",
                column: "AttackTechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemThreats_SystemAssetId",
                table: "SystemThreats",
                column: "SystemAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueControls_CisControlId",
                table: "TechniqueControls",
                column: "CisControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ThreatControls_CisControlId",
                table: "ThreatControls",
                column: "CisControlId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetMappings");

            migrationBuilder.DropTable(
                name: "SystemAccess");

            migrationBuilder.DropTable(
                name: "TechniqueControls");

            migrationBuilder.DropTable(
                name: "ThreatControls");

            migrationBuilder.DropTable(
                name: "BusinessAssets");

            migrationBuilder.DropTable(
                name: "CisControls");

            migrationBuilder.DropTable(
                name: "SystemThreats");

            migrationBuilder.DropTable(
                name: "BusinessProcesses");

            migrationBuilder.DropTable(
                name: "AttackTechniques");

            migrationBuilder.DropTable(
                name: "SystemAssets");
        }
    }
}
