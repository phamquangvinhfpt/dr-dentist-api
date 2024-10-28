using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Migrators.PostgreSQL.Migrations.Application
{
    /// <inheritdoc />
    public partial class Modified_Prescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Prescription_RecordId",
                schema: "Treatment",
                table: "Prescription",
                column: "RecordId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescription_MedicalRecord_RecordId",
                schema: "Treatment",
                table: "Prescription",
                column: "RecordId",
                principalSchema: "Treatment",
                principalTable: "MedicalRecord",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prescription_MedicalRecord_RecordId",
                schema: "Treatment",
                table: "Prescription");

            migrationBuilder.DropIndex(
                name: "IX_Prescription_RecordId",
                schema: "Treatment",
                table: "Prescription");
        }
    }
}
