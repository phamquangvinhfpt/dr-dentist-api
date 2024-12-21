using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office.CustomUI;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Application.DentalServices;
using FSH.WebApi.Application.DentalServices.Procedures;
using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ServiceService> _logger;

    public ServiceService(ApplicationDbContext db, IStringLocalizer<ServiceService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<ServiceService> logger)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task CreateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task ModifyProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existingProcedure = await _db.Procedures
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException($"Procedure with ID {request.Id} not found.");

            bool isDuplicate = await _db.Procedures
                .AnyAsync(p => p.Id != request.Id &&
                              p.Name == request.Name &&
                              p.Price == request.Price &&
                              p.Description == request.Description,
                         cancellationToken);

            if (isDuplicate)
            {
                throw new ConflictException("A procedure with the same information already exists.");
            }

            bool hasServiceProcedures = await _db.ServiceProcedures
                .AnyAsync(p => p.ProcedureId == request.Id, cancellationToken);
            var id = request.Id;
            if (hasServiceProcedures)
            {
                var newProcedure = new Procedure
                {
                    Name = request.Name,
                    Price = request.Price,
                    Description = request.Description,
                    CreatedBy = _currentUserService.GetUserId(),
                    CreatedOn = DateTime.UtcNow
                };

                var entry = _db.Procedures.Add(newProcedure).Entity;
                id = entry.Id;
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
            if (request.hasService)
            {
                var service = await _db.Services
                .FirstOrDefaultAsync(s => s.Id == request.ServiceID, cancellationToken);
                var oldProcedure = await _db.Procedures
                    .FirstAsync(p => p.Id == existingProcedure.Id, cancellationToken);
                var newProcedure = await _db.Procedures
                    .FirstAsync(p => p.Id == id, cancellationToken);

                double priceDifference = newProcedure.Price - oldProcedure.Price;

                bool wasUsed = await _db.Appointments.AnyAsync(p => p.ServiceId == request.ServiceID);

                if (!wasUsed)
                {
                    service.TotalPrice += priceDifference;
                    var serviceProcedure = await _db.ServiceProcedures
                        .FirstOrDefaultAsync(sp => sp.ServiceId == request.ServiceID && sp.ProcedureId == existingProcedure.Id);

                    serviceProcedure.ProcedureId = id;
                }
                else
                {
                    var s = new Service
                    {
                        ServiceName = service.ServiceName,
                        ServiceDescription = service.ServiceDescription,
                        IsActive = true,
                        TotalPrice = service.TotalPrice + priceDifference,
                        CreatedBy = service.CreatedBy,
                        CreatedOn = service.CreatedOn,
                        LastModifiedBy = _currentUserService.GetUserId(),
                        LastModifiedOn = DateTime.UtcNow,
                        TypeServiceID = service.TypeServiceID,
                    };
                    var entry = _db.Services.Add(s).Entity;

                    var serviceProcedure = await _db.ServiceProcedures
                        .Where(sp => sp.ServiceId == request.ServiceID).ToListAsync();

                    foreach (var item in serviceProcedure)
                    {
                        _db.ServiceProcedures.Add(new ServiceProcedures
                        {
                            ServiceId = entry.Id,
                            ProcedureId = (item.ProcedureId == existingProcedure.Id) ? id : item.ProcedureId,
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
                }
                await _db.SaveChangesAsync();
            }
            else
            {
                var affectedServices = await _db.ServiceProcedures
                    .Where(sp => sp.ProcedureId == existingProcedure.Id)
                    .ToListAsync(cancellationToken);
                var newProcedure = await _db.Procedures
                        .FirstAsync(p => p.Id == id, cancellationToken);
                foreach (var sp in affectedServices)
                {
                    var service = await _db.Services
                        .FirstOrDefaultAsync(s => s.Id == sp.ServiceId, cancellationToken);
                    var oldProcedure = await _db.Procedures
                        .FirstAsync(p => p.Id == sp.ProcedureId, cancellationToken);

                    double priceDifference = newProcedure.Price - oldProcedure.Price;

                    bool wasUsed = await _db.Appointments.AnyAsync(p => p.ServiceId == sp.ServiceId);

                    if (!wasUsed)
                    {
                        service.TotalPrice += priceDifference;
                        var serviceProcedure = await _db.ServiceProcedures
                            .FirstOrDefaultAsync(s => s.ServiceId == sp.ServiceId && s.ProcedureId == existingProcedure.Id);

                        serviceProcedure.ProcedureId = id;
                    }
                    else
                    {
                        var s = new Service
                        {
                            ServiceName = service.ServiceName,
                            ServiceDescription = service.ServiceDescription,
                            IsActive = true,
                            TotalPrice = service.TotalPrice + priceDifference,
                            CreatedBy = service.CreatedBy,
                            CreatedOn = service.CreatedOn,
                            LastModifiedBy = _currentUserService.GetUserId(),
                            LastModifiedOn = DateTime.UtcNow,
                            TypeServiceID = service.TypeServiceID,
                        };
                        var entry = _db.Services.Add(s).Entity;

                        var serviceProcedure = await _db.ServiceProcedures
                            .Where(sp => sp.ServiceId == request.ServiceID).ToListAsync();

                        foreach (var item in serviceProcedure)
                        {
                            _db.ServiceProcedures.Add(new ServiceProcedures
                            {
                                ServiceId = entry.Id,
                                ProcedureId = (item.ProcedureId == existingProcedure.Id) ? id : item.ProcedureId,
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
                        oldProcedure.DeletedOn = DateTime.Now;

                    }
                    await _db.SaveChangesAsync();
                }
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            bool check = await _db.Services.AnyAsync(p => p.ServiceName == request.Name && p.IsActive);
            if (check)
            {
                throw new BadRequestException("Service Name is existing!!!");
            }
            var type = await _db.TypeServices.FirstOrDefaultAsync(p => p.Id == request.TypeID);
            if (type == null)
            {
                throw new Exception("Warning: Error when identity type service.");
            }
            _db.Services.Add(new Domain.Service.Service
            {
                CreatedBy = _currentUserService.GetUserId(),
                CreatedOn = DateTime.Now,
                ServiceName = request.Name,
                ServiceDescription = request.Description,
                TotalPrice = 0,
                IsActive = false,
                TypeServiceID = request.TypeID,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task ModifyServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await _db.Services.Where(p => p.Id == request.ServiceID).FirstOrDefaultAsync(cancellationToken) ?? throw new BadRequestException("Service not found.");

            if (existing.TotalPrice == 0)
            {
                var check = await _db.Services.Where(p => p.ServiceName == request.Name && p.IsActive).FirstOrDefaultAsync();
                if (check != null)
                {
                    throw new BadRequestException("Service Name is existing!!!");
                }
                var type = await _db.TypeServices.FirstOrDefaultAsync(p => p.Id == existing.TypeServiceID);
                if (type == null)
                {
                    throw new Exception("Warning: Error when identity type service.");
                }
                existing.ServiceName = request.Name ?? existing.ServiceName;
                existing.ServiceDescription = request.Description ?? existing.ServiceDescription;
                existing.LastModifiedBy = _currentUserService.GetUserId();
                existing.LastModifiedOn = DateTime.Now;
                existing.TypeServiceID = type.Id;
            }
            else
            {
                var check = await _db.Services.Where(p => p.ServiceName == request.Name && p.IsActive).FirstOrDefaultAsync();
                if (check != null)
                {
                    throw new BadRequestException("Service Name is existing!!!");
                }
                bool wasUse = await _db.Appointments.AnyAsync(p => p.ServiceId ==  request.ServiceID);
                var type = await _db.TypeServices.FirstOrDefaultAsync(p => p.Id == existing.TypeServiceID);
                if (type == null)
                {
                    throw new Exception("Warning: Error when identity type service.");
                }
                if (wasUse) {
                    existing.IsActive = false;
                    existing.DeletedOn = DateTime.Now;
                    existing.DeletedBy = _currentUserService.GetUserId();
                    var ser_pro = await _db.ServiceProcedures.Where(p => p.ServiceId == existing.Id).ToListAsync();

                    var entry = _db.Services.Add(new Service
                    {
                        ServiceName = request.Name,
                        ServiceDescription = request.Description,
                        TotalPrice = existing.TotalPrice,
                        IsActive = true,
                        CreatedBy = _currentUserService.GetUserId(),
                        CreatedOn = DateTime.Now,
                        LastModifiedBy = _currentUserService.GetUserId(),
                        LastModifiedOn = DateTime.UtcNow,
                        TypeServiceID = type.Id
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
                else
                {
                    existing.ServiceName = request.Name ?? existing.ServiceName;
                    existing.ServiceDescription = request.Description ?? existing.ServiceDescription;
                    existing.LastModifiedBy = _currentUserService.GetUserId();
                    existing.LastModifiedOn = DateTime.Now;
                    existing.TypeServiceID = type.Id;
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task<string> ToggleServiceAsync(ToggleStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Id == null)
            {
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
                if(existing.TotalPrice == 0)
                {
                    throw new BadRequestException("Warning: Can not activate service when service price is 0 !!!!");
                }
            }
            existing.IsActive = request.Activate;
            existing.LastModifiedBy = _currentUserService.GetUserId();
            existing.LastModifiedOn = DateTime.Now;

            await _db.SaveChangesAsync(cancellationToken);
            return _t["Success"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<ProcedureDTOs> GetProcedureByID(Guid procedureID, CancellationToken cancellationToken)
    {
        try
        {
            if (procedureID == Guid.Empty)
            {
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
            foreach (var item in ser_pro)
            {
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
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
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
        var existing = await _db.Services.Where(p => p.Id == serviceID).FirstOrDefaultAsync(cancellationToken) ?? throw new BadRequestException("Service is not found.");
        var type = await _db.TypeServices.FirstOrDefaultAsync(p => p.Id == existing.TypeServiceID);
        if(type == null)
        {
            throw new Exception("Warning: Error when identity type service.");
        }
        var user = await _userManager.FindByIdAsync(existing.CreatedBy.ToString());
        var result = new ServiceDTO();
        result.ServiceID = existing.Id;
        result.Name = existing.ServiceName;
        result.CreateDate = existing.CreatedOn;
        result.CreateBy = $"{user.FirstName} {user.LastName}";
        result.TotalPrice = existing.TotalPrice;
        result.Description = existing.ServiceDescription;
        result.TypeName = type.TypeName;
        result.TypeServiceID = type.Id;
        result.IsActive = existing.IsActive;
        var service_procedure = await _db.ServiceProcedures.Where(p => p.ServiceId == serviceID).ToListAsync(cancellationToken);
        if (service_procedure != null) {
            result.Procedures = new List<ProcedureDTO>();
            foreach (var item in service_procedure) {
                var pro = await _db.Procedures.Where(p => p.Id == item.ProcedureId).FirstOrDefaultAsync(cancellationToken);
                var u = await _userManager.FindByIdAsync(pro.CreatedBy.ToString());
                result.Procedures.Add(new ProcedureDTO
                {
                    Description = pro.Description,
                    Name = pro.Name,
                    Price = pro.Price,
                    ProcedureID = pro.Id,
                    CreateBy = $"{u.FirstName} {u.LastName}",
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

    public async Task<PaginationResponse<ServiceDTOs>> GetServicesPaginationAsync(PaginationFilter filter, CancellationToken cancellation)
    {
        var spec = new EntitiesByPaginationFilterSpec<Service>(filter);
        var result = new List<ServiceDTOs>();
        var services = await _db.Services
            .AsNoTracking()
            .WithSpecification(spec)
            .ToListAsync(cancellation);

        int count = await _db.Services
            .CountAsync(cancellation);

        foreach(var item in services)
        {
            var type = await _db.TypeServices.FirstOrDefaultAsync(p => p.Id == item.TypeServiceID);
            var user = await _userManager.FindByIdAsync(item.CreatedBy.ToString());
            result.Add(new ServiceDTOs
            {
                TypeServiceID = item.TypeServiceID,
                TypeName = type.TypeName,
                CreateDate = item.CreatedOn,
                Description = item.ServiceDescription,
                IsActive = item.IsActive,
                Name = item.ServiceName,
                ServiceID = item.Id,
                TotalPrice = item.TotalPrice,
                CreateBy = $"{user.FirstName} {user.LastName}"
            });
        }
        return new PaginationResponse<ServiceDTOs>(result, count, filter.PageNumber, filter.PageSize);
    }

    public async Task<string> DeleteServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == id) ?? throw new BadRequestException("Service Not Found.");
            service.IsActive = false;
            service.DeletedBy = _currentUserService.GetUserId();
            service.DeletedOn = DateTime.Now;
            //var sp = await _db.ServiceProcedures.Where(p => p.ServiceId == id).ToListAsync();
            //_db.ServiceProcedures.RemoveRange(sp);
            await _db.SaveChangesAsync(cancellationToken);
            return _t["Service Deleted"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<PaginationResponse<ServiceDTOs>> GetDeletedServiceAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<Service>(request);
            var result = new List<ServiceDTOs>();
            var services = await _db.Services
                .IgnoreQueryFilters()
                .AsNoTracking()
                .WithSpecification(spec)
                .Where(p => p.DeletedBy != null)
                .ToListAsync(cancellationToken);

            int count = await _db.Services.Where(p => p.DeletedBy != null)
                .CountAsync(cancellationToken);
            foreach (var item in services)
            {
                var type = await _db.TypeServices.FirstOrDefaultAsync(p => p.Id == item.TypeServiceID);
                var user = await _userManager.FindByIdAsync(item.CreatedBy.ToString());
                result.Add(new ServiceDTOs
                {
                    TypeServiceID = item.TypeServiceID,
                    TypeName = type.TypeName,
                    CreateDate = item.CreatedOn,
                    Description = item.ServiceDescription,
                    IsActive = item.IsActive,
                    Name = item.ServiceName,
                    ServiceID = item.Id,
                    TotalPrice = item.TotalPrice,
                    CreateBy = $"{user.FirstName} {user.LastName}"
                });
            }
            return new PaginationResponse<ServiceDTOs>(result, count, request.PageNumber, request.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<string> RestoreServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var service = await _db.Services.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id) ?? throw new BadRequestException("Service Not Found.");

            if(service == null)
            {
                throw new BadRequestException("Service is not existing!!!");
            }

            var check = await _db.Services.Where(p => p.ServiceName == service.ServiceName && p.IsActive).FirstOrDefaultAsync();
            if (check != null)
            {
                throw new BadRequestException("Existing same service!!!");
            }

            service.DeletedBy = null;
            service.DeletedOn = null;
            service.LastModifiedBy = _currentUserService.GetUserId();
            service.LastModifiedOn = DateTime.Now;
            //var sp = await _db.ServiceProcedures.IgnoreQueryFilters().Where(p => p.ServiceId == id).ToListAsync();
            //foreach (var p in sp)
            //{
            //    p.DeletedOn = null;
            //    p.DeletedBy = null;
            //    p.LastModifiedBy = _currentUserService.GetUserId();
            //    p.LastModifiedOn = DateTime.Now;
            //    var pro = await _db.Procedures.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == p.ProcedureId && a.DeletedBy != null);
            //    if (pro != null)
            //    {
            //        pro.DeletedOn = null;
            //        pro.DeletedBy = null;
            //    }
            //}
            await _db.SaveChangesAsync(cancellationToken);
            return _t["Restored Service"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<bool> CheckExistingService(Guid serviceId)
    {
        var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == serviceId);
        return service is not null;
    }

    public async Task<bool> CheckExistingProcedure(Guid procedureID)
    {
        var procedure = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == procedureID);
        return procedure is not null;
    }

    public async Task<ServiceDTO> AddOrDeleteProcedureToService(AddOrDeleteProcedureToService request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var currentServiceProcedures = await _db.ServiceProcedures
                .Where(sp => sp.ServiceId == request.ServiceID)
                .OrderBy(sp => sp.StepOrder)
                .ToListAsync(cancellationToken);
            bool wasUsed = await _db.Appointments.AnyAsync(p => p.ServiceId == request.ServiceID);
            var id = request.ServiceID;
            if (request.IsRemove)
            {
                if (!wasUsed)
                {
                    var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceID);
                    foreach (var item in request.ProcedureID)
                    {
                        var sp = await _db.ServiceProcedures.FirstOrDefaultAsync(p => p.ServiceId == request.ServiceID && p.ProcedureId == item)
                            ?? throw new BadRequestException("This Procedure is not in this Service");
                        var procedure = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == item);
                        service.TotalPrice -= procedure.Price;
                        _db.ServiceProcedures.Remove(sp);
                    }
                }
                else
                {
                    var current_service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceID);

                    var newService = new Service
                    {
                        ServiceName = current_service.ServiceName,
                        ServiceDescription = current_service.ServiceDescription,
                        TotalPrice = current_service.TotalPrice,
                        IsActive = true,
                        CreatedBy = _currentUserService.GetUserId(),
                        CreatedOn = DateTime.Now,
                    };
                    var entry = _db.Services.Add(newService).Entity;

                    int step = 1;
                    foreach (var currentProcedure in currentServiceProcedures)
                    {
                        if (!request.ProcedureID.Contains(currentProcedure.ProcedureId.Value))
                        {
                            _db.ServiceProcedures.Add(new ServiceProcedures
                            {
                                ServiceId = entry.Id,
                                ProcedureId = currentProcedure.ProcedureId,
                                StepOrder = step++,
                                CreatedBy = _currentUserService.GetUserId(),
                                CreatedOn = DateTime.UtcNow,
                                LastModifiedBy = _currentUserService.GetUserId(),
                                LastModifiedOn = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            var procedure = await _db.Procedures.FirstOrDefaultAsync(p =>
                                p.Id == currentProcedure.ProcedureId);
                            newService.TotalPrice -= procedure.Price;
                        }
                    }
                    current_service.IsActive = false;
                    current_service.DeletedOn = DateTime.UtcNow;
                    current_service.DeletedBy = _currentUserService.GetUserId();
                    id = entry.Id;
                }
            }
            else
            {
                if (!wasUsed)
                {
                    var current_service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceID);
                    int lastStep = currentServiceProcedures.Any() ?
                        currentServiceProcedures.Max(x => x.StepOrder) : 0;

                    foreach (var procedureId in request.ProcedureID)
                    {
                        var ser_pro = await _db.ServiceProcedures.FirstOrDefaultAsync(p =>
                        p.ServiceId == request.ServiceID && p.ProcedureId == procedureId);
                        if (ser_pro is not null)
                        {
                            throw new BadRequestException("The Procedure is already in this Service");
                        }

                        ser_pro = await _db.ServiceProcedures.IgnoreQueryFilters().FirstOrDefaultAsync(p =>
                                    p.ServiceId == request.ServiceID && p.ProcedureId == procedureId);
                        var current_procedure = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == procedureId);
                        if (ser_pro is not null)
                        {
                            ser_pro.DeletedOn = null;
                            ser_pro.DeletedBy = null;
                        }
                        else
                        {
                            _db.ServiceProcedures.Add(new ServiceProcedures
                            {
                                ServiceId = request.ServiceID,
                                ProcedureId = procedureId,
                                StepOrder = ++lastStep,
                                CreatedBy = _currentUserService.GetUserId(),
                                CreatedOn = DateTime.UtcNow,
                                LastModifiedBy = _currentUserService.GetUserId(),
                                LastModifiedOn = DateTime.UtcNow
                            });
                        }
                        current_service.TotalPrice += current_procedure.Price;
                    }
                }
                else
                {
                    var current_service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceID);

                    var newService = new Service
                    {
                        ServiceName = current_service.ServiceName,
                        ServiceDescription = current_service.ServiceDescription,
                        TotalPrice = current_service.TotalPrice,
                        IsActive = true,
                        CreatedBy = _currentUserService.GetUserId(),
                        CreatedOn = DateTime.Now,
                    };

                    var entry = _db.Services.Add(newService).Entity;

                    int step = 1;
                    foreach (var currentProcedure in currentServiceProcedures)
                    {
                        _db.ServiceProcedures.Add(new ServiceProcedures
                        {
                            ServiceId = entry.Id,
                            ProcedureId = currentProcedure.ProcedureId,
                            StepOrder = step++,
                            CreatedBy = _currentUserService.GetUserId(),
                            CreatedOn = DateTime.UtcNow,
                            LastModifiedBy = _currentUserService.GetUserId(),
                            LastModifiedOn = DateTime.UtcNow
                        });
                    }

                    foreach (var procedureId in request.ProcedureID)
                    {
                        var current_procedure = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == procedureId);

                        _db.ServiceProcedures.Add(new ServiceProcedures
                        {
                            ServiceId = entry.Id,
                            ProcedureId = procedureId,
                            StepOrder = step++,
                            CreatedBy = _currentUserService.GetUserId(),
                            CreatedOn = DateTime.UtcNow,
                            LastModifiedBy = _currentUserService.GetUserId(),
                            LastModifiedOn = DateTime.UtcNow
                        });

                        newService.TotalPrice += current_procedure.Price;
                    }

                    current_service.IsActive = false;
                    current_service.DeletedOn = DateTime.UtcNow;
                    current_service.DeletedBy = _currentUserService.GetUserId();
                    id = entry.Id;
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetServiceByID(id, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<string> DeleteProcedureAsync(Guid id, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
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
                    if (service.DeletedBy != null)
                    {
                        _db.ServiceProcedures.Remove(item);
                        service.TotalPrice -= procedure.Price;
                    }
                    else
                    {
                        bool wasUse = await _db.Appointments.AnyAsync(p => p.ServiceId == item.ServiceId);
                        if (wasUse)
                        {
                            service.IsActive = false;
                            service.DeletedOn = DateTime.Now;
                            service.DeletedBy = _currentUserService.GetUserId();

                            var sp = await _db.ServiceProcedures.Where(p => p.ServiceId == service.Id).OrderBy(p => p.StepOrder).ToListAsync();

                            var entry = _db.Services.Add(new Service
                            {
                                ServiceName = service.ServiceName,
                                ServiceDescription = service.ServiceDescription,
                                TotalPrice = service.TotalPrice - procedure.Price,
                                IsActive = true,
                                CreatedBy = _currentUserService.GetUserId(),
                                CreatedOn = DateTime.Now,
                                LastModifiedBy = _currentUserService.GetUserId(),
                                LastModifiedOn = DateTime.UtcNow
                            }).Entity;
                            int step = 1;
                            foreach (var a in sp)
                            {
                                if(a.ProcedureId != procedure.Id)
                                {
                                    await _db.ServiceProcedures.AddAsync(new ServiceProcedures
                                    {
                                        ServiceId = entry.Id,
                                        StepOrder = step++,
                                        ProcedureId = a.ProcedureId,
                                        CreatedBy = _currentUserService.GetUserId(),
                                        CreatedOn = DateTime.Now,
                                    });
                                }
                                else
                                {
                                    _db.ServiceProcedures.Remove(a);
                                }
                            }
                        }
                        else
                        {
                            service.TotalPrice -= procedure.Price;
                        }
                    }
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return _t["Sucessfully"];
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
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

        int count = await _db.Services.Where(p => p.DeletedBy != null)
            .CountAsync(cancellationToken);
        return new PaginationResponse<Procedure>(procedures, count, request.PageNumber, request.PageSize);
    }

    public async Task<string> RestoreProcedureAsync(DefaultIdType id, CancellationToken cancellationToken)
    {
        var procedure = await _db.Procedures.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id) ?? throw new BadRequestException("Procedure Not Found.");

        if(procedure == null)
        {
            throw new BadRequestException("Procedure Not Found!!!");
        }

        var check = await _db.Procedures.Where(p => p.Name == procedure.Name).FirstOrDefaultAsync();
        if (check != null)
        {
            throw new BadRequestException("Existing same procedure!!!");
        }

        procedure.DeletedBy = null;
        procedure.DeletedOn = null;
        procedure.LastModifiedBy = _currentUserService.GetUserId();
        procedure.LastModifiedOn = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        return _t["Restored"];
    }

    private async Task UpdateServiceProcedure(Guid serviceId, Guid oldProcedureId, Guid newProcedureId, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var service = await _db.Services
                .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken)
                ?? throw new NotFoundException($"Service with ID {serviceId} not found.");
            var oldProcedure = await _db.Procedures
                .FirstAsync(p => p.Id == oldProcedureId, cancellationToken);
            var newProcedure = await _db.Procedures
                .FirstAsync(p => p.Id == newProcedureId, cancellationToken);

            double priceDifference = newProcedure.Price - oldProcedure.Price;

            bool wasUsed = await _db.Appointments.AnyAsync(p => p.ServiceId == serviceId);

            if (!wasUsed)
            {
                service.TotalPrice += priceDifference;
                var serviceProcedure = await _db.ServiceProcedures
                    .FirstOrDefaultAsync(sp => sp.ServiceId == serviceId && sp.ProcedureId == oldProcedureId);

                serviceProcedure.ProcedureId = newProcedureId;
            }
            else
            {
                var s = new Service
                {
                    ServiceName = service.ServiceName,
                    ServiceDescription = service.ServiceDescription,
                    IsActive = true,
                    TotalPrice = service.TotalPrice + priceDifference,
                    CreatedBy = service.CreatedBy,
                    CreatedOn = service.CreatedOn,
                    LastModifiedBy = _currentUserService.GetUserId(),
                    LastModifiedOn = DateTime.UtcNow
                };
                var entry = _db.Services.Add(s).Entity;

                var serviceProcedure = await _db.ServiceProcedures
                    .Where(sp => sp.ServiceId == serviceId).ToListAsync();

                foreach (var item in serviceProcedure)
                {
                    _db.ServiceProcedures.Add(new ServiceProcedures
                    {
                        ServiceId = entry.Id,
                        ProcedureId = (item.ProcedureId == oldProcedureId) ? newProcedureId : item.ProcedureId,
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
            }
            await _db.SaveChangesAsync();
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<List<ProcedurePlanResponse>> GetProceduresByServiceID(Guid serviceID, CancellationToken cancellationToken)
    {
        try
        {
            if (!await CheckExistingService(serviceID))
            {
                throw new NotFoundException("Service Not Found");
            }

            var groupService = await _db.ServiceProcedures
                .Where(p => p.ServiceId == serviceID)
                .GroupBy(p => p.ServiceId)
                .Select(group => new
                {
                    Procedures = group.Select(p => p.ProcedureId).Distinct().ToList(),
                }).FirstOrDefaultAsync(cancellationToken);

            List<ProcedurePlanResponse> result = new List<ProcedurePlanResponse>();

            foreach (var item in groupService.Procedures)
            {
                var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == item);
                result.Add(new ProcedurePlanResponse
                {
                    DiscountAmount = 0.3,
                    Price = pro.Price,
                    ProcedureID = pro.Id,
                    ProcedureName = pro.Name,
                    PlanCost = pro.Price - (pro.Price * 0.3),
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<ServiceDTO> GetDeleteServiceByID(DefaultIdType id, CancellationToken cancellationToken)
    {
        var existing = await _db.Services.IgnoreQueryFilters().Where(p => p.Id == id).FirstOrDefaultAsync(cancellationToken) ?? throw new BadRequestException("Service is not found.");
        var type = await _db.TypeServices.FirstOrDefaultAsync(p => p.Id == existing.TypeServiceID);
        if (type == null)
        {
            throw new Exception("Warning: Error when identity type service.");
        }
        var user = await _userManager.FindByIdAsync(existing.CreatedBy.ToString());
        var result = new ServiceDTO();
        result.ServiceID = existing.Id;
        result.Name = existing.ServiceName;
        result.CreateDate = existing.CreatedOn;
        result.CreateBy = user.UserName;
        result.TotalPrice = existing.TotalPrice;
        result.Description = existing.ServiceDescription;
        result.TypeName = type.TypeName;
        result.TypeServiceID = type.Id;
        var service_procedure = await _db.ServiceProcedures.Where(p => p.ServiceId == id).ToListAsync(cancellationToken);
        if (service_procedure != null)
        {
            result.Procedures = new List<ProcedureDTO>();
            foreach (var item in service_procedure)
            {
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

    public async Task<ServiceHaveFeedback> GetServiceDetailHaveFeedback(Guid id, CancellationToken cancellationToken)
    {
        var result = new ServiceHaveFeedback();
        try
        {
            var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new NotFoundException("Service not found");
            var type = await _db.TypeServices.FirstOrDefaultAsync(p => p.Id == service.TypeServiceID);
            if (type == null)
            {
                throw new Exception("Warning: Error when identity type service.");
            }
            var totalRating = await _db.Feedbacks
                .Where(f => f.ServiceId == id)
                .GroupBy(f => f.ServiceId)
                .Select(group => new
                {
                    AverageRating = group.Average(f => f.Rating),
                    TotalFeedbacks = group.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            var feedbackByRating = await _db.Feedbacks
                .Where(f => f.ServiceId == id)
                .GroupBy(f => f.Rating)
                .Select(group => new
                {
                    Rating = group.Key,
                    TotalFeedbacks = group.Count(),
                    Feedbacks = group.Select(f => new
                    {
                        f.Id,
                        f.PatientProfileId,
                        f.DoctorProfileId,
                        f.Message,
                        f.Rating,
                        f.CreatedOn,
                        Appointment = _db.Appointments.FirstOrDefault(p => p.Id == f.AppointmentId)
                    }).ToList()
                })
                .OrderByDescending(x => x.Rating)
                .ToListAsync(cancellationToken);

            // Get procedures for the service
            var procedures = await _db.ServiceProcedures
                .Where(p => p.ServiceId == id)
                .GroupBy(p => p.ServiceId)
                .Select(p => new
                {
                    Procedure = p.Select(pro => pro.ProcedureId).Distinct().ToList(),
                })
                .FirstOrDefaultAsync(cancellationToken);

            var p = new List<ProcedureDTO>();

            foreach(var item in procedures.Procedure)
            {
                var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == item.Value);
                var createby = await _userManager.FindByIdAsync(pro.CreatedBy.ToString());
                p.Add(new ProcedureDTO
                {
                    ProcedureID = pro.Id,
                    Name = pro.Name,
                    Description = pro.Description,
                    Price = pro.Price,
                    CreateBy = $"{createby.FirstName} {createby.LastName}",
                    CreateDate = pro.CreatedOn,
                });
            }
            var createBy = await _userManager.FindByIdAsync(service.CreatedBy.ToString());
            result.ServiceDTO = new ServiceDTO
            {
                ServiceID = service.Id,
                Name = service.ServiceName,
                Description = service.ServiceDescription,
                CreateBy = $"{createBy.FirstName} {createBy.LastName}",
                CreateDate = service.CreatedOn,
                IsActive = service.IsActive,
                TotalPrice = service.TotalPrice,
                Procedures = p,
                TypeServiceID = type.Id,
                TypeName = type.TypeName,
            };

            // Initialize feedback list
            result.Feedbacks = new List<FeedbackServiceResponse>();

            // Process each rating group
            foreach (var ratingGroup in feedbackByRating)
            {
                var feedbackServiceResponse = new FeedbackServiceResponse
                {
                    RatingType = ratingGroup.Rating,
                    TotalFeedback = ratingGroup.TotalFeedbacks,
                    Feedbacks = new List<FeedbackServiceDetail>()
                };

                // Process each feedback in the rating group
                foreach (var feedback in ratingGroup.Feedbacks)
                {
                    var doctorProfile = await _db.DoctorProfiles
                        .FirstOrDefaultAsync(d => d.Id == feedback.DoctorProfileId, cancellationToken);
                    var doctorUser = doctorProfile != null ?
                        await _userManager.FindByIdAsync(doctorProfile.DoctorId) : null;

                    var patientProfile = await _db.PatientProfiles
                        .FirstOrDefaultAsync(p => p.Id == feedback.PatientProfileId, cancellationToken);
                    var patientUser = patientProfile != null ?
                        await _userManager.FindByIdAsync(patientProfile.UserId) : null;

                    var feedbackDetail = new FeedbackServiceDetail
                    {
                        FeedbackId = feedback.Id,
                        DoctorID = doctorProfile?.DoctorId,
                        DoctorName = doctorUser != null ? $"{doctorUser.FirstName} {doctorUser.LastName}" : null,
                        DoctorAvatar = doctorUser.ImageUrl,
                        PatientID = patientProfile?.UserId,
                        PatientName = patientUser != null ? $"{patientUser.FirstName} {patientUser.LastName}" : null,
                        PatientAvatar = patientUser.ImageUrl,
                        CreateDate = feedback.CreatedOn,
                        Ratings = feedback.Rating,
                        Message = feedback.Message,
                        CanFeedback = feedback.Appointment.canFeedback
                    };

                    feedbackServiceResponse.Feedbacks.Add(feedbackDetail);
                }

                result.Feedbacks.Add(feedbackServiceResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }

        return result;
    }

    public async Task<bool> CheckTypeServiceExisting(Guid type)
    {
        try
        {
            bool existing = await _db.TypeServices.AnyAsync(p => p.Id == type);
            return existing;
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public async Task<PaginationResponse<TypeService>> GetTypeServiceAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<TypeService>(request);
            var procedures = await _db.TypeServices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .WithSpecification(spec)
                .ToListAsync(cancellationToken);

            int count = await _db.TypeServices
                .CountAsync(cancellationToken);
            return new PaginationResponse<TypeService>(procedures, count, request.PageNumber, request.PageSize);
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public async Task<string> AddTypeServiceAsync(AddTypeServiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            bool existing = await _db.TypeServices.AnyAsync(p => p.TypeName == request.TypeName || p.TypeDescription == request.TypeDescription);
            if (existing)
            {
                throw new BadRequestException("Type Service has been existing");
            }
            _db.TypeServices.Add(new TypeService
            {
                TypeName = request.TypeName,
                TypeDescription = request.TypeDescription,
            });
            await _db.SaveChangesAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }
}
