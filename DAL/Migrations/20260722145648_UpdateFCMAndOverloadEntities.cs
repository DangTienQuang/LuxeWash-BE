using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFCMAndOverloadEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "OverloadSuggestions",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(@"
                DELETE t1 FROM UserFcmTokens t1
                INNER JOIN UserFcmTokens t2 
                WHERE t1.Id < t2.Id AND t1.Token = t2.Token;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_UserFcmTokens_Token",
                table: "UserFcmTokens",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserFcmTokens_Token",
                table: "UserFcmTokens");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "OverloadSuggestions");
        }
    }
}
