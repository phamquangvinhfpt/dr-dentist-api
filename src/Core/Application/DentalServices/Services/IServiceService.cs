using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Services;
public interface IServiceService
{
    Task CreateServiceAsync();
    Task DeleteServiceAsync();
    Task UpdateServiceAsync();
}
