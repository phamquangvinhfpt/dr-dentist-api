using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Services;
public class ServiceDTOs
{
    public Guid ServiceID { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string? CreateBy { get; set; }
    public DateTime CreateDate { get; set; }
    public Guid TypeServiceID { get; set; }
    public string? TypeName { get; set; }
    public bool IsActive { get; set; } = true;
    public double TotalPrice { get; set; } = 0;
}
