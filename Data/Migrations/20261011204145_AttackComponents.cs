using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlpacaMedic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttackComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttackComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SystemAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackTechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Likelihood = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttackComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttackComponents_AttackTechniques_AttackTechniqueId",
                        column: x => x.AttackTechniqueId,
                        principalTable: "AttackTechniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttackComponents_SystemAssets_SystemAssetId",
                        column: x => x.SystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttackConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromAttackComponentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToAttackComponentId = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemAssetRelationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttackConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttackConnections_AttackComponents_FromAttackComponentId",
                        column: x => x.FromAttackComponentId,
                        principalTable: "AttackComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttackConnections_AttackComponents_ToAttackComponentId",
                        column: x => x.ToAttackComponentId,
                        principalTable: "AttackComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttackConnections_SystemAssetRelations_SystemAssetRelationId",
                        column: x => x.SystemAssetRelationId,
                        principalTable: "SystemAssetRelations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComponentControls",
                columns: table => new
                {
                    AttackComponentId = table.Column<int>(type: "INTEGER", nullable: false),
                    CisControlId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRecommended = table.Column<bool>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentControls", x => new { x.AttackComponentId, x.CisControlId });
                    table.ForeignKey(
                        name: "FK_ComponentControls_AttackComponents_AttackComponentId",
                        column: x => x.AttackComponentId,
                        principalTable: "AttackComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComponentControls_CisControls_CisControlId",
                        column: x => x.CisControlId,
                        principalTable: "CisControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttackComponents_AttackTechniqueId",
                table: "AttackComponents",
                column: "AttackTechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_AttackComponents_SystemAssetId_AttackTechniqueId",
                table: "AttackComponents",
                columns: new[] { "SystemAssetId", "AttackTechniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttackConnections_FromAttackComponentId_ToAttackComponentId_SystemAssetRelationId",
                table: "AttackConnections",
                columns: new[] { "FromAttackComponentId", "ToAttackComponentId", "SystemAssetRelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttackConnections_SystemAssetRelationId",
                table: "AttackConnections",
                column: "SystemAssetRelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AttackConnections_ToAttackComponentId",
                table: "AttackConnections",
                column: "ToAttackComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentControls_CisControlId",
                table: "ComponentControls",
                column: "CisControlId");
            // Consolidate old assessments without losing comments or selected Controls.
            migrationBuilder.Sql("""
                INSERT INTO AttackComponents (Id, SystemAssetId, AttackTechniqueId, Likelihood, Comment)
                SELECT MIN(Id), SystemAssetId, AttackTechniqueId, MAX(Likelihood), GROUP_CONCAT(Scenario, char(10))
                FROM SystemThreats GROUP BY SystemAssetId, AttackTechniqueId;
                INSERT INTO ComponentControls (AttackComponentId, CisControlId, IsRecommended, Comment)
                SELECT component.Id, control.CisControlId, MAX(control.IsRecommended), GROUP_CONCAT(DISTINCT control.Comment)
                FROM ThreatControls control
                JOIN SystemThreats threat ON threat.Id = control.SystemThreatId
                JOIN AttackComponents component ON component.SystemAssetId = threat.SystemAssetId AND component.AttackTechniqueId = threat.AttackTechniqueId
                GROUP BY component.Id, control.CisControlId;
                """);

            migrationBuilder.DropTable(
                name: "SystemAccess");

            migrationBuilder.DropTable(
                name: "ThreatControls");

            migrationBuilder.DropTable(
                name: "SystemThreats");

            migrationBuilder.DropColumn(
                name: "BlastRadius",
                table: "SystemAssets");

            migrationBuilder.DropColumn(
                name: "BlastRadiusReason",
                table: "SystemAssets");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Attack Components consolidate assessments and remove manual System Analysis. Restore a backup to return to the old model.");
    }
}
