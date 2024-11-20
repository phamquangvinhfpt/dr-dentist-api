using FSH.WebApi.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Payments;
public class PaymentResponse
{
    public Guid PaymentId { get; set; }
    public Guid PatientProfileId { get; set; }
    public string? PatientName { get; set; }
    public string? PatientCode { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public double DepositAmount { get; set; }
    public DateOnly? DepositDate { get; set; }
    public double RemainingAmount { get; set; }
    public double TotalAmount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
}
