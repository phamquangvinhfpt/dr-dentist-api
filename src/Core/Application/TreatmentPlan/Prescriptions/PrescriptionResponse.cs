using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.TreatmentPlan.Prescriptions;
public class PrescriptionResponse
{
    public string? CreateDate { get; set; }
    public string? PatientID { get; set; }
    public string? PatientName { get; set; }
    public string? DoctorID { get; set; }
    public string? DoctorName { get; set; }
    public string? Notes { get; set; }
    public List<PrescriptionItemRespomse>? Items { get; set; }
}
