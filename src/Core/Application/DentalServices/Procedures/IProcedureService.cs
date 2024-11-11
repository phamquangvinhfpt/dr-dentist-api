using FSH.WebApi.Domain.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Procedures;
public interface IProcedureService : ITransientService
{
    Task<List<Procedure>> GetProceduresByServiceID(Guid serviceID, CancellationToken cancellationToken);
}
