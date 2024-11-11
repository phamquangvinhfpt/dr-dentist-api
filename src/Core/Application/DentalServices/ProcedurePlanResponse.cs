using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices;
public class ProcedurePlanResponse
{
    public Guid ProcedureID { get; set; }
    public string? ProcedureName { get; set; }
    public double Price { get; set; }
    public double DiscountAmount { get; set; }
    public double PlanCost { get; set; }
}
