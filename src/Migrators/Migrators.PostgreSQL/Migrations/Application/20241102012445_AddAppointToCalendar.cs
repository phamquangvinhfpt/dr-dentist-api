using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Migrators.PostgreSQL.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddAppointToCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                schema: "Identity",
                table: "WorkingCalendar",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendar_AppointmentId",
                schema: "Identity",
                table: "WorkingCalendar",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkingCalendar_Appointment_AppointmentId",
                schema: "Identity",
                table: "WorkingCalendar",
                column: "AppointmentId",
                principalSchema: "Treatment",
                principalTable: "Appointment",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkingCalendar_Appointment_AppointmentId",
                schema: "Identity",
                table: "WorkingCalendar");

            migrationBuilder.DropIndex(
                name: "IX_WorkingCalendar_AppointmentId",
                schema: "Identity",
                table: "WorkingCalendar");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                schema: "Identity",
                table: "WorkingCalendar");
        }
    }
}
