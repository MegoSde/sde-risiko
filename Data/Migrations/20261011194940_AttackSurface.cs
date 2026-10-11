using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlpacaMedic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttackSurface : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttackSurfaceEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SystemAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsExposed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExposureType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttackSurfaceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttackSurfaceEntries_SystemAssets_SystemAssetId",
                        column: x => x.SystemAssetId,
                        principalTable: "SystemAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttackSurfaceEntries_SystemAssetId",
                table: "AttackSurfaceEntries",
                column: "SystemAssetId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttackSurfaceEntries");
        }
    }
}
