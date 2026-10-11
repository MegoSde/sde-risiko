using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlpacaMedic.Data.Migrations
{
    /// <inheritdoc />
    public partial class SharedBusinessAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessProcessAssets",
                columns: table => new
                {
                    BusinessProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    BusinessAssetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessProcessAssets", x => new { x.BusinessProcessId, x.BusinessAssetId });
                    table.ForeignKey(
                        name: "FK_BusinessProcessAssets_BusinessAssets_BusinessAssetId",
                        column: x => x.BusinessAssetId,
                        principalTable: "BusinessAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessProcessAssets_BusinessProcesses_BusinessProcessId",
                        column: x => x.BusinessProcessId,
                        principalTable: "BusinessProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO BusinessProcessAssets (BusinessProcessId, BusinessAssetId)
                SELECT BusinessProcessId, Id FROM BusinessAssets;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessAssets_BusinessProcesses_BusinessProcessId",
                table: "BusinessAssets");

            migrationBuilder.DropIndex(
                name: "IX_BusinessAssets_BusinessProcessId",
                table: "BusinessAssets");

            migrationBuilder.DropColumn(
                name: "BusinessProcessId",
                table: "BusinessAssets");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "BusinessAssets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAssets_NormalizedName",
                table: "BusinessAssets",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProcessAssets_BusinessAssetId",
                table: "BusinessProcessAssets",
                column: "BusinessAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Fælles forretningsassets kan ikke nedgraderes til én proces pr. asset uden datatab. Gendan den tidligere database fra backup ved tilbagerulning.");
        }
    }
}
