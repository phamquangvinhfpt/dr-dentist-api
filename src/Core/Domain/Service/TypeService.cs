using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Domain.Service;
public class TypeService : AuditableEntity, IAggregateRoot
{
    public string? TypeName { get; set; }
    public string? TypeDescription { get; set; }
}
