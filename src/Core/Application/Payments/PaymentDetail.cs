using FSH.WebApi.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Payments;
public class PaymentDetail
{
    public Guid ProcedureID { get; set; }
    public string? ProcedureName { get; set; }
    public double PaymentAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
}
