using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Drawing.Charts;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.DentalServices;
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
    private readonly UserManager<ApplicationUser> _userManager;

    public ServiceService(ApplicationDbContext db, IStringLocalizer<ServiceService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
    }

    public async Task CreateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        var check = await _db.Procedures.Where(p => p.Name == request.Name && p.Price == request.Price && p.Description == request.Description).FirstOrDefaultAsync();
        if (check != null)
        {
            throw new BadRequestException("Procedure information is existing!!!");
        }
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

    public async Task ModifyProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        var existingProcedure = await _db.Procedures
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Procedure with ID {request.Id} not found.");

        var isDuplicate = await _db.Procedures
            .AnyAsync(p => p.Id != request.Id &&
                          p.Name == request.Name &&
                          p.Price == request.Price &&
                          p.Description == request.Description,
                     cancellationToken);

        if (isDuplicate)
        {
            throw new ConflictException("A procedure with the same information already exists.");
        }

        var hasServiceProcedures = await _db.ServiceProcedures
            .AnyAsync(p => p.ProcedureId == request.Id, cancellationToken);

        if (hasServiceProcedures)
        {
            // Create new procedure instead of modifying existing one
            var newProcedure = new Procedure
            {
                Name = request.Name,
                Price = request.Price,
                Description = request.Description,
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.UtcNow
            };

            _db.Procedures.Add(newProcedure);

            if (request.hasService)
            {
                await UpdateServiceProcedure(
                    request.ServiceID,
                    existingProcedure.Id,
                    newProcedure.Id,
                    cancellationToken);
            }
            else
            {
                var affectedServices = await _db.ServiceProcedures
                    .Where(sp => sp.ProcedureId == existingProcedure.Id)
                    .ToListAsync(cancellationToken);

                foreach (var serviceProcedure in affectedServices)
                {
                    await UpdateServiceProcedure(
                        serviceProcedure.ServiceId!.Value,
                        existingProcedure.Id,
                        newProcedure.Id,
                        cancellationToken);
                }
            }
        }
        else
        {
            existingProcedure.Name = request.Name;
            existingProcedure.Price = request.Price;
            existingProcedure.Description = request.Description;
            existingProcedure.LastModifiedBy = _currentUserService.GetUserId();
            existingProcedure.LastModifiedOn = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var check = await _db.Services.AnyAsync(p => p.ServiceName == request.Name && p.IsActive);
        if (check) {
            throw new BadRequestException("Service Name is existing!!!");
        }
        _db.Services.Add(new Domain.Service.Service
        {
            CreatedBy = _currentUserService.GetUserId(),
            CreatedOn = DateTime.Now,
            ServiceName = request.Name,
            ServiceDescription = request.Description,
            TotalPrice = 0,
            IsActive = false,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifyServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var existing = await _db.Services.Where(p => p.Id == request.ServiceID).FirstOrDefaultAsync(cancellationToken) ?? throw new BadRequestException("Service not found.");

        if(existing.TotalPrice == 0)
        {
            var check = await _db.Services.Where(p => p.ServiceName == request.Name && p.IsActive && p.TotalPrice > 0).FirstOrDefaultAsync();
            if (check != null)
            {
                throw new BadRequestException("Service Name is existing!!!");
            }
            existing.ServiceName = request.Name ?? existing.ServiceName;
            existing.ServiceDescription = request.Description ?? existing.ServiceDescription;
            existing.LastModifiedBy = _currentUserService.GetUserId();
            existing.LastModifiedOn = DateTime.Now;
        }
        else
        {
            existing.IsActive = false;
            existing.DeletedOn = DateTime.Now;
            existing.DeletedBy = _currentUserService.GetUserId();

            var ser_pro = await _db.ServiceProcedures.Where(p => p.ServiceId == existing.Id).ToListAsync();

            var entry = _db.Services.Add(new Service
            {
                ServiceName = request.Name,
                ServiceDescription = request.Description,
                TotalPrice = existing.TotalPrice,
                IsActive = false,
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.Now,
                LastModifiedBy = _currentUserService.GetUserId(),
                LastModifiedOn = DateTime.UtcNow
            }).Entity;
            foreach (var sp in ser_pro)
            {
                await _db.ServiceProcedures.AddAsync(new ServiceProcedures
                {
                    ServiceId = entry.Id,
                    StepOrder = sp.StepOrder,
                    ProcedureId = sp.ProcedureId,
                    CreatedBy = _currentUserService.GetUserId(),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> ToggleServiceAsync(ToggleStatusRequest request, CancellationToken cancellationToken)
    {
        if (request.Id == null) {
            throw new Exception("Can not identity service.");
        }
        var existing = await _db.Services.Where(p => p.Id == Guid.Parse(request.Id)).FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
        {
            throw new Exception("Service is not found.");
        }
        if (request.Activate)
        {
            var check = await _db.Services.Where(p => p.ServiceName == existing.ServiceName && p.IsActive).FirstOrDefaultAsync();
            if (check != null)
            {
                throw new BadRequestException("Service Name is existing!!!");
            }
        }
        existing.IsActive = request.Activate;
        existing.LastModifiedBy = _currentUserService.GetUserId();
        existing.LastModifiedOn = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken);
        return _t["Delete Success"];
    }

    public async Task<ProcedureDTOs> GetProcedureByID(Guid procedureID, CancellationToken cancellationToken)
    {
        if (procedureID == Guid.Empty) {
            throw new Exception($"{nameof(Procedure)} is not empty");
        }
        var result = new ProcedureDTOs();
        var procedure = await _db.Procedures.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == procedureID, cancellationToken: cancellationToken) ?? throw new NotFoundException("Procedure Not Found.");
        var ser_pro = await _db.ServiceProcedures.Where(p => p.ProcedureId == procedureID).ToListAsync();
        var user = await _userManager.FindByIdAsync(procedure.CreatedBy.ToString());
        result.CreateDate = procedure.CreatedOn;
        result.CreateBy = user.UserName;
        result.Description = procedure.Description;
        result.Name = procedure.Name;
        result.Price = procedure.Price;
        result.Services = new List<ServiceDTOs>();
        foreach (var item in ser_pro) {
            var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == item.ServiceId);
            var u = await _userManager.FindByIdAsync(item.CreatedBy.ToString());
            result.Services.Add(new ServiceDTOs
            {
                ServiceID = service.Id,
                Name = service.ServiceName,
                Description = service.ServiceDescription,
                CreateBy = u.UserName,
                CreateDate = service.CreatedOn,
                TotalPrice = service.TotalPrice,
                IsActive = service.IsActive,
            });
        }
        return result;
    }

    public async Task<PaginationResponse<Procedure>> GetProcedurePaginationAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        var spec = new EntitiesByPaginationFilterSpec<Procedure>(request);
        var procedures = await _db.Procedures
            .AsNoTracking()
            .WithSpecification(spec)
            .ToListAsync(cancellationToken);

        int count = await _db.Procedures
            .CountAsync(cancellationToken);
        return new PaginationResponse<Procedure>(procedures, count, request.PageNumber, request.PageSize);
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
        var existing = await _db.Services.IgnoreQueryFilters().Where(p => p.Id == serviceID).FirstOrDefaultAsync(cancellationToken) ?? throw new BadRequestException("Service is not found.");
        var user = await _userManager.FindByIdAsync(existing.CreatedBy.ToString());
        var result = new ServiceDTO();
        result.ServiceID = existing.Id;
        result.Name = existing.ServiceName;
        result.CreateDate = existing.CreatedOn;
        result.CreateBy = user.UserName;
        result.TotalPrice = existing.TotalPrice;
        result.Description = existing.ServiceDescription;
        var service_procedure = await _db.ServiceProcedures.IgnoreQueryFilters().Where(p => p.ServiceId == serviceID).ToListAsync(cancellationToken);
        if (service_procedure != null) {
            result.Procedures = new List<ProcedureDTO>();
            foreach (var item in service_procedure) {
                var pro = await _db.Procedures.IgnoreQueryFilters().Where(p => p.Id == item.ProcedureId).FirstOrDefaultAsync(cancellationToken);
                var u = await _userManager.FindByIdAsync(pro.CreatedBy.ToString());
                result.Procedures.Add(new ProcedureDTO
                {
                    Description = pro.Description,
                    Name = pro.Name,
                    Price = pro.Price,
                    ProcedureID = pro.Id,
                    CreateBy = u.UserName,
                    CreateDate = pro.CreatedOn,
                });
            }
        }
        return result;
    }

    public async Task<List<Service>> GetServicesAsync()
    {
        return await _db.Services.Where(p => p.IsActive).ToListAsync();

    }

    public async Task<PaginationResponse<Service>> GetServicesPaginationAsync(PaginationFilter filter, CancellationToken cancellation)
    {
        var spec = new EntitiesByPaginationFilterSpec<Service>(filter);
        var services = await _db.Services
            .AsNoTracking()
            .WithSpecification(spec)
            .ToListAsync(cancellation);

        int count = await _db.Services
            .CountAsync(cancellation);
        return new PaginationResponse<Service>(services, count, filter.PageNumber, filter.PageSize);
    }

    public async Task<string> DeleteServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == id) ?? throw new BadRequestException("Service Not Found.");
        service.IsActive = false;
        service.DeletedBy = _currentUserService.GetUserId();
        service.DeletedOn = DateTime.Now;
        var sp = await _db.ServiceProcedures.Where(p => p.ServiceId == id).ToListAsync();
        _db.ServiceProcedures.RemoveRange(sp);
        await _db.SaveChangesAsync(cancellationToken);
        return _t["Service Deleted"];
    }

    public async Task<PaginationResponse<Service>> GetDeletedServiceAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        var spec = new EntitiesByPaginationFilterSpec<Service>(request);
        var services = await _db.Services
            .IgnoreQueryFilters()
            .AsNoTracking()
            .WithSpecification(spec)
            .Where(p => p.DeletedBy != null)
            .ToListAsync(cancellationToken);

        int count = await _db.Services
            .CountAsync(cancellationToken);
        return new PaginationResponse<Service>(services, count, request.PageNumber, request.PageSize);
    }

    public async Task<string> RestoreServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        var service = await _db.Services.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id) ?? throw new BadRequestException("Service Not Found.");
        service.DeletedBy = null;
        service.DeletedOn = null;
        service.LastModifiedBy = _currentUserService.GetUserId();
        service.LastModifiedOn = DateTime.Now;
        var sp = await _db.ServiceProcedures.IgnoreQueryFilters().Where(p => p.ServiceId == id).ToListAsync();
        foreach (var p in sp) {
            p.DeletedOn = null;
            p.DeletedBy = null;
            p.LastModifiedBy = _currentUserService.GetUserId();
            p.LastModifiedOn= DateTime.Now;
            var pro = await _db.Procedures.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == p.ProcedureId && a.DeletedBy != null);
            if (pro != null) {
                pro.DeletedOn = null;
                pro.DeletedBy = null;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        return _t["Restored Service"];
    }

    public async Task<bool> CheckExistingService(Guid serviceId)
    {
        var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == serviceId && p.IsActive);
        return service is not null;
    }

    public async Task<bool> CheckExistingProcedure(Guid procedureID)
    {
        var procedure = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == procedureID);
        return procedure is not null;
    }

    public async Task AddOrDeleteProcedureToService(AddOrDeleteProcedureToService request, CancellationToken cancellationToken)
    {
        var currentServiceProcedures = await _db.ServiceProcedures
            .Where(sp => sp.ServiceId == request.ServiceID)
            .OrderBy(sp => sp.StepOrder)
            .ToListAsync(cancellationToken);
        if (request.IsRemove)
        {
            var ser_pro = await _db.ServiceProcedures.FirstOrDefaultAsync(p => p.ServiceId == request.ServiceID && p.ProcedureId == request.ProcedureID)
                ?? throw new BadRequestException("This Procedure is not in this Service");

            var current_service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceID);
            var current_procedure = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == request.ProcedureID);

            var entry = _db.Services.Add(new Service
            {
                ServiceName = current_service.ServiceName,
                ServiceDescription = current_service.ServiceDescription,
                TotalPrice = current_service.TotalPrice - current_procedure.Price,
                IsActive = true,
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.Now,
            }).Entity;
            int step = 1;
            foreach(var item in currentServiceProcedures)
            {
                if(item.ProcedureId != request.ProcedureID)
                {
                    _db.ServiceProcedures.Add(new ServiceProcedures
                    {
                        ServiceId = entry.Id,
                        ProcedureId = item.ProcedureId,
                        StepOrder = step++,
                        CreatedBy = _currentUserService.GetUserId(),
                        CreatedOn = DateTime.UtcNow,
                        LastModifiedBy = _currentUserService.GetUserId(),
                        LastModifiedOn = DateTime.UtcNow
                    });
                }
                item.DeletedOn = DateTime.UtcNow;
                item.DeletedBy = _currentUserService.GetUserId();
            }
            _db.Services.Remove(current_service);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var ser_pro = await _db.ServiceProcedures.FirstOrDefaultAsync(p => p.ServiceId == request.ServiceID && p.ProcedureId == request.ProcedureID);
            if (ser_pro is not null)
            {
                throw new BadRequestException("The Procedure is in this Service");
            }
            var current_service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceID);
            var current_procedure = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == request.ProcedureID);

            var entry = _db.Services.Add(new Service
            {
                ServiceName = current_service.ServiceName,
                ServiceDescription = current_service.ServiceDescription,
                TotalPrice = current_service.TotalPrice + current_procedure.Price,
                IsActive = true,
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.Now,
            }).Entity;
            int step = 1;
            foreach (var item in currentServiceProcedures)
            {
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = entry.Id,
                    ProcedureId = item.ProcedureId,
                    StepOrder = step++,
                    CreatedBy = _currentUserService.GetUserId(),
                    CreatedOn = DateTime.UtcNow,
                    LastModifiedBy = _currentUserService.GetUserId(),
                    LastModifiedOn = DateTime.UtcNow
                });

                item.DeletedOn = DateTime.UtcNow;
                item.DeletedBy = _currentUserService.GetUserId();
            }
            _db.ServiceProcedures.Add(new ServiceProcedures
            {
                ServiceId = entry.Id,
                ProcedureId = current_procedure.Id,
                StepOrder = step++,
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.UtcNow,
                LastModifiedBy = _currentUserService.GetUserId(),
                LastModifiedOn = DateTime.UtcNow
            });
            _db.Services.Remove(current_service);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<string> DeleteProcedureAsync(Guid id, CancellationToken cancellationToken)
    {
        var procedure = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == id) ?? throw new BadRequestException("Procedure Not Found");
        var ser_pro = await _db.ServiceProcedures.Where(p => p.ProcedureId == id).ToListAsync();
        procedure.DeletedOn = DateTime.UtcNow;
        procedure.DeletedBy = _currentUserService.GetUserId();
        if (ser_pro != null)
        {
            foreach (var item in ser_pro)
            {
                var service = await _db.Services.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == item.ServiceId);
                service.TotalPrice -= procedure.Price;
            }
            _db.ServiceProcedures.RemoveRange(ser_pro);
        }
        return _t["Sucessfully"];
    }

    public async Task<PaginationResponse<Procedure>> GetDeletedProcedureAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        var spec = new EntitiesByPaginationFilterSpec<Procedure>(request);
        var procedures = await _db.Procedures
            .IgnoreQueryFilters()
            .AsNoTracking()
            .WithSpecification(spec)
            .Where(p => p.DeletedBy != null)
            .ToListAsync(cancellationToken);

        int count = await _db.Services
            .CountAsync(cancellationToken);
        return new PaginationResponse<Procedure>(procedures, count, request.PageNumber, request.PageSize);
    }

    public async Task<string> RestoreProcedureAsync(DefaultIdType id, CancellationToken cancellationToken)
    {
        var procedure = await _db.Procedures.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id) ?? throw new BadRequestException("Procedure Not Found.");
        procedure.DeletedBy = null;
        procedure.DeletedOn = null;
        procedure.LastModifiedBy = _currentUserService.GetUserId();
        procedure.LastModifiedOn = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        return _t["Restored"];
    }

    private async Task UpdateServiceProcedure(Guid serviceId, Guid oldProcedureId, Guid newProcedureId, CancellationToken cancellationToken)
    {
        var service = await _db.Services
            .Include(s => s.ServiceProcedures)
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken)
            ?? throw new NotFoundException($"Service with ID {serviceId} not found.");

        var oldProcedure = await _db.Procedures
            .FirstAsync(p => p.Id == oldProcedureId, cancellationToken);
        var newProcedure = await _db.Procedures
            .FirstAsync(p => p.Id == newProcedureId, cancellationToken);

        var priceDifference = newProcedure.Price - oldProcedure.Price;

        var entry = _db.Services.Add(new Service
        {
            ServiceName = service.ServiceName,
            ServiceDescription = service.ServiceDescription,
            IsActive = true,
            TotalPrice = service.TotalPrice + priceDifference,
            CreatedBy = service.CreatedBy,
            CreatedOn = service.CreatedOn,
            LastModifiedBy = _currentUserService.GetUserId(),
            LastModifiedOn = DateTime.UtcNow
        }).Entity;

        var serviceProcedure = await _db.ServiceProcedures
            .Where(sp => sp.ServiceId == serviceId).ToListAsync();

        foreach(var item in serviceProcedure)
        {
            _db.ServiceProcedures.Add(new ServiceProcedures
            {
                ServiceId = entry.Id,
                ProcedureId = (item.ProcedureId == newProcedureId) ? newProcedureId : item.ProcedureId,
                StepOrder = item.StepOrder,
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.UtcNow,
                LastModifiedBy = _currentUserService.GetUserId(),
                LastModifiedOn = DateTime.UtcNow
            });
        }
        service.IsActive = false;
        service.DeletedOn = DateTime.UtcNow;
        service.DeletedBy = _currentUserService.GetUserId();
        oldProcedure.DeletedBy = _currentUserService.GetUserId();
        oldProcedure.DeletedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
