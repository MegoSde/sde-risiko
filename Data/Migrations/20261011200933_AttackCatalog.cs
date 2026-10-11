using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlpacaMedic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttackCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSubTechnique",
                table: "AttackTechniques",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParentTechniqueId",
                table: "AttackTechniques",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Platforms",
                table: "AttackTechniques",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AttackTactics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShortName = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttackTactics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttackTechniqueTactics",
                columns: table => new
                {
                    AttackTechniqueId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttackTacticId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttackTechniqueTactics", x => new { x.AttackTechniqueId, x.AttackTacticId });
                    table.ForeignKey(
                        name: "FK_AttackTechniqueTactics_AttackTactics_AttackTacticId",
                        column: x => x.AttackTacticId,
                        principalTable: "AttackTactics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttackTechniqueTactics_AttackTechniques_AttackTechniqueId",
                        column: x => x.AttackTechniqueId,
                        principalTable: "AttackTechniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttackTechniques_TechniqueId",
                table: "AttackTechniques",
                column: "TechniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttackTactics_ShortName",
                table: "AttackTactics",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttackTechniqueTactics_AttackTacticId",
                table: "AttackTechniqueTactics",
                column: "AttackTacticId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttackTechniqueTactics");

            migrationBuilder.DropTable(
                name: "AttackTactics");

            migrationBuilder.DropIndex(
                name: "IX_AttackTechniques_TechniqueId",
                table: "AttackTechniques");

            migrationBuilder.DropColumn(
                name: "IsSubTechnique",
                table: "AttackTechniques");

            migrationBuilder.DropColumn(
                name: "ParentTechniqueId",
                table: "AttackTechniques");

            migrationBuilder.DropColumn(
                name: "Platforms",
                table: "AttackTechniques");
        }
    }
}
