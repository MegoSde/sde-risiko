using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlpacaMedic.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSystemAssetZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve legacy labels as explicit network assets and directed placement relations.
            migrationBuilder.Sql("""
                INSERT INTO SystemAssets (Name, Description, AssetType, Zone, BlastRadius, BlastRadiusReason)
                SELECT MIN(TRIM(source.Zone)), '', 'Network', '', '1.0', ''
                FROM SystemAssets AS source
                WHERE TRIM(source.Zone) <> '' AND NOT EXISTS (
                    SELECT 1 FROM SystemAssets AS network
                    WHERE network.AssetType = 'Network'
                      AND TRIM(network.Name) = TRIM(source.Zone) COLLATE NOCASE
                )
                GROUP BY TRIM(source.Zone) COLLATE NOCASE;

                WITH placements AS (
                    SELECT source.Id AS SourceId, (
                        SELECT MIN(network.Id) FROM SystemAssets AS network
                        WHERE network.AssetType = 'Network'
                          AND TRIM(network.Name) = TRIM(source.Zone) COLLATE NOCASE
                    ) AS TargetId
                    FROM SystemAssets AS source WHERE TRIM(source.Zone) <> ''
                )
                INSERT INTO SystemAssetRelations (SourceSystemAssetId, TargetSystemAssetId, RelationType, Description)
                SELECT SourceId, TargetId, 'LocatedIn', '' FROM placements
                WHERE SourceId <> TargetId AND NOT EXISTS (
                    SELECT 1 FROM SystemAssetRelations AS relation
                    WHERE relation.SourceSystemAssetId = SourceId
                      AND relation.TargetSystemAssetId = TargetId
                      AND relation.RelationType = 'LocatedIn'
                );
                """);

            migrationBuilder.DropColumn(
                name: "Zone",
                table: "SystemAssets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Zone",
                table: "SystemAssets",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
