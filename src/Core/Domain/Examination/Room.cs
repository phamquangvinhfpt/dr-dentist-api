using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Domain.Examination;
public class Room : AuditableEntity, IAggregateRoot
{
    public string? RoomName { get; set; }
    public bool Status { get; set; }
}
