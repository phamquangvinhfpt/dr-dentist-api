using FSH.WebApi.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Payments;
public class ExportPaymentRequest
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? UserID { get; set; }
}
