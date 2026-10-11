using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlpacaMedic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssetModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessAssetRelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BusinessAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAssetRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessAssetRelations_BusinessAssets_BusinessAssetId",
                        column: x => x.BusinessAssetId,
                        principalTable: "BusinessAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessAssetRelations_SystemAssets_SystemAssetId",
                        column: x => x.SystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemAssetRelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceSystemAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetSystemAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAssetRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemAssetRelations_SystemAssets_SourceSystemAssetId",
                        column: x => x.SourceSystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SystemAssetRelations_SystemAssets_TargetSystemAssetId",
                        column: x => x.TargetSystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAssetRelations_BusinessAssetId_SystemAssetId_RelationType",
                table: "BusinessAssetRelations",
                columns: new[] { "BusinessAssetId", "SystemAssetId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAssetRelations_SystemAssetId",
                table: "BusinessAssetRelations",
                column: "SystemAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAssetRelations_SourceSystemAssetId_TargetSystemAssetId_RelationType",
                table: "SystemAssetRelations",
                columns: new[] { "SourceSystemAssetId", "TargetSystemAssetId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemAssetRelations_TargetSystemAssetId",
                table: "SystemAssetRelations",
                column: "TargetSystemAssetId");

            // Keep IDs and every legacy link before removing the old join table.
            migrationBuilder.Sql("""
                INSERT INTO BusinessAssetRelations (Id, BusinessAssetId, SystemAssetId, RelationType, Description)
                SELECT Id, BusinessAssetId, SystemAssetId, 'DependsOn', '' FROM AssetMappings;
                UPDATE SystemAssets SET AssetType = CASE AssetType
                    WHEN 'Server' THEN 'Host'
                    WHEN 'Cloud' THEN 'CloudService'
                    WHEN 'PersonAccount' THEN 'Identity'
                    ELSE AssetType END;
                """);
            migrationBuilder.DropTable(name: "AssetMappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_AssetMappings_BusinessAssetId_SystemAssetId",
                table: "AssetMappings",
                columns: new[] { "BusinessAssetId", "SystemAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetMappings_SystemAssetId",
                table: "AssetMappings",
                column: "SystemAssetId");

            // The old schema can retain only one link per pair, without type or description.
            migrationBuilder.Sql("""
                INSERT INTO AssetMappings (Id, BusinessAssetId, SystemAssetId)
                SELECT MIN(Id), BusinessAssetId, SystemAssetId FROM BusinessAssetRelations
                GROUP BY BusinessAssetId, SystemAssetId;
                UPDATE SystemAssets SET AssetType = CASE AssetType
                    WHEN 'Host' THEN 'Server'
                    WHEN 'CloudService' THEN 'Cloud'
                    WHEN 'Identity' THEN 'PersonAccount'
                    WHEN 'Network' THEN 'Network'
                    WHEN 'Endpoint' THEN 'Endpoint'
                    ELSE 'Other' END;
                """);
            migrationBuilder.DropTable(name: "BusinessAssetRelations");
            migrationBuilder.DropTable(name: "SystemAssetRelations");
        }
    }
}
