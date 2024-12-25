using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class AppointmentResponse
{
    public string? PatientAvatar { get; set; }
    public string? PatientUserID { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public string? PatientCode { get; set; }
    public string? PatientName { get; set; }
    public string? PatientPhone { get; set; }
    public Guid DentistId { get; set; }
    public string? DentistName { get; set; }
    public Guid ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public AppointmentStatus Status { get; set; }
    public AppointmentType Type { get; set; }
    public string? Notes { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public double ServicePrice { get; set; }
    public bool canFeedback { get; set; }
    public bool isFeedback { get; set; }
    public Guid RoomID { get; set; }
    public string? RoomName { get; set; }
}
