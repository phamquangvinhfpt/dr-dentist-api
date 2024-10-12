using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.MedicalHistories;
public interface IMedicalHistoryService : ITransientService
{
    Task<MedicalHistory> GetMedicalHistoryByPatientID(string patientID, CancellationToken cancellationToken);
    Task CreateAndUpdateMedicalHistory(CreateAndUpdateMedicalHistoryRequest request, CancellationToken cancellationToken);
    Task<string> DeleteMedicalHistory(string patientID, CancellationToken cancellationToken);
}
