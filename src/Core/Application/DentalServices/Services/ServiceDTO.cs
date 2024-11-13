using FSH.WebApi.Application.DentalServices.Procedures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Services;
public class ServiceDTO
{
    public Guid ServiceID { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string? CreateBy { get; set; }
    public DateTime CreateDate { get; set; }
    public bool IsActive { get; set; } = true;
    public double TotalPrice { get; set; } = 0;
    public List<ProcedureDTO>? Procedures { get; set; }
}
