using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.ApplicationForms;
public interface IApplicationFormService : ITransientService
{
    Task<PaginationResponse<FormDetailResponse>> GetFormDetails(PaginationFilter filter, CancellationToken cancellationToken);
    Task<string> AddFormAsync(AddFormRequest form, CancellationToken cancellationToken);
    Task<string> ToggleFormAsync(ToggleFormRequest form, CancellationToken cancellationToken);
}
