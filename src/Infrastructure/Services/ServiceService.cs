using Ardalis.Specification.EntityFrameworkCore;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.DentalServices.Procedures;
using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Services;
internal class ServiceService : IServiceService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer _t;
    private readonly ICurrentUser _currentUserService;

    public ServiceService(ApplicationDbContext db, IStringLocalizer<ServiceService> t, ICurrentUser currentUserService)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
    }

    public async Task CreateOrUpdateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        if (request.isDuplicate)
        {
            var entry = _db.Procedures.Add(new Procedure
            {
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.Now,
                Name = request.Name,
                Price = request.Price,
                Description = request.Description,
            }).Entity;
            await _db.SaveChangesAsync(cancellationToken);
            if (request.hasService)
            {
                var service_procedure = await _db.ServiceProcedures.FirstOrDefaultAsync(p => p.ServiceId == request.ServiceID && p.ProcedureId == request.Id);
                if (service_procedure != null)
                {
                    service_procedure.ProcedureId = entry.Id;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }
        else {
            if (request.Id == Guid.Empty) {
                _db.Procedures.Add(new Procedure
                {
                    CreatedBy = _currentUserService.GetUserId(),
                    CreatedOn = DateTime.Now,
                    Name = request.Name,
                    Price = request.Price,
                    Description = request.Description,
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var existing = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == request.Id) ?? throw new Exception("Procedure not found.");
                existing.Name = request.Name ?? existing.Name;
                existing.Price = request.Price;
                existing.Description = request.Description ?? existing.Description;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public async Task CreateOrUpdateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var existing = await _db.Services.Where(p => p.Id == request.ServiceID).FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
        {
            _db.Services.Add(new Domain.Service.Service
            {
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.Now,
                ServiceName = request.Name,
                ServiceDescription = request.Description,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            existing.ServiceName = request.Name ?? existing.ServiceName;
            existing.ServiceDescription = request.Description ?? existing.ServiceDescription;
            existing.LastModifiedBy = _currentUserService.GetUserId();
            existing.LastModifiedOn = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<string> DeleteServiceAsync(Guid serviceID, CancellationToken cancellationToken)
    {
        if (serviceID == Guid.Empty) {
            throw new Exception("Can not identity service.");
        }
        var existing = await _db.Services.Where(p => p.Id == serviceID).FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
        {
            throw new Exception("Service is not found.");
        }

        existing.DeletedOn = DateTime.Now;
        existing.DeletedBy = _currentUserService.GetUserId();

        await _db.SaveChangesAsync(cancellationToken);
        return _t["Delete Success"];
    }

    public async Task<Procedure> GetProcedureByID(Guid procedureID, CancellationToken cancellationToken)
    {
        if (procedureID == Guid.Empty) {
            throw new Exception($"{nameof(Procedure)} is not empty");
        }
        return await _db.Procedures.FirstOrDefaultAsync(p => p.Id == procedureID, cancellationToken: cancellationToken);
    }

    public async Task<PaginationResponse<Procedure>> GetProcedurePaginationAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        var list = new List<Procedure>();
        var spec = new EntitiesByPaginationFilterSpec<Procedure>(request);
        var procedures = await _db.Procedures
            .AsNoTracking()
            .WithSpecification(spec)
            .ToListAsync(cancellationToken);

        int count = await _db.Procedures
            .CountAsync(cancellationToken);
        return new PaginationResponse<Procedure>(list, count, request.PageNumber, request.PageSize);
    }

    public async Task<List<Procedure>> GetProceduresAsync(CancellationToken cancellationToken)
    {
        return await _db.Procedures
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceDTO> GetServiceByID(Guid serviceID, CancellationToken cancellationToken)
    {
        if (serviceID == Guid.Empty)
        {
            throw new Exception("Can not identity service.");
        }
        var existing = await _db.Services.Where(p => p.Id == serviceID).FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
        {
            throw new Exception("Service is not found.");
        }
        var result = new ServiceDTO();
        result.ServiceID = existing.Id;
        result.Name = existing.ServiceName;
        result.Description = existing.ServiceDescription;
        var service_procedure = await _db.ServiceProcedures.Where(p => p.ServiceId == serviceID).ToListAsync(cancellationToken);
        if (service_procedure != null) {
            foreach (var item in service_procedure) {
                var pro = await _db.Procedures.Where(p => p.Id == item.ProcedureId).FirstOrDefaultAsync(cancellationToken);

                result.Procedures.Add(new ProcedureDTO
                {
                    Description = pro.Description,
                    Name = pro.Name,
                    Price = pro.Price,
                    ProcedureID = pro.Id
                });
            }
        }
        return result;
    }

    public async Task<List<Service>> GetServicesAsync(CancellationToken cancellationToken)
    {
        return await _db.Services
            .ToListAsync(cancellationToken);
    }

    public async Task<PaginationResponse<Service>> GetServicesPaginationAsync(PaginationFilter filter, CancellationToken cancellation)
    {
        var list = new List<Service>();
        var spec = new EntitiesByPaginationFilterSpec<Service>(filter);
        var services = await _db.Services
            .AsNoTracking()
            .WithSpecification(spec)
            .ToListAsync(cancellation);

        int count = await _db.Services
            .CountAsync(cancellation);
        return new PaginationResponse<Service>(list, count, filter.PageNumber, filter.PageSize);
    }
}
