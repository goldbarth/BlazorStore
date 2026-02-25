using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorStore.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistIdToVideoItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ThumbnailUrl",
                table: "VideoItems",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlaylistId",
                table: "VideoItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_VideoItems_PlaylistId",
                table: "VideoItems",
                column: "PlaylistId");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoItems_Playlists_PlaylistId",
                table: "VideoItems",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoItems_Playlists_PlaylistId",
                table: "VideoItems");

            migrationBuilder.DropIndex(
                name: "IX_VideoItems_PlaylistId",
                table: "VideoItems");

            migrationBuilder.DropColumn(
                name: "PlaylistId",
                table: "VideoItems");

            migrationBuilder.AlterColumn<string>(
                name: "ThumbnailUrl",
                table: "VideoItems",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 2000);
        }
    }
}
