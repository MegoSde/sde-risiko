using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlpacaMedic.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAttackComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInitial",
                table: "AttackComponents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
            // Existing components on exposed assets are the best available historical startpoints.
            migrationBuilder.Sql("""
                UPDATE AttackComponents SET IsInitial = 1
                WHERE SystemAssetId IN (SELECT SystemAssetId FROM AttackSurfaceEntries WHERE IsExposed = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInitial",
                table: "AttackComponents");
        }
    }
}
