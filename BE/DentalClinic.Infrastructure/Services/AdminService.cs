using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminService> _logger;
    private readonly IPasswordHasherService _passwordHasher;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public AdminService(
        DentalClinicDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<AdminService> logger,
        IPasswordHasherService? passwordHasher = null)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;
        _passwordHasher = passwordHasher ?? new PasswordHasherService();
    }

    // ==========================================
    // DEPARTMENTS
    // ==========================================

    public async Task<ApiResponse<List<AdminDepartmentDto>>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Include(d => d.ManagerUser)
            .Include(d => d.DepartmentServices)
            .Include(d => d.DentistDepartments)
            .OrderBy(d => d.DepartmentId)
            .AsSplitQuery()
            .ToListAsync(ct);

        var dtos = departments.Select(d => new AdminDepartmentDto
        {
            DepartmentId = d.DepartmentId,
            Name = d.Name,
            Description = d.Description,
            ManagerUserId = d.ManagerUserId,
            ManagerName = d.ManagerUser?.FullName,
            ManagerEmployeeCode = d.ManagerUser?.EmployeeCode,
            Status = d.Status,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            ServicesCount = d.DepartmentServices.Count,
            DentistsCount = d.DentistDepartments.Count(dd => dd.Status == "Active")
        }).ToList();

        return ApiResponse<List<AdminDepartmentDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AdminDepartmentDto>> GetDepartmentByIdAsync(long departmentId, CancellationToken ct = default)
    {
        var d = await _dbContext.Departments
            .AsNoTracking()
            .Include(d => d.ManagerUser)
            .Include(d => d.DepartmentServices)
            .Include(d => d.DentistDepartments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId, ct);

        if (d == null)
        {
            return ApiResponse<AdminDepartmentDto>.Fail("Không tìm thấy chuyên khoa.");
        }

        var dto = new AdminDepartmentDto
        {
            DepartmentId = d.DepartmentId,
            Name = d.Name,
            Description = d.Description,
            ManagerUserId = d.ManagerUserId,
            ManagerName = d.ManagerUser?.FullName,
            ManagerEmployeeCode = d.ManagerUser?.EmployeeCode,
            Status = d.Status,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            ServicesCount = d.DepartmentServices.Count,
            DentistsCount = d.DentistDepartments.Count(dd => dd.Status == "Active")
        };

        return ApiResponse<AdminDepartmentDto>.Ok(dto);
    }

    public async Task<ApiResponse<AdminDepartmentDto>> CreateDepartmentAsync(CreateDepartmentRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<AdminDepartmentDto>.Fail("Tên chuyên khoa không được để trống.");
        }

        var trimmedName = request.Name.Trim();
        var exists = await _dbContext.Departments
            .AnyAsync(d => d.Name.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
        {
            return ApiResponse<AdminDepartmentDto>.Fail($"Chuyên khoa '{trimmedName}' đã tồn tại trong hệ thống.");
        }

        if (request.ManagerUserId.HasValue)
        {
            var staffExists = await _dbContext.StaffProfiles
                .AnyAsync(s => s.UserId == request.ManagerUserId.Value, ct);

            if (!staffExists)
            {
                return ApiResponse<AdminDepartmentDto>.Fail("Nhân sự được chỉ định không tồn tại trong danh sách nhân sự.");
            }
        }

        var department = new Department
        {
            Name = trimmedName,
            Description = request.Description?.Trim(),
            ManagerUserId = request.ManagerUserId,
            Status = string.Equals(request.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Departments.Add(department);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CREATE",
            "Department",
            department.DepartmentId.ToString(),
            null,
            JsonSerializer.Serialize(new { department.Name, department.ManagerUserId, department.Status }),
            ct);

        return await GetDepartmentByIdAsync(department.DepartmentId, ct);
    }

    public async Task<ApiResponse<AdminDepartmentDto>> UpdateDepartmentAsync(long departmentId, UpdateDepartmentRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId, ct);

        if (department == null)
        {
            return ApiResponse<AdminDepartmentDto>.Fail("Không tìm thấy chuyên khoa cần cập nhật.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<AdminDepartmentDto>.Fail("Tên chuyên khoa không được để trống.");
        }

        var trimmedName = request.Name.Trim();
        var nameConflict = await _dbContext.Departments
            .AnyAsync(d => d.DepartmentId != departmentId && d.Name.ToLower() == trimmedName.ToLower(), ct);

        if (nameConflict)
        {
            return ApiResponse<AdminDepartmentDto>.Fail($"Tên chuyên khoa '{trimmedName}' đã được sử dụng bởi khoa khác.");
        }

        if (request.ManagerUserId.HasValue)
        {
            var staffExists = await _dbContext.StaffProfiles
                .AnyAsync(s => s.UserId == request.ManagerUserId.Value, ct);

            if (!staffExists)
            {
                return ApiResponse<AdminDepartmentDto>.Fail("Nhân sự được chỉ định không tồn tại.");
            }
        }

        var oldValues = JsonSerializer.Serialize(new { department.Name, department.ManagerUserId, department.Status });

        department.Name = trimmedName;
        department.Description = request.Description?.Trim();
        department.ManagerUserId = request.ManagerUserId;
        department.Status = string.Equals(request.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";
        department.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { department.Name, department.ManagerUserId, department.Status });

        await _auditLogService.LogAsync(
            adminUserId,
            "UPDATE",
            "Department",
            department.DepartmentId.ToString(),
            oldValues,
            newValues,
            ct);

        return await GetDepartmentByIdAsync(departmentId, ct);
    }

    public async Task<ApiResponse> ToggleDepartmentStatusAsync(long departmentId, long? adminUserId = null, CancellationToken ct = default)
    {
        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId, ct);

        if (department == null)
        {
            return ApiResponse.Fail("Không tìm thấy chuyên khoa.");
        }

        var oldStatus = department.Status;
        department.Status = department.Status == "Active" ? "Inactive" : "Active";
        department.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "TOGGLE_STATUS",
            "Department",
            department.DepartmentId.ToString(),
            JsonSerializer.Serialize(new { Status = oldStatus }),
            JsonSerializer.Serialize(new { department.Status }),
            ct);

        return ApiResponse.Ok($"Đã chuyển trạng thái chuyên khoa '{department.Name}' thành {department.Status}.");
    }

    // ==========================================
    // SERVICES
    // ==========================================

    public async Task<ApiResponse<List<AdminDepartmentServiceDto>>> GetServicesAsync(long? departmentId = null, string? search = null, CancellationToken ct = default)
    {
        var query = _dbContext.DepartmentServices
            .AsNoTracking()
            .Include(s => s.Department)
            .Include(s => s.Workflows)
                .ThenInclude(w => w.Steps)
            .AsSplitQuery()
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(s => s.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(s => s.ServiceName.ToLower().Contains(q) || (s.Description != null && s.Description.ToLower().Contains(q)));
        }

        var services = await query
            .OrderBy(s => s.DepartmentId)
            .ThenBy(s => s.DepartmentServiceId)
            .ToListAsync(ct);

        var dtos = services.Select(s =>
        {
            var activeWf = s.Workflows?.FirstOrDefault(w => w.IsActive);
            var latestWf = activeWf ?? s.Workflows?.OrderByDescending(w => w.Version).FirstOrDefault();
            var targetWf = activeWf ?? latestWf;
            var hasActive = activeWf != null && activeWf.Steps.Any();
            var approvalStatus = activeWf != null ? (activeWf.ApprovalStatus ?? "Approved") : (latestWf != null ? (latestWf.ApprovalStatus ?? "Draft") : "None");

            return new AdminDepartmentServiceDto
            {
                DepartmentServiceId = s.DepartmentServiceId,
                ServiceCode = s.ServiceCode ?? $"SRV-{s.DepartmentId}-{s.DepartmentServiceId:D3}",
                DepartmentId = s.DepartmentId,
                DepartmentName = s.Department?.Name ?? string.Empty,
                ServiceName = s.ServiceName,
                Description = s.Description,
                DurationMinutes = s.DurationMinutes,
                Price = s.Price,
                Status = s.Status,
                ServiceType = s.ServiceType ?? "Treatment",
                RequiresExclusiveRoom = s.RequiresExclusiveRoom,
                RequiresConsultationFirst = s.RequiresConsultationFirst,
                MinLeadTimeHours = s.MinLeadTimeHours,
                BookingInstructions = s.BookingInstructions,
                WorkflowStepCount = targetWf?.Steps?.Count ?? 0,
                HasActiveWorkflow = hasActive,
                WorkflowApprovalStatus = approvalStatus,
                ActiveWorkflow = targetWf != null ? new ServiceWorkflowDto
                {
                    WorkflowId = targetWf.WorkflowId,
                    DepartmentServiceId = targetWf.DepartmentServiceId,
                    WorkflowName = targetWf.WorkflowName,
                    Version = targetWf.Version,
                    IsActive = targetWf.IsActive,
                    ApprovalStatus = targetWf.ApprovalStatus ?? (targetWf.IsActive ? "Approved" : "Draft"),
                    ApprovedByUserId = targetWf.ApprovedByUserId,
                    ApprovedAt = targetWf.ApprovedAt,
                    Description = targetWf.Description,
                    Steps = targetWf.Steps.OrderBy(st => st.StepOrder).Select(st => new ServiceWorkflowStepDto
                    {
                        StepId = st.StepId,
                        WorkflowId = st.WorkflowId,
                        StepName = st.StepName,
                        Description = st.Description,
                        StepOrder = st.StepOrder,
                        IsRequired = st.IsRequired,
                        CompletionRequirement = st.CompletionRequirement,
                        ExpectedDataType = st.ExpectedDataType
                    }).ToList()
                } : null
            };
        }).ToList();

        return ApiResponse<List<AdminDepartmentServiceDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AdminDepartmentServiceDto>> GetServiceByIdAsync(long serviceId, CancellationToken ct = default)
    {
        var s = await _dbContext.DepartmentServices
            .AsNoTracking()
            .Include(s => s.Department)
            .Include(s => s.Workflows)
                .ThenInclude(w => w.Steps)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == serviceId, ct);

        if (s == null)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Không tìm thấy dịch vụ.");
        }

        var activeWf = s.Workflows?.FirstOrDefault(w => w.IsActive);
        var latestWf = activeWf ?? s.Workflows?.OrderByDescending(w => w.Version).FirstOrDefault();
        var targetWf = activeWf ?? latestWf;
        var hasActive = activeWf != null && activeWf.Steps.Any();
        var approvalStatus = activeWf != null ? (activeWf.ApprovalStatus ?? "Approved") : (latestWf != null ? (latestWf.ApprovalStatus ?? "Draft") : "None");

        var dto = new AdminDepartmentServiceDto
        {
            DepartmentServiceId = s.DepartmentServiceId,
            ServiceCode = s.ServiceCode ?? $"SRV-{s.DepartmentId}-{s.DepartmentServiceId:D3}",
            DepartmentId = s.DepartmentId,
            DepartmentName = s.Department?.Name ?? string.Empty,
            ServiceName = s.ServiceName,
            Description = s.Description,
            DurationMinutes = s.DurationMinutes,
            Price = s.Price,
            Status = s.Status,
            ServiceType = s.ServiceType ?? "Treatment",
            RequiresExclusiveRoom = s.RequiresExclusiveRoom,
            RequiresConsultationFirst = s.RequiresConsultationFirst,
            MinLeadTimeHours = s.MinLeadTimeHours,
            BookingInstructions = s.BookingInstructions,
            WorkflowStepCount = targetWf?.Steps?.Count ?? 0,
            HasActiveWorkflow = hasActive,
            WorkflowApprovalStatus = approvalStatus,
            ActiveWorkflow = targetWf != null ? new ServiceWorkflowDto
            {
                WorkflowId = targetWf.WorkflowId,
                DepartmentServiceId = targetWf.DepartmentServiceId,
                WorkflowName = targetWf.WorkflowName,
                Version = targetWf.Version,
                IsActive = targetWf.IsActive,
                ApprovalStatus = targetWf.ApprovalStatus ?? (targetWf.IsActive ? "Approved" : "Draft"),
                ApprovedByUserId = targetWf.ApprovedByUserId,
                ApprovedAt = targetWf.ApprovedAt,
                Description = targetWf.Description,
                Steps = targetWf.Steps.OrderBy(st => st.StepOrder).Select(st => new ServiceWorkflowStepDto
                {
                    StepId = st.StepId,
                    WorkflowId = st.WorkflowId,
                    StepName = st.StepName,
                    Description = st.Description,
                    StepOrder = st.StepOrder,
                    IsRequired = st.IsRequired,
                    CompletionRequirement = st.CompletionRequirement,
                    ExpectedDataType = st.ExpectedDataType
                }).ToList()
            } : null
        };

        return ApiResponse<AdminDepartmentServiceDto>.Ok(dto);
    }

    public async Task<ApiResponse<AdminDepartmentServiceDto>> CreateServiceAsync(CreateDepartmentServiceRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Tên dịch vụ không được để trống.");
        }

        if (request.Price < 0)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Giá dịch vụ không được nhỏ hơn 0.");
        }

        if (!request.DurationMinutes.HasValue || request.DurationMinutes.Value <= 0)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Thời lượng dự kiến phải lớn hơn 0 phút.");
        }

        var dept = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);

        if (dept == null || dept.Status != "Active")
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Khoa được chọn không tồn tại hoặc đã ngưng hoạt động.");
        }

        var trimmedName = request.ServiceName.Trim();
        var exists = await _dbContext.DepartmentServices
            .AnyAsync(s => s.DepartmentId == request.DepartmentId && s.ServiceName.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail($"Dịch vụ '{trimmedName}' đã tồn tại trong chuyên khoa này.");
        }

        // Validate ServiceCode
        string serviceCode;
        if (!string.IsNullOrWhiteSpace(request.ServiceCode))
        {
            serviceCode = request.ServiceCode.Trim().ToUpperInvariant();
            var codeExists = await _dbContext.DepartmentServices
                .AnyAsync(s => s.ServiceCode != null && s.ServiceCode.ToUpper() == serviceCode, ct);
            if (codeExists)
            {
                return ApiResponse<AdminDepartmentServiceDto>.Fail($"Mã dịch vụ '{serviceCode}' đã tồn tại trong hệ thống. Vui lòng chọn mã khác.");
            }
        }
        else
        {
            var prefix = dept.DepartmentId switch
            {
                1 => "GEN",
                2 => "ORTHO",
                3 => "SURG",
                4 => "PED",
                _ => $"D{dept.DepartmentId}"
            };
            var num = await _dbContext.DepartmentServices.CountAsync(s => s.DepartmentId == dept.DepartmentId, ct) + 1;
            do
            {
                serviceCode = $"SRV-{prefix}-{num:D3}";
                num++;
            } while (await _dbContext.DepartmentServices.AnyAsync(s => s.ServiceCode == serviceCode, ct));
        }

        // Validate Workflow Steps if provided
        if (request.WorkflowSteps != null && request.WorkflowSteps.Any())
        {
            if (request.WorkflowSteps.Any(st => string.IsNullOrWhiteSpace(st.StepName)))
            {
                return ApiResponse<AdminDepartmentServiceDto>.Fail("Tên các bước quy trình không được để trống.");
            }
            if (request.WorkflowSteps.Select(st => st.StepOrder).Distinct().Count() != request.WorkflowSteps.Count)
            {
                return ApiResponse<AdminDepartmentServiceDto>.Fail("Thứ tự các bước quy trình không được trùng nhau.");
            }
        }

        var validTypes = new[] { "Consultation", "Treatment", "Surgery", "Cosmetic", "Pediatric", "Implant", "Orthodontics", "General", "Endodontics", "Periodontics", "Examination" };
        var serviceType = validTypes.Contains(request.ServiceType, StringComparer.OrdinalIgnoreCase) ? request.ServiceType : "Treatment";

        var service = new DepartmentService
        {
            ServiceCode = serviceCode,
            DepartmentId = request.DepartmentId,
            ServiceName = trimmedName,
            Description = request.Description?.Trim(),
            DurationMinutes = request.DurationMinutes.Value,
            Price = request.Price,
            Status = string.Equals(request.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active",
            ServiceType = serviceType,
            RequiresExclusiveRoom = request.RequiresExclusiveRoom,
            RequiresConsultationFirst = request.RequiresConsultationFirst,
            MinLeadTimeHours = request.MinLeadTimeHours >= 0 ? request.MinLeadTimeHours : 2,
            BookingInstructions = request.BookingInstructions?.Trim()
        };

        _dbContext.DepartmentServices.Add(service);
        await _dbContext.SaveChangesAsync(ct);

        // Add initial workflow if steps provided
        if (request.WorkflowSteps != null && request.WorkflowSteps.Any())
        {
            var wf = new ServiceWorkflow
            {
                DepartmentServiceId = service.DepartmentServiceId,
                WorkflowName = $"Quy trình: {service.ServiceName}",
                Version = 1,
                IsActive = true,
                ApprovalStatus = "Approved",
                ApprovedByUserId = adminUserId,
                ApprovedAt = DateTime.UtcNow,
                Description = service.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = adminUserId
            };

            int order = 1;
            foreach (var st in request.WorkflowSteps.OrderBy(s => s.StepOrder))
            {
                wf.Steps.Add(new ServiceWorkflowStep
                {
                    StepName = st.StepName.Trim(),
                    Description = st.Description?.Trim(),
                    StepOrder = order++,
                    IsRequired = st.IsRequired,
                    CompletionRequirement = st.CompletionRequirement?.Trim(),
                    ExpectedDataType = string.IsNullOrWhiteSpace(st.ExpectedDataType) ? "None" : st.ExpectedDataType.Trim(),
                    CreatedAt = DateTime.UtcNow
                });
            }

            _dbContext.ServiceWorkflows.Add(wf);
            await _dbContext.SaveChangesAsync(ct);
        }

        await _auditLogService.LogAsync(
            adminUserId,
            "CREATE",
            "DepartmentService",
            service.DepartmentServiceId.ToString(),
            null,
            JsonSerializer.Serialize(new { service.ServiceCode, service.ServiceName, service.DepartmentId, service.Price, service.Status }),
            ct);

        return await GetServiceByIdAsync(service.DepartmentServiceId, ct);
    }

    public async Task<ApiResponse<AdminDepartmentServiceDto>> UpdateServiceAsync(long serviceId, UpdateDepartmentServiceRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        var service = await _dbContext.DepartmentServices
            .Include(s => s.Workflows)
                .ThenInclude(w => w.Steps)
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == serviceId, ct);

        if (service == null)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Không tìm thấy dịch vụ cần cập nhật.");
        }

        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Tên dịch vụ không được để trống.");
        }

        if (request.Price < 0)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Giá dịch vụ không được nhỏ hơn 0.");
        }

        if (!request.DurationMinutes.HasValue || request.DurationMinutes.Value <= 0)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Thời lượng dự kiến phải lớn hơn 0 phút.");
        }

        var dept = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);

        if (dept == null || dept.Status != "Active")
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Khoa được chọn không tồn tại hoặc đã ngưng hoạt động.");
        }

        var trimmedName = request.ServiceName.Trim();
        var conflict = await _dbContext.DepartmentServices
            .AnyAsync(s => s.DepartmentServiceId != serviceId && s.DepartmentId == request.DepartmentId && s.ServiceName.ToLower() == trimmedName.ToLower(), ct);

        if (conflict)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail($"Tên dịch vụ '{trimmedName}' đã được sử dụng trong chuyên khoa này.");
        }

        // Validate ServiceCode
        if (!string.IsNullOrWhiteSpace(request.ServiceCode))
        {
            var reqCode = request.ServiceCode.Trim().ToUpperInvariant();
            var codeConflict = await _dbContext.DepartmentServices
                .AnyAsync(s => s.DepartmentServiceId != serviceId && s.ServiceCode != null && s.ServiceCode.ToUpper() == reqCode, ct);
            if (codeConflict)
            {
                return ApiResponse<AdminDepartmentServiceDto>.Fail($"Mã dịch vụ '{reqCode}' đã được sử dụng bởi dịch vụ khác.");
            }
            service.ServiceCode = reqCode;
        }

        var oldValues = JsonSerializer.Serialize(new { service.ServiceCode, service.ServiceName, service.DepartmentId, service.Price, service.Status });

        var validTypes = new[] { "Consultation", "Treatment", "Surgery", "Cosmetic", "Pediatric", "Implant", "Orthodontics", "General", "Endodontics", "Periodontics", "Examination" };
        if (!string.IsNullOrWhiteSpace(request.ServiceType) && validTypes.Contains(request.ServiceType, StringComparer.OrdinalIgnoreCase))
        {
            service.ServiceType = request.ServiceType;
        }
        service.DepartmentId = request.DepartmentId;
        service.ServiceName = trimmedName;
        service.Description = request.Description?.Trim();
        service.DurationMinutes = request.DurationMinutes.Value;
        service.Price = request.Price;
        service.Status = string.Equals(request.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";
        service.RequiresExclusiveRoom = request.RequiresExclusiveRoom;
        service.RequiresConsultationFirst = request.RequiresConsultationFirst;
        service.MinLeadTimeHours = request.MinLeadTimeHours >= 0 ? request.MinLeadTimeHours : 2;
        service.BookingInstructions = request.BookingInstructions?.Trim();

        // Handle Workflow Steps with Versioning
        if (request.WorkflowSteps != null && request.WorkflowSteps.Any())
        {
            if (request.WorkflowSteps.Any(st => string.IsNullOrWhiteSpace(st.StepName)))
            {
                return ApiResponse<AdminDepartmentServiceDto>.Fail("Tên các bước quy trình không được để trống.");
            }
            if (request.WorkflowSteps.Select(st => st.StepOrder).Distinct().Count() != request.WorkflowSteps.Count)
            {
                return ApiResponse<AdminDepartmentServiceDto>.Fail("Thứ tự các bước quy trình không được trùng nhau.");
            }

            var activeWf = service.Workflows.FirstOrDefault(w => w.IsActive);
            int newVersion = service.Workflows.Any() ? service.Workflows.Max(w => w.Version) + 1 : 1;
            foreach (var active in service.Workflows.Where(w => w.IsActive))
            {
                active.IsActive = false;
                active.UpdatedAt = DateTime.UtcNow;
            }

            var newWf = new ServiceWorkflow
            {
                DepartmentServiceId = service.DepartmentServiceId,
                WorkflowName = $"Quy trình: {service.ServiceName} (v{newVersion})",
                Version = newVersion,
                IsActive = true,
                ApprovalStatus = "Approved",
                ApprovedByUserId = adminUserId,
                ApprovedAt = DateTime.UtcNow,
                Description = service.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = adminUserId
            };

            int order = 1;
            foreach (var st in request.WorkflowSteps.OrderBy(s => s.StepOrder))
            {
                newWf.Steps.Add(new ServiceWorkflowStep
                {
                    StepName = st.StepName.Trim(),
                    Description = st.Description?.Trim(),
                    StepOrder = order++,
                    IsRequired = st.IsRequired,
                    CompletionRequirement = st.CompletionRequirement?.Trim(),
                    ExpectedDataType = string.IsNullOrWhiteSpace(st.ExpectedDataType) ? "None" : st.ExpectedDataType.Trim(),
                    CreatedAt = DateTime.UtcNow
                });
            }

            _dbContext.ServiceWorkflows.Add(newWf);
        }

        await _dbContext.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { service.ServiceCode, service.ServiceName, service.DepartmentId, service.Price, service.Status });

        await _auditLogService.LogAsync(
            adminUserId,
            "UPDATE",
            "DepartmentService",
            service.DepartmentServiceId.ToString(),
            oldValues,
            newValues,
            ct);

        return await GetServiceByIdAsync(serviceId, ct);
    }

    public async Task<ApiResponse<ServiceWorkflowDto>> GetWorkflowAsync(long serviceId, int? version = null, CancellationToken ct = default)
    {
        var query = _dbContext.ServiceWorkflows
            .AsNoTracking()
            .Include(w => w.Steps)
            .Where(w => w.DepartmentServiceId == serviceId);

        if (version.HasValue)
        {
            query = query.Where(w => w.Version == version.Value);
        }
        else
        {
            query = query.OrderByDescending(w => w.IsActive).ThenByDescending(w => w.Version);
        }

        var wf = await query.FirstOrDefaultAsync(ct);
        if (wf == null)
        {
            return ApiResponse<ServiceWorkflowDto>.Fail("Không tìm thấy quy trình cho dịch vụ này.");
        }

        var dto = new ServiceWorkflowDto
        {
            WorkflowId = wf.WorkflowId,
            DepartmentServiceId = wf.DepartmentServiceId,
            WorkflowName = wf.WorkflowName,
            Version = wf.Version,
            IsActive = wf.IsActive,
            ApprovalStatus = wf.ApprovalStatus ?? (wf.IsActive ? "Approved" : "Draft"),
            ApprovedByUserId = wf.ApprovedByUserId,
            ApprovedAt = wf.ApprovedAt,
            Description = wf.Description,
            Steps = wf.Steps.OrderBy(s => s.StepOrder).Select(s => new ServiceWorkflowStepDto
            {
                StepId = s.StepId,
                WorkflowId = s.WorkflowId,
                StepName = s.StepName,
                Description = s.Description,
                StepOrder = s.StepOrder,
                IsRequired = s.IsRequired,
                CompletionRequirement = s.CompletionRequirement,
                ExpectedDataType = s.ExpectedDataType
            }).ToList()
        };

        return ApiResponse<ServiceWorkflowDto>.Ok(dto);
    }

    public async Task<ApiResponse<ServiceWorkflowDto>> ConfigureWorkflowAsync(long serviceId, ConfigureWorkflowRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        var service = await _dbContext.DepartmentServices
            .Include(s => s.Workflows)
                .ThenInclude(w => w.Steps)
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == serviceId, ct);

        if (service == null)
        {
            return ApiResponse<ServiceWorkflowDto>.Fail("Không tìm thấy dịch vụ cần cấu hình quy trình.");
        }

        if (request.Steps == null || !request.Steps.Any())
        {
            return ApiResponse<ServiceWorkflowDto>.Fail("Quy trình phải có ít nhất một bước thực hiện.");
        }

        if (request.Steps.Any(st => string.IsNullOrWhiteSpace(st.StepName)))
        {
            return ApiResponse<ServiceWorkflowDto>.Fail("Tên các bước quy trình không được để trống.");
        }

        if (request.Steps.Select(st => st.StepOrder).Distinct().Count() != request.Steps.Count)
        {
            return ApiResponse<ServiceWorkflowDto>.Fail("Thứ tự các bước quy trình không được trùng nhau.");
        }

        var activeWf = service.Workflows.FirstOrDefault(w => w.IsActive);
        int newVersion = service.Workflows.Any() ? service.Workflows.Max(w => w.Version) + 1 : 1;
        bool activate = request.ActivateImmediately;
        string approval = !string.IsNullOrWhiteSpace(request.ApprovalStatus) ? request.ApprovalStatus.Trim() : (activate ? "Approved" : "Draft");

        if (activate)
        {
            foreach (var active in service.Workflows.Where(w => w.IsActive))
            {
                active.IsActive = false;
                active.UpdatedAt = DateTime.UtcNow;
            }
        }

        var workflowName = !string.IsNullOrWhiteSpace(request.WorkflowName)
            ? request.WorkflowName.Trim()
            : $"Quy trình: {service.ServiceName} (v{newVersion})";

        var newWf = new ServiceWorkflow
        {
            DepartmentServiceId = service.DepartmentServiceId,
            WorkflowName = workflowName,
            Version = newVersion,
            IsActive = activate,
            ApprovalStatus = approval,
            ApprovedByUserId = approval == "Approved" ? adminUserId : null,
            ApprovedAt = approval == "Approved" ? DateTime.UtcNow : null,
            Description = request.Description?.Trim() ?? service.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUserId
        };

        int order = 1;
        foreach (var st in request.Steps.OrderBy(s => s.StepOrder))
        {
            newWf.Steps.Add(new ServiceWorkflowStep
            {
                StepName = st.StepName.Trim(),
                Description = st.Description?.Trim(),
                StepOrder = order++,
                IsRequired = st.IsRequired,
                CompletionRequirement = st.CompletionRequirement?.Trim(),
                ExpectedDataType = string.IsNullOrWhiteSpace(st.ExpectedDataType) ? "None" : st.ExpectedDataType.Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }

        _dbContext.ServiceWorkflows.Add(newWf);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CONFIGURE",
            "ServiceWorkflow",
            newWf.WorkflowId.ToString(),
            activeWf != null ? JsonSerializer.Serialize(new { activeWf.WorkflowId, activeWf.Version }) : null,
            JsonSerializer.Serialize(new { newWf.WorkflowId, newWf.Version, StepCount = newWf.Steps.Count }),
            ct);

        return await GetWorkflowAsync(serviceId, newVersion, ct);
    }

    public async Task<ApiResponse> ToggleServiceStatusAsync(long serviceId, long? adminUserId = null, CancellationToken ct = default)
    {
        var service = await _dbContext.DepartmentServices
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == serviceId, ct);

        if (service == null)
        {
            return ApiResponse.Fail("Không tìm thấy dịch vụ.");
        }

        var oldStatus = service.Status;
        service.Status = service.Status == "Active" ? "Inactive" : "Active";

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "TOGGLE_STATUS",
            "DepartmentService",
            service.DepartmentServiceId.ToString(),
            JsonSerializer.Serialize(new { Status = oldStatus }),
            JsonSerializer.Serialize(new { service.Status }),
            ct);

        return ApiResponse.Ok($"Đã chuyển trạng thái dịch vụ '{service.ServiceName}' thành {service.Status}.");
    }

    // ==========================================
    // ROOMS
    // ==========================================

    public async Task<ApiResponse<List<AdminRoomDto>>> GetRoomsAsync(CancellationToken ct = default)
    {
        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.DentalChairs)
            .OrderBy(r => r.RoomId)
            .ToListAsync(ct);

        var dtos = rooms.Select(r => new AdminRoomDto
        {
            RoomId = r.RoomId,
            RoomCode = r.RoomCode,
            RoomName = r.RoomName,
            DepartmentId = r.DepartmentId,
            DepartmentName = r.Department?.Name,
            Status = r.Status,
            IsExclusive = r.IsExclusive,
            ChairsCount = r.DentalChairs.Count,
            ActiveChairsCount = r.DentalChairs.Count(c => c.Status == "Active")
        }).ToList();

        return ApiResponse<List<AdminRoomDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AdminRoomDto>> GetRoomByIdAsync(long roomId, CancellationToken ct = default)
    {
        var r = await _dbContext.Rooms
            .AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.DentalChairs)
            .FirstOrDefaultAsync(r => r.RoomId == roomId, ct);

        if (r == null)
        {
            return ApiResponse<AdminRoomDto>.Fail("Không tìm thấy phòng khám.");
        }

        var dto = new AdminRoomDto
        {
            RoomId = r.RoomId,
            RoomCode = r.RoomCode,
            RoomName = r.RoomName,
            DepartmentId = r.DepartmentId,
            DepartmentName = r.Department?.Name,
            Status = r.Status,
            IsExclusive = r.IsExclusive,
            ChairsCount = r.DentalChairs.Count,
            ActiveChairsCount = r.DentalChairs.Count(c => c.Status == "Active")
        };

        return ApiResponse<AdminRoomDto>.Ok(dto);
    }

    public async Task<ApiResponse<AdminRoomDto>> CreateRoomAsync(CreateRoomRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RoomCode))
        {
            return ApiResponse<AdminRoomDto>.Fail("Mã phòng không được để trống.");
        }

        var trimmedCode = request.RoomCode.Trim().ToUpper();
        var exists = await _dbContext.Rooms
            .AnyAsync(r => r.RoomCode.ToUpper() == trimmedCode, ct);

        if (exists)
        {
            return ApiResponse<AdminRoomDto>.Fail($"Mã phòng '{trimmedCode}' đã tồn tại.");
        }

        var validStatuses = new[] { "Active", "Inactive", "Maintenance" };
        var status = validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase) ? request.Status : "Active";

        var room = new Room
        {
            RoomCode = trimmedCode,
            RoomName = string.IsNullOrWhiteSpace(request.RoomName) ? $"Phòng {trimmedCode}" : request.RoomName.Trim(),
            DepartmentId = request.DepartmentId,
            Status = status,
            IsExclusive = request.IsExclusive
        };

        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CREATE",
            "Room",
            room.RoomId.ToString(),
            null,
            JsonSerializer.Serialize(new { room.RoomCode, room.RoomName, room.DepartmentId, room.Status }),
            ct);

        return await GetRoomByIdAsync(room.RoomId, ct);
    }

    public async Task<ApiResponse<AdminRoomDto>> UpdateRoomAsync(long roomId, UpdateRoomRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        var room = await _dbContext.Rooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId, ct);

        if (room == null)
        {
            return ApiResponse<AdminRoomDto>.Fail("Không tìm thấy phòng khám.");
        }

        if (string.IsNullOrWhiteSpace(request.RoomCode))
        {
            return ApiResponse<AdminRoomDto>.Fail("Mã phòng không được để trống.");
        }

        var trimmedCode = request.RoomCode.Trim().ToUpper();
        var conflict = await _dbContext.Rooms
            .AnyAsync(r => r.RoomId != roomId && r.RoomCode.ToUpper() == trimmedCode, ct);

        if (conflict)
        {
            return ApiResponse<AdminRoomDto>.Fail($"Mã phòng '{trimmedCode}' đã được sử dụng.");
        }

        var validStatuses = new[] { "Active", "Inactive", "Maintenance" };
        var status = validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase) ? request.Status : room.Status;

        var oldValues = JsonSerializer.Serialize(new { room.RoomCode, room.RoomName, room.DepartmentId, room.Status });

        room.RoomCode = trimmedCode;
        room.RoomName = string.IsNullOrWhiteSpace(request.RoomName) ? room.RoomName : request.RoomName.Trim();
        if (request.DepartmentId.HasValue)
        {
            room.DepartmentId = request.DepartmentId.Value;
        }
        room.Status = status;
        room.IsExclusive = request.IsExclusive;

        await _dbContext.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { room.RoomCode, room.RoomName, room.DepartmentId, room.Status });

        await _auditLogService.LogAsync(
            adminUserId,
            "UPDATE",
            "Room",
            room.RoomId.ToString(),
            oldValues,
            newValues,
            ct);

        return await GetRoomByIdAsync(roomId, ct);
    }

    public async Task<ApiResponse> UpdateRoomStatusAsync(long roomId, string status, long? adminUserId = null, CancellationToken ct = default)
    {
        var room = await _dbContext.Rooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId, ct);

        if (room == null)
        {
            return ApiResponse.Fail("Không tìm thấy phòng khám.");
        }

        var validStatuses = new[] { "Active", "Inactive", "Maintenance" };
        var normalizedStatus = validStatuses.FirstOrDefault(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));

        if (normalizedStatus == null)
        {
            return ApiResponse.Fail("Trạng thái phòng không hợp lệ (Chỉ chấp nhận: Active, Inactive, Maintenance).");
        }

        var oldStatus = room.Status;
        room.Status = normalizedStatus;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CHANGE_STATUS",
            "Room",
            room.RoomId.ToString(),
            JsonSerializer.Serialize(new { Status = oldStatus }),
            JsonSerializer.Serialize(new { room.Status }),
            ct);

        return ApiResponse.Ok($"Đã cập nhật trạng thái phòng '{room.RoomCode}' thành {room.Status}.");
    }

    // ==========================================
    // DENTAL CHAIRS
    // ==========================================

    public async Task<ApiResponse<List<AdminDentalChairDto>>> GetChairsAsync(long? roomId = null, CancellationToken ct = default)
    {
        var query = _dbContext.DentalChairs
            .AsNoTracking()
            .Include(c => c.Room)
            .AsQueryable();

        if (roomId.HasValue && roomId.Value > 0)
        {
            query = query.Where(c => c.RoomId == roomId.Value);
        }

        var chairs = await query
            .OrderBy(c => c.RoomId)
            .ThenBy(c => c.DentalChairId)
            .ToListAsync(ct);

        var dtos = chairs.Select(c => new AdminDentalChairDto
        {
            DentalChairId = c.DentalChairId,
            RoomId = c.RoomId,
            RoomCode = c.Room?.RoomCode ?? string.Empty,
            RoomName = c.Room?.RoomName,
            ChairCode = c.ChairCode,
            Status = c.Status
        }).ToList();

        return ApiResponse<List<AdminDentalChairDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AdminDentalChairDto>> GetChairByIdAsync(long chairId, CancellationToken ct = default)
    {
        var c = await _dbContext.DentalChairs
            .AsNoTracking()
            .Include(c => c.Room)
            .FirstOrDefaultAsync(c => c.DentalChairId == chairId, ct);

        if (c == null)
        {
            return ApiResponse<AdminDentalChairDto>.Fail("Không tìm thấy ghế nha khoa.");
        }

        var dto = new AdminDentalChairDto
        {
            DentalChairId = c.DentalChairId,
            RoomId = c.RoomId,
            RoomCode = c.Room?.RoomCode ?? string.Empty,
            RoomName = c.Room?.RoomName,
            ChairCode = c.ChairCode,
            Status = c.Status
        };

        return ApiResponse<AdminDentalChairDto>.Ok(dto);
    }

    public async Task<ApiResponse<AdminDentalChairDto>> CreateChairAsync(CreateDentalChairRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ChairCode))
        {
            return ApiResponse<AdminDentalChairDto>.Fail("Mã ghế không được để trống.");
        }

        var roomExists = await _dbContext.Rooms
            .AnyAsync(r => r.RoomId == request.RoomId, ct);

        if (!roomExists)
        {
            return ApiResponse<AdminDentalChairDto>.Fail("Phòng khám không tồn tại.");
        }

        var trimmedCode = request.ChairCode.Trim().ToUpper();
        var existsInRoom = await _dbContext.DentalChairs
            .AnyAsync(c => c.RoomId == request.RoomId && c.ChairCode.ToUpper() == trimmedCode, ct);

        if (existsInRoom)
        {
            return ApiResponse<AdminDentalChairDto>.Fail($"Ghế mã '{trimmedCode}' đã tồn tại trong phòng khám này.");
        }

        var validStatuses = new[] { "Active", "Inactive", "Maintenance" };
        var status = validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase) ? request.Status : "Active";

        var chair = new DentalChair
        {
            RoomId = request.RoomId,
            ChairCode = trimmedCode,
            Status = status
        };

        _dbContext.DentalChairs.Add(chair);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CREATE",
            "DentalChair",
            chair.DentalChairId.ToString(),
            null,
            JsonSerializer.Serialize(new { chair.RoomId, chair.ChairCode, chair.Status }),
            ct);

        return await GetChairByIdAsync(chair.DentalChairId, ct);
    }

    public async Task<ApiResponse<AdminDentalChairDto>> UpdateChairAsync(long chairId, UpdateDentalChairRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        var chair = await _dbContext.DentalChairs
            .FirstOrDefaultAsync(c => c.DentalChairId == chairId, ct);

        if (chair == null)
        {
            return ApiResponse<AdminDentalChairDto>.Fail("Không tìm thấy ghế nha khoa cần cập nhật.");
        }

        if (string.IsNullOrWhiteSpace(request.ChairCode))
        {
            return ApiResponse<AdminDentalChairDto>.Fail("Mã ghế không được để trống.");
        }

        var roomExists = await _dbContext.Rooms
            .AnyAsync(r => r.RoomId == request.RoomId, ct);

        if (!roomExists)
        {
            return ApiResponse<AdminDentalChairDto>.Fail("Phòng khám không tồn tại.");
        }

        var trimmedCode = request.ChairCode.Trim().ToUpper();
        var conflict = await _dbContext.DentalChairs
            .AnyAsync(c => c.DentalChairId != chairId && c.RoomId == request.RoomId && c.ChairCode.ToUpper() == trimmedCode, ct);

        if (conflict)
        {
            return ApiResponse<AdminDentalChairDto>.Fail($"Ghế mã '{trimmedCode}' đã được sử dụng trong phòng khám này.");
        }

        var validStatuses = new[] { "Active", "Inactive", "Maintenance" };
        var status = validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase) ? request.Status : chair.Status;

        var oldValues = JsonSerializer.Serialize(new { chair.RoomId, chair.ChairCode, chair.Status });

        chair.RoomId = request.RoomId;
        chair.ChairCode = trimmedCode;
        chair.Status = status;

        await _dbContext.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { chair.RoomId, chair.ChairCode, chair.Status });

        await _auditLogService.LogAsync(
            adminUserId,
            "UPDATE",
            "DentalChair",
            chair.DentalChairId.ToString(),
            oldValues,
            newValues,
            ct);

        return await GetChairByIdAsync(chairId, ct);
    }

    public async Task<ApiResponse> UpdateChairStatusAsync(long chairId, string status, long? adminUserId = null, CancellationToken ct = default)
    {
        var chair = await _dbContext.DentalChairs
            .FirstOrDefaultAsync(c => c.DentalChairId == chairId, ct);

        if (chair == null)
        {
            return ApiResponse.Fail("Không tìm thấy ghế nha khoa.");
        }

        var validStatuses = new[] { "Active", "Inactive", "Maintenance" };
        var normalizedStatus = validStatuses.FirstOrDefault(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));

        if (normalizedStatus == null)
        {
            return ApiResponse.Fail("Trạng thái ghế không hợp lệ (Chỉ chấp nhận: Active, Inactive, Maintenance).");
        }

        var oldStatus = chair.Status;
        chair.Status = normalizedStatus;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CHANGE_STATUS",
            "DentalChair",
            chair.DentalChairId.ToString(),
            JsonSerializer.Serialize(new { Status = oldStatus }),
            JsonSerializer.Serialize(new { chair.Status }),
            ct);

        return ApiResponse.Ok($"Đã cập nhật trạng thái ghế '{chair.ChairCode}' thành {chair.Status}.");
    }

    // ==========================================
    // AUDIT LOGS
    // ==========================================

    public async Task<ApiResponse<PagedResult<AuditLogDto>>> GetAuditLogsAsync(AuditLogQueryRequest request, CancellationToken ct = default)
    {
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .ThenInclude(u => u!.StaffProfile)
            .Include(a => a.User)
            .ThenInclude(u => u!.PatientProfile)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(a => a.Action.ToUpper() == request.Action.Trim().ToUpper());
        }

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            query = query.Where(a => a.EntityName.ToLower() == request.EntityName.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(s) ||
                a.EntityName.ToLower().Contains(s) ||
                (a.EntityId != null && a.EntityId.ToLower().Contains(s)) ||
                (a.User != null && a.User.Email != null && a.User.Email.ToLower().Contains(s)));
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= request.ToDate.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 && request.PageSize <= 100 ? request.PageSize : 20;

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = logs.Select(a => new AuditLogDto
        {
            AuditLogId = a.AuditLogId,
            UserId = a.UserId,
            UserEmail = a.User?.Email,
            UserFullName = a.User?.StaffProfile?.FullName ?? a.User?.PatientProfile?.FullName ?? a.User?.Email,
            UserRole = a.User?.Role,
            Action = a.Action,
            EntityName = a.EntityName,
            EntityId = a.EntityId,
            OldValues = a.OldValues,
            NewValues = a.NewValues,
            CreatedAt = a.CreatedAt
        }).ToList();

        var result = new PagedResult<AuditLogDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return ApiResponse<PagedResult<AuditLogDto>>.Ok(result);
    }

    // ==========================================
    // DASHBOARD & REPORTS
    // ==========================================

    public async Task<ApiResponse<AdminDashboardStatsDto>> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var totalPatients = await _dbContext.PatientProfiles.CountAsync(ct);
        var totalStaff = await _dbContext.StaffProfiles.CountAsync(ct);
        var totalDentists = await _dbContext.DentistProfiles.CountAsync(ct);
        var totalDepartments = await _dbContext.Departments.CountAsync(ct);
        var totalRooms = await _dbContext.Rooms.CountAsync(ct);
        var totalChairs = await _dbContext.DentalChairs.CountAsync(ct);
        var activeChairs = await _dbContext.DentalChairs.CountAsync(c => c.Status == "Active", ct);
        var maintenanceChairs = await _dbContext.DentalChairs.CountAsync(c => c.Status == "Maintenance", ct);

        // Appointments counts
        var apptStats = await _dbContext.Appointments
            .AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalAppointments = apptStats.Sum(x => x.Count);
        var completedAppts = apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.Completed)?.Count ?? 0;
        var confirmedAppts = apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.Confirmed)?.Count ?? 0;
        var cancelledAppts = (apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.Cancelled)?.Count ?? 0)
            + (apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.Rejected)?.Count ?? 0);
        var pendingAppts = (apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.PendingReceptionReview)?.Count ?? 0)
            + (apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.PendingDepartmentReview)?.Count ?? 0);

        var completionRate = totalAppointments > 0 ? Math.Round((double)completedAppts / totalAppointments * 100, 1) : 0;
        var cancellationRate = totalAppointments > 0 ? Math.Round((double)cancelledAppts / totalAppointments * 100, 1) : 0;

        // Revenue calculation:
        // 1. From Invoices where Status == 'Paid'
        var paidInvoiceRevenue = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.Status == "Paid")
            .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0;

        decimal totalRevenue = paidInvoiceRevenue;
        if (totalRevenue <= 0)
        {
            var performedServiceRevenue = await _dbContext.PerformedServices
                .AsNoTracking()
                .Where(p => p.TreatmentSession.Appointment.Status == AppointmentStatus.Completed)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

            totalRevenue = performedServiceRevenue > 0
                ? performedServiceRevenue
                : (await _dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => a.Status == AppointmentStatus.Completed && a.RequestedService != null)
                    .SumAsync(a => (decimal?)a.RequestedService!.Price, ct) ?? 0);
        }

        // Monthly Revenues for the current year (1 single batch query instead of 12 full table scans)
        var currentYear = DateTime.UtcNow.Year;
        var startOfYear = new DateTime(currentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfYear = startOfYear.AddYears(1);

        var yearAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledStart >= startOfYear && a.ScheduledStart < endOfYear)
            .Select(a => new
            {
                Month = a.ScheduledStart!.Value.Month,
                IsCompleted = a.Status == AppointmentStatus.Completed,
                Price = a.RequestedService != null ? a.RequestedService.Price : 0
            })
            .ToListAsync(ct);

        var monthlyList = new List<MonthlyRevenueItemDto>();
        for (int m = 1; m <= 12; m++)
        {
            var inMonth = yearAppts.Where(a => a.Month == m).ToList();
            monthlyList.Add(new MonthlyRevenueItemDto
            {
                Year = currentYear,
                Month = m,
                MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                Revenue = inMonth.Where(a => a.IsCompleted).Sum(a => a.Price),
                AppointmentCount = inMonth.Count
            });
        }
        // Department breakdown
        var deptStatsQuery = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Department)
            .Include(a => a.RequestedService)
            .Where(a => a.DepartmentId != null)
            .GroupBy(a => new { a.DepartmentId, DepartmentName = a.Department != null ? a.Department.Name : "Khác" })
            .Select(g => new
            {
                DepartmentId = g.Key.DepartmentId!.Value,
                g.Key.DepartmentName,
                Count = g.Count(),
                Revenue = g.Where(a => a.Status == AppointmentStatus.Completed && a.RequestedService != null)
                           .Sum(a => a.RequestedService!.Price)
            })
            .ToListAsync(ct);

        var deptList = deptStatsQuery.Select(d => new DepartmentStatItemDto
        {
            DepartmentId = d.DepartmentId,
            DepartmentName = d.DepartmentName,
            AppointmentCount = d.Count,
            Revenue = d.Revenue,
            Percentage = totalAppointments > 0 ? Math.Round((double)d.Count / totalAppointments * 100, 1) : 0
        }).OrderByDescending(d => d.AppointmentCount).ToList();

        // Top Services
        var topServicesQuery = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.RequestedService)
            .ThenInclude(s => s!.Department)
            .Where(a => a.RequestedServiceId != null && a.RequestedService != null)
            .GroupBy(a => new
            {
                ServiceId = a.RequestedServiceId!.Value,
                ServiceName = a.RequestedService!.ServiceName,
                DepartmentName = a.RequestedService.Department != null ? a.RequestedService.Department.Name : string.Empty,
                Price = a.RequestedService.Price
            })
            .Select(g => new TopServiceStatItemDto
            {
                DepartmentServiceId = g.Key.ServiceId,
                ServiceName = g.Key.ServiceName,
                DepartmentName = g.Key.DepartmentName,
                Price = g.Key.Price,
                BookingCount = g.Count(),
                TotalRevenue = g.Count() * g.Key.Price
            })
            .OrderByDescending(s => s.BookingCount)
            .Take(5)
            .ToListAsync(ct);

        // Recent Audit Logs
        var recentAuditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new AuditLogDto
            {
                AuditLogId = a.AuditLogId,
                UserId = a.UserId,
                UserEmail = a.User != null ? a.User.Email : null,
                UserRole = a.User != null ? a.User.Role : null,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        var stats = new AdminDashboardStatsDto
        {
            TotalRevenue = totalRevenue,
            TotalAppointments = totalAppointments,
            CompletedAppointments = completedAppts,
            ConfirmedAppointments = confirmedAppts,
            CancelledAppointments = cancelledAppts,
            PendingAppointments = pendingAppts,
            TotalPatients = totalPatients,
            TotalStaff = totalStaff,
            TotalDentists = totalDentists,
            TotalDepartments = totalDepartments,
            TotalRooms = totalRooms,
            TotalChairs = totalChairs,
            ActiveChairs = activeChairs,
            MaintenanceChairs = maintenanceChairs,
            CompletionRate = completionRate,
            CancellationRate = cancellationRate,
            MonthlyRevenues = monthlyList,
            DepartmentStats = deptList,
            TopServices = topServicesQuery,
            RecentAuditLogs = recentAuditLogs
        };

        return ApiResponse<AdminDashboardStatsDto>.Ok(stats);
    }

    public async Task<ApiResponse<RevenueReportDto>> GetRevenueReportAsync(int? year = null, CancellationToken ct = default)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;

        var monthlyList = new List<MonthlyRevenueItemDto>();
        decimal totalYearRevenue = 0;
        int totalYearAppts = 0;

        for (int m = 1; m <= 12; m++)
        {
            var apptsInMonth = await _dbContext.Appointments
                .AsNoTracking()
                .Include(a => a.RequestedService)
                .Where(a => a.ScheduledStart != null &&
                            a.ScheduledStart.Value.Year == targetYear &&
                            a.ScheduledStart.Value.Month == m)
                .ToListAsync(ct);

            var count = apptsInMonth.Count;
            var rev = apptsInMonth
                .Where(a => a.Status == AppointmentStatus.Completed && a.RequestedService != null)
                .Sum(a => a.RequestedService?.Price ?? 0);

            totalYearRevenue += rev;
            totalYearAppts += count;

            monthlyList.Add(new MonthlyRevenueItemDto
            {
                Year = targetYear,
                Month = m,
                MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                Revenue = rev,
                AppointmentCount = count
            });
        }

        var deptBreakdownQuery = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Department)
            .Include(a => a.RequestedService)
            .Where(a => a.ScheduledStart != null &&
                        a.ScheduledStart.Value.Year == targetYear &&
                        a.DepartmentId != null)
            .GroupBy(a => new { a.DepartmentId, DepartmentName = a.Department != null ? a.Department.Name : "Khác" })
            .Select(g => new
            {
                DepartmentId = g.Key.DepartmentId!.Value,
                g.Key.DepartmentName,
                Count = g.Count(),
                Revenue = g.Where(a => a.Status == AppointmentStatus.Completed && a.RequestedService != null)
                           .Sum(a => a.RequestedService!.Price)
            })
            .ToListAsync(ct);

        var deptBreakdown = deptBreakdownQuery.Select(d => new DepartmentStatItemDto
        {
            DepartmentId = d.DepartmentId,
            DepartmentName = d.DepartmentName,
            AppointmentCount = d.Count,
            Revenue = d.Revenue,
            Percentage = totalYearRevenue > 0 ? Math.Round((double)(d.Revenue / totalYearRevenue) * 100, 1) : 0
        }).OrderByDescending(d => d.Revenue).ToList();

        var report = new RevenueReportDto
        {
            Year = targetYear,
            TotalYearRevenue = totalYearRevenue,
            TotalYearAppointments = totalYearAppts,
            MonthlyBreakdown = monthlyList,
            DepartmentBreakdown = deptBreakdown
        };

        return ApiResponse<RevenueReportDto>.Ok(report);
    }

    public async Task<ApiResponse<AppointmentReportDto>> GetAppointmentReportAsync(CancellationToken ct = default)
    {
        var apptStats = await _dbContext.Appointments
            .AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalAppointments = apptStats.Sum(x => x.Count);
        var completedCount = apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.Completed)?.Count ?? 0;
        var cancelledCount = (apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.Cancelled)?.Count ?? 0)
            + (apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.Rejected)?.Count ?? 0);
        var noShowCount = apptStats.FirstOrDefault(x => x.Status == AppointmentStatus.NoShow)?.Count ?? 0;
        var completionRate = totalAppointments > 0 ? Math.Round((double)completedCount / totalAppointments * 100, 1) : 0;

        var deptBreakdownQuery = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Department)
            .Where(a => a.DepartmentId != null)
            .GroupBy(a => new { a.DepartmentId, DepartmentName = a.Department != null ? a.Department.Name : "Khác" })
            .Select(g => new
            {
                DepartmentId = g.Key.DepartmentId!.Value,
                g.Key.DepartmentName,
                Count = g.Count()
            })
            .ToListAsync(ct);

        var deptBreakdown = deptBreakdownQuery.Select(d => new DepartmentStatItemDto
        {
            DepartmentId = d.DepartmentId,
            DepartmentName = d.DepartmentName,
            AppointmentCount = d.Count,
            Percentage = totalAppointments > 0 ? Math.Round((double)d.Count / totalAppointments * 100, 1) : 0
        }).OrderByDescending(d => d.AppointmentCount).ToList();

        var topServices = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.RequestedService)
            .ThenInclude(s => s!.Department)
            .Where(a => a.RequestedServiceId != null && a.RequestedService != null)
            .GroupBy(a => new
            {
                ServiceId = a.RequestedServiceId!.Value,
                ServiceName = a.RequestedService!.ServiceName,
                DepartmentName = a.RequestedService.Department != null ? a.RequestedService.Department.Name : string.Empty,
                Price = a.RequestedService.Price
            })
            .Select(g => new TopServiceStatItemDto
            {
                DepartmentServiceId = g.Key.ServiceId,
                ServiceName = g.Key.ServiceName,
                DepartmentName = g.Key.DepartmentName,
                Price = g.Key.Price,
                BookingCount = g.Count(),
                TotalRevenue = g.Count() * g.Key.Price
            })
            .OrderByDescending(s => s.BookingCount)
            .Take(10)
            .ToListAsync(ct);

        var report = new AppointmentReportDto
        {
            TotalAppointments = totalAppointments,
            CompletedCount = completedCount,
            CancelledCount = cancelledCount,
            NoShowCount = noShowCount,
            CompletionRate = completionRate,
            DepartmentBreakdown = deptBreakdown,
            TopServices = topServices
        };

        return ApiResponse<AppointmentReportDto>.Ok(report);
    }

    // ==========================================
    // USER MANAGEMENT
    // ==========================================

    public async Task<ApiResponse<List<AdminUserDto>>> GetUsersAsync(string? role = null, string? search = null, CancellationToken ct = default)
    {
        var query = _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
                .ThenInclude(sp => sp!.DentistProfile)
            .Include(u => u.PatientProfile)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(q)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(q)) ||
                (u.StaffProfile != null && u.StaffProfile.FullName.ToLower().Contains(q)) ||
                (u.StaffProfile != null && u.StaffProfile.EmployeeCode.ToLower().Contains(q)) ||
                (u.PatientProfile != null && u.PatientProfile.FullName.ToLower().Contains(q)) ||
                (u.PatientProfile != null && u.PatientProfile.PatientCode.ToLower().Contains(q)));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        var dtos = users.Select(u => new AdminUserDto
        {
            UserId = u.UserId,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            FullName = u.StaffProfile?.FullName ?? u.PatientProfile?.FullName ?? (u.Email ?? "Chưa đặt tên"),
            Code = u.StaffProfile?.EmployeeCode ?? u.PatientProfile?.PatientCode,
            Role = u.Role,
            Status = u.Status,
            Specialty = u.StaffProfile?.DentistProfile?.Qualification,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        }).ToList();

        return ApiResponse<List<AdminUserDto>>.Ok(dtos);
    }

    public async Task<ApiResponse> ToggleUserLockAsync(long userId, string? reason = null, long? adminUserId = null, CancellationToken ct = default)
    {
        if (adminUserId.HasValue && adminUserId.Value == userId)
        {
            return ApiResponse.Fail("Bạn không thể tự khóa tài khoản của chính mình.");
        }

        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user == null)
        {
            return ApiResponse.Fail("Không tìm thấy tài khoản người dùng.");
        }

        var oldStatus = user.Status;
        var newStatus = user.Status == AccountStatus.Locked ? AccountStatus.Active : AccountStatus.Locked;
        user.Status = newStatus;
        user.UpdatedAt = DateTime.UtcNow;

        if (newStatus == AccountStatus.Locked)
        {
            user.LockoutEnd = DateTime.UtcNow.AddYears(100);
        }
        else
        {
            user.LockoutEnd = null;
            user.FailedLoginCount = 0;
        }

        await _dbContext.SaveChangesAsync(ct);

        var actionName = newStatus == AccountStatus.Locked ? "LOCK_USER" : "UNLOCK_USER";
        await _auditLogService.LogAsync(
            adminUserId,
            actionName,
            "UserAccount",
            user.UserId.ToString(),
            JsonSerializer.Serialize(new { Status = oldStatus }),
            JsonSerializer.Serialize(new { Status = newStatus, Reason = reason }),
            ct);

        var msg = newStatus == AccountStatus.Locked
            ? $"Đã khóa tài khoản {user.Email ?? user.UserId.ToString()} thành công."
            : $"Đã mở khóa tài khoản {user.Email ?? user.UserId.ToString()} thành công.";

        return ApiResponse.Ok(msg);
    }

    // ==========================================
    // DENTIST DEPARTMENT ALLOCATIONS
    // ==========================================
    public async Task<ApiResponse<List<DentistDepartmentAllocationDto>>> GetDentistAllocationsAsync(long? departmentId = null, CancellationToken ct = default)
    {
        var query = _dbContext.DentistDepartments
            .AsNoTracking()
            .Include(dd => dd.DentistUser)
                .ThenInclude(dp => dp.User)
                    .ThenInclude(sp => sp.User)
            .Include(dd => dd.Department)
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(dd => dd.DepartmentId == departmentId.Value);
        }

        var list = await query
            .OrderBy(dd => dd.Department.Name)
            .ThenBy(dd => dd.DentistUser.User.FullName)
            .ToListAsync(ct);

        var dtos = list.Select(dd => new DentistDepartmentAllocationDto
        {
            DentistDepartmentId = dd.DentistDepartmentId,
            DentistUserId = dd.DentistUserId,
            DentistName = dd.DentistUser?.User?.FullName ?? $"BS #{dd.DentistUserId}",
            DentistCode = dd.DentistUser?.User?.EmployeeCode,
            DentistEmail = dd.DentistUser?.User?.User?.Email,
            DentistPhone = dd.DentistUser?.User?.User?.PhoneNumber,
            Specialty = dd.DentistUser?.Qualification,
            DepartmentId = dd.DepartmentId,
            DepartmentName = dd.Department?.Name ?? $"Khoa #{dd.DepartmentId}",
            Status = dd.Status,
            AssignedAt = dd.AssignedAt
        }).ToList();

        return ApiResponse<List<DentistDepartmentAllocationDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<DentistDepartmentAllocationDto>> AssignDentistToDepartmentAsync(AssignDentistDepartmentRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        if (request.DentistUserId <= 0)
        {
            return ApiResponse<DentistDepartmentAllocationDto>.Fail("Vui lòng chọn bác sĩ hợp lệ.");
        }

        if (request.DepartmentId <= 0)
        {
            return ApiResponse<DentistDepartmentAllocationDto>.Fail("Vui lòng chọn chuyên khoa hợp lệ.");
        }

        var dept = await _dbContext.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);
        if (dept == null)
        {
            return ApiResponse<DentistDepartmentAllocationDto>.Fail("Không tìm thấy chuyên khoa yêu cầu.");
        }

        var dentist = await _dbContext.DentistProfiles
            .Include(dp => dp.User)
                .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(dp => dp.UserId == request.DentistUserId, ct);

        if (dentist == null)
        {
            return ApiResponse<DentistDepartmentAllocationDto>.Fail("Không tìm thấy hồ sơ bác sĩ.");
        }

        var existing = await _dbContext.DentistDepartments
            .FirstOrDefaultAsync(dd => dd.DentistUserId == request.DentistUserId && dd.DepartmentId == request.DepartmentId, ct);

        DentistDepartment record;
        if (existing != null)
        {
            existing.Status = "Active";
            existing.AssignedAt = DateTime.UtcNow;
            record = existing;
        }
        else
        {
            record = new DentistDepartment
            {
                DentistUserId = request.DentistUserId,
                DepartmentId = request.DepartmentId,
                Status = "Active",
                AssignedAt = DateTime.UtcNow
            };
            _dbContext.DentistDepartments.Add(record);
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "ASSIGN_DENTIST_DEPT",
            "DentistDepartment",
            record.DentistDepartmentId.ToString(),
            null,
            JsonSerializer.Serialize(new { record.DentistUserId, record.DepartmentId, record.Status }),
            ct);

        var dto = new DentistDepartmentAllocationDto
        {
            DentistDepartmentId = record.DentistDepartmentId,
            DentistUserId = record.DentistUserId,
            DentistName = dentist.User?.FullName ?? $"BS #{record.DentistUserId}",
            DentistCode = dentist.User?.EmployeeCode,
            DentistEmail = dentist.User?.User?.Email,
            DentistPhone = dentist.User?.User?.PhoneNumber,
            Specialty = dentist.Qualification,
            DepartmentId = record.DepartmentId,
            DepartmentName = dept.Name,
            Status = record.Status,
            AssignedAt = record.AssignedAt
        };

        return ApiResponse<DentistDepartmentAllocationDto>.Ok(dto, "Phân bổ bác sĩ vào chuyên khoa thành công.");
    }

    public async Task<ApiResponse> ToggleDentistAllocationStatusAsync(long dentistDepartmentId, long? adminUserId = null, CancellationToken ct = default)
    {
        var record = await _dbContext.DentistDepartments
            .Include(dd => dd.DentistUser)
                .ThenInclude(dp => dp.User)
            .Include(dd => dd.Department)
            .FirstOrDefaultAsync(dd => dd.DentistDepartmentId == dentistDepartmentId, ct);

        if (record == null)
        {
            return ApiResponse.Fail("Không tìm thấy bản ghi phân bổ bác sĩ.");
        }

        var oldStatus = record.Status;
        record.Status = record.Status == "Active" ? "Inactive" : "Active";
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "TOGGLE_DENTIST_DEPT_STATUS",
            "DentistDepartment",
            record.DentistDepartmentId.ToString(),
            JsonSerializer.Serialize(new { Status = oldStatus }),
            JsonSerializer.Serialize(new { Status = record.Status }),
            ct);

        return ApiResponse.Ok($"Đã cập nhật trạng thái phân bổ bác sĩ thành '{record.Status}'.");
    }

    // ==========================================
    // USER MANAGEMENT EXTENSIONS
    // ==========================================

    public async Task<ApiResponse<AdminUserDto>> CreateStaffAccountAsync(CreateStaffRequest request, long adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return ApiResponse<AdminUserDto>.Fail("Email không được để trống.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return ApiResponse<AdminUserDto>.Fail("Mật khẩu phải có tối thiểu 6 ký tự.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            return ApiResponse<AdminUserDto>.Fail("Họ và tên không được để trống.");

        var normalizedRole = request.Role?.Trim();
        var allowedRoles = new[] { UserRole.Dentist, UserRole.Receptionist, UserRole.SystemAdministrator };
        if (!allowedRoles.Contains(normalizedRole))
        {
            return ApiResponse<AdminUserDto>.Fail($"Vai trò '{normalizedRole}' không hợp lệ. Chỉ chấp nhận Dentist, Receptionist, SystemAdministrator.");
        }

        var trimmedEmail = request.Email.Trim().ToLower();
        var emailExists = await _dbContext.UserAccounts.AnyAsync(u => u.Email != null && u.Email.ToLower() == trimmedEmail, ct);
        if (emailExists)
        {
            return ApiResponse<AdminUserDto>.Fail($"Email '{trimmedEmail}' đã được đăng ký trong hệ thống.");
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var trimmedPhone = request.PhoneNumber.Trim();
            var phoneExists = await _dbContext.UserAccounts.AnyAsync(u => u.PhoneNumber == trimmedPhone, ct);
            if (phoneExists)
            {
                return ApiResponse<AdminUserDto>.Fail($"Số điện thoại '{trimmedPhone}' đã được sử dụng.");
            }
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new UserAccount
        {
            Email = trimmedEmail,
            PasswordHash = passwordHash,
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = normalizedRole!,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow,
            EmailVerifiedAt = DateTime.UtcNow
        };

        _dbContext.UserAccounts.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        var prefix = normalizedRole switch
        {
            UserRole.Dentist => "DNT",
            UserRole.Receptionist => "REC",
            UserRole.SystemAdministrator => "ADM",
            _ => "STF"
        };
        var empCode = string.IsNullOrWhiteSpace(request.StaffCode) ? $"{prefix}-{user.UserId:D4}" : request.StaffCode.Trim().ToUpper();

        var staffProfile = new StaffProfile
        {
            UserId = user.UserId,
            FullName = request.FullName.Trim(),
            EmployeeCode = empCode,
            EmploymentStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.StaffProfiles.Add(staffProfile);

        if (normalizedRole == UserRole.Dentist)
        {
            var qualification = !string.IsNullOrWhiteSpace(request.Education) 
                ? request.Education.Trim() 
                : (!string.IsNullOrWhiteSpace(request.Specialty) ? request.Specialty.Trim() : "Bác sĩ Chuyên khoa Răng Hàm Mặt");
            var licenseNum = !string.IsNullOrWhiteSpace(request.StaffCode) 
                ? request.StaffCode.Trim() 
                : $"LIC-{user.UserId:D4}";

            var dentistProfile = new DentistProfile
            {
                UserId = user.UserId,
                YearsOfExperience = request.ExperienceYears ?? 3,
                Qualification = qualification,
                LicenseNumber = licenseNum,
                Biography = !string.IsNullOrWhiteSpace(request.Specialty)
                    ? $"Bác sĩ chuyên khoa {request.Specialty.Trim()} tại phòng khám DentalCare."
                    : "Bác sĩ chuyên khoa tại phòng khám DentalCare."
            };
            _dbContext.DentistProfiles.Add(dentistProfile);

            if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
            {
                var dept = await _dbContext.Departments.FindAsync(new object[] { request.DepartmentId.Value }, ct);
                if (dept != null)
                {
                    var allocation = new DentistDepartment
                    {
                        DentistUserId = user.UserId,
                        DepartmentId = dept.DepartmentId,
                        Status = "Active",
                        AssignedAt = DateTime.UtcNow
                    };
                    _dbContext.DentistDepartments.Add(allocation);
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CREATE_STAFF_ACCOUNT",
            "UserAccount",
            user.UserId.ToString(),
            null,
            JsonSerializer.Serialize(new { user.Email, user.Role, staffProfile.FullName, staffProfile.EmployeeCode }),
            ct);

        var resultDto = new AdminUserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FullName = staffProfile.FullName,
            Code = staffProfile.EmployeeCode,
            Role = user.Role,
            Status = user.Status,
            Specialty = request.Specialty,
            CreatedAt = user.CreatedAt
        };

        return ApiResponse<AdminUserDto>.Ok(resultDto, $"Tạo tài khoản {normalizedRole} '{user.Email}' thành công.");
    }

    public async Task<ApiResponse<AdminUserDto>> UpdateStaffAccountAsync(long userId, UpdateStaffRequest request, long adminUserId, CancellationToken ct = default)
    {
        var user = await _dbContext.UserAccounts
            .Include(u => u.StaffProfile)
                .ThenInclude(sp => sp!.DentistProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user == null || user.StaffProfile == null)
        {
            return ApiResponse<AdminUserDto>.Fail("Không tìm thấy nhân viên.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ApiResponse<AdminUserDto>.Fail("Họ và tên không được để trống.");
        }

        var oldValues = JsonSerializer.Serialize(new { user.StaffProfile.FullName, user.PhoneNumber, user.Status });

        user.StaffProfile.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            user.Status = request.Status.Trim();
        }
        if (user.StaffProfile.DentistProfile != null && !string.IsNullOrWhiteSpace(request.Specialty))
        {
            user.StaffProfile.DentistProfile.Qualification = request.Specialty.Trim();
        }
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { user.StaffProfile.FullName, user.PhoneNumber, user.Status });

        await _auditLogService.LogAsync(
            adminUserId,
            "UPDATE_STAFF_ACCOUNT",
            "StaffProfile",
            user.UserId.ToString(),
            oldValues,
            newValues,
            ct);

        var resultDto = new AdminUserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FullName = user.StaffProfile.FullName,
            Code = user.StaffProfile.EmployeeCode,
            Role = user.Role,
            Status = user.Status,
            Specialty = user.StaffProfile.DentistProfile?.Qualification,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };

        return ApiResponse<AdminUserDto>.Ok(resultDto, "Cập nhật thông tin nhân viên thành công.");
    }

    // ==========================================
    // SYSTEM & DATABASE INFO
    // ==========================================

    public async Task<ApiResponse<DatabaseInfoDto>> GetDatabaseInfoAsync(CancellationToken ct = default)
    {
        var dbConn = _dbContext.Database.GetDbConnection();
        var dbName = dbConn.Database;
        var serverName = dbConn.DataSource;

        var totalUsers = await _dbContext.UserAccounts.CountAsync(ct);
        var totalServices = await _dbContext.DepartmentServices.CountAsync(ct);
        var totalAppointments = await _dbContext.Appointments.CountAsync(ct);

        var dto = new DatabaseInfoDto
        {
            DatabaseName = dbName,
            ServerName = serverName,
            Status = "Connected",
            ServerTime = DateTime.UtcNow,
            TotalUsers = totalUsers,
            TotalServices = totalServices,
            TotalAppointments = totalAppointments
        };

        return ApiResponse<DatabaseInfoDto>.Ok(dto);
    }
}
