using FSH.WebApi.Domain.Treatment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.TreatmentPlan;
public class TreatmentPlanResponse
{
    public Guid TreatmentPlanID { get; set; }
    public Guid ProcedureID { get; set; }
    public string? ProcedureName { get; set; }
    public DateOnly StartDate { get; set; }
    public string? DoctorID { get; set; }
    public string? DoctorName { get; set; }
    public double Price { get; set; }
    public double PlanCost { get; set; }
    public string? PlanDescription { get; set; }
    public int Step { get; set; }
    public TreatmentPlanStatus Status { get; set; }
    public bool hasPrescription { get; set; } = false;
}