using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyPlayerOne.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSessionsAndTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionParticipants_Teams_TeamId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_Teams_GameSessionId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_TeamId",
                table: "SessionParticipants");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teams",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsReady",
                table: "SessionParticipants",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "GameSessions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "GameSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Mode",
                table: "GameSessions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Teams_GameSessionId_Id",
                table: "Teams",
                columns: new[] { "GameSessionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_GameSessionId_Name",
                table: "Teams",
                columns: new[] { "GameSessionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_GameSessionId_TeamId",
                table: "SessionParticipants",
                columns: new[] { "GameSessionId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_CreatedAt",
                table: "GameSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Status",
                table: "GameSessions",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionParticipants_Teams_GameSessionId_TeamId",
                table: "SessionParticipants",
                columns: new[] { "GameSessionId", "TeamId" },
                principalTable: "Teams",
                principalColumns: new[] { "GameSessionId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionParticipants_Teams_GameSessionId_TeamId",
                table: "SessionParticipants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Teams_GameSessionId_Id",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_GameSessionId_Name",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_GameSessionId_TeamId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_CreatedAt",
                table: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_Status",
                table: "GameSessions");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teams",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "IsReady",
                table: "SessionParticipants",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "GameSessions",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "GameSessions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "Mode",
                table: "GameSessions",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_GameSessionId",
                table: "Teams",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_TeamId",
                table: "SessionParticipants",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionParticipants_Teams_TeamId",
                table: "SessionParticipants",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");
        }
    }
}
