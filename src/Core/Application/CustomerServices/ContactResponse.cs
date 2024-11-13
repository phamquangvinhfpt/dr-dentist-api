using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices;
public class ContactResponse
{
    public string? StaffId { get; set; }
    public string? StaffName { get; set; }
    public Guid ContactId { get; set; }
    public DateTime CreateDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
