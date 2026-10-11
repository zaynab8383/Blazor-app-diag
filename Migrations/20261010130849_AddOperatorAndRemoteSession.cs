using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBlazorApp1.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorAndRemoteSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatorAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false),
                    PasswordHash = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false),
                    IsActive = table.Column<bool>(
                        type: "bit",
                        nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemoteDiagnosticSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperatorAccountId = table.Column<int>(
                        type: "int",
                        nullable: false),
                    DiagInfoId = table.Column<int>(
                        type: "int",
                        nullable: false),
                    SessionTokenHash = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false),
                    ExpiresAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false),
                    RevokedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_RemoteDiagnosticSessions",
                        x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorAccounts_Username",
                table: "OperatorAccounts",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemoteDiagnosticSessions_SessionTokenHash",
                table: "RemoteDiagnosticSessions",
                column: "SessionTokenHash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteDiagnosticSessions");

            migrationBuilder.DropTable(
                name: "OperatorAccounts");
        }

        /// <inheritdoc />
    }
}
