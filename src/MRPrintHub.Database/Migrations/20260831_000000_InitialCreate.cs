using Microsoft.EntityFrameworkCore.Migrations;

namespace MRPrintHub.Database.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Settings",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Key = table.Column<string>(maxLength: 256, nullable: false),
                Value = table.Column<string>(maxLength: 4096, nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Settings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Sessions",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TokenHash = table.Column<string>(maxLength: 128, nullable: false),
                TokenPrefix = table.Column<string>(maxLength: 32, nullable: false),
                IpAddress = table.Column<string>(maxLength: 45, nullable: false),
                InterfaceName = table.Column<string>(maxLength: 256, nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                ExpiresAt = table.Column<DateTime>(nullable: false),
                Revoked = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Uploads",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SessionId = table.Column<int>(nullable: false),
                OriginalFilename = table.Column<string>(maxLength: 260, nullable: false),
                StoredFilename = table.Column<string>(maxLength: 260, nullable: false),
                Size = table.Column<long>(nullable: false),
                Status = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                CompletedAt = table.Column<DateTime>(nullable: true),
                ErrorReason = table.Column<string>(maxLength: 1024, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Uploads", x => x.Id);
                table.ForeignKey(
                    name: "FK_Uploads_Sessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "Sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ApplicationLogs",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Event = table.Column<string>(maxLength: 128, nullable: false),
                Message = table.Column<string>(maxLength: 4096, nullable: false),
                Level = table.Column<string>(maxLength: 32, nullable: false),
                Details = table.Column<string>(maxLength: 4096, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApplicationLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Settings_Key",
            table: "Settings",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_TokenHash",
            table: "Sessions",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Uploads_SessionId",
            table: "Uploads",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_ApplicationLogs_CreatedAt",
            table: "ApplicationLogs",
            column: "CreatedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Uploads");
        migrationBuilder.DropTable(name: "Sessions");
        migrationBuilder.DropTable(name: "ApplicationLogs");
        migrationBuilder.DropTable(name: "Settings");
    }
}
