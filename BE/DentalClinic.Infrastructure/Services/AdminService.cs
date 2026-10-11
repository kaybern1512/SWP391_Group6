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

    public AdminService(
        DentalClinicDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<AdminService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;
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
                return ApiResponse<AdminDepartmentDto>.Fail("Trưởng khoa được chỉ định không tồn tại trong danh sách nhân sự.");
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
                return ApiResponse<AdminDepartmentDto>.Fail("Trưởng khoa được chỉ định không tồn tại.");
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

        var dtos = services.Select(s => new AdminDepartmentServiceDto
        {
            DepartmentServiceId = s.DepartmentServiceId,
            DepartmentId = s.DepartmentId,
            DepartmentName = s.Department?.Name ?? string.Empty,
            ServiceName = s.ServiceName,
            Description = s.Description,
            DurationMinutes = s.DurationMinutes,
            Price = s.Price,
            Status = s.Status
        }).ToList();

        return ApiResponse<List<AdminDepartmentServiceDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AdminDepartmentServiceDto>> GetServiceByIdAsync(long serviceId, CancellationToken ct = default)
    {
        var s = await _dbContext.DepartmentServices
            .AsNoTracking()
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == serviceId, ct);

        if (s == null)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Không tìm thấy dịch vụ.");
        }

        var dto = new AdminDepartmentServiceDto
        {
            DepartmentServiceId = s.DepartmentServiceId,
            DepartmentId = s.DepartmentId,
            DepartmentName = s.Department?.Name ?? string.Empty,
            ServiceName = s.ServiceName,
            Description = s.Description,
            DurationMinutes = s.DurationMinutes,
            Price = s.Price,
            Status = s.Status
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

        var deptExists = await _dbContext.Departments
            .AnyAsync(d => d.DepartmentId == request.DepartmentId, ct);

        if (!deptExists)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Chuyên khoa không tồn tại.");
        }

        var trimmedName = request.ServiceName.Trim();
        var exists = await _dbContext.DepartmentServices
            .AnyAsync(s => s.DepartmentId == request.DepartmentId && s.ServiceName.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail($"Dịch vụ '{trimmedName}' đã tồn tại trong chuyên khoa này.");
        }

        var service = new DepartmentService
        {
            DepartmentId = request.DepartmentId,
            ServiceName = trimmedName,
            Description = request.Description?.Trim(),
            DurationMinutes = request.DurationMinutes.HasValue && request.DurationMinutes.Value > 0 ? request.DurationMinutes.Value : 30,
            Price = request.Price,
            Status = string.Equals(request.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active"
        };

        _dbContext.DepartmentServices.Add(service);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CREATE",
            "DepartmentService",
            service.DepartmentServiceId.ToString(),
            null,
            JsonSerializer.Serialize(new { service.ServiceName, service.DepartmentId, service.Price, service.Status }),
            ct);

        return await GetServiceByIdAsync(service.DepartmentServiceId, ct);
    }

    public async Task<ApiResponse<AdminDepartmentServiceDto>> UpdateServiceAsync(long serviceId, UpdateDepartmentServiceRequest request, long? adminUserId = null, CancellationToken ct = default)
    {
        var service = await _dbContext.DepartmentServices
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

        var deptExists = await _dbContext.Departments
            .AnyAsync(d => d.DepartmentId == request.DepartmentId, ct);

        if (!deptExists)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail("Chuyên khoa không tồn tại.");
        }

        var trimmedName = request.ServiceName.Trim();
        var conflict = await _dbContext.DepartmentServices
            .AnyAsync(s => s.DepartmentServiceId != serviceId && s.DepartmentId == request.DepartmentId && s.ServiceName.ToLower() == trimmedName.ToLower(), ct);

        if (conflict)
        {
            return ApiResponse<AdminDepartmentServiceDto>.Fail($"Tên dịch vụ '{trimmedName}' đã được sử dụng trong chuyên khoa này.");
        }

        var oldValues = JsonSerializer.Serialize(new { service.ServiceName, service.DepartmentId, service.Price, service.Status });

        service.DepartmentId = request.DepartmentId;
        service.ServiceName = trimmedName;
        service.Description = request.Description?.Trim();
        service.DurationMinutes = request.DurationMinutes.HasValue && request.DurationMinutes.Value > 0 ? request.DurationMinutes.Value : 30;
        service.Price = request.Price;
        service.Status = string.Equals(request.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";

        await _dbContext.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { service.ServiceName, service.DepartmentId, service.Price, service.Status });

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
            .Include(r => r.DentalChairs)
            .OrderBy(r => r.RoomId)
            .ToListAsync(ct);

        var dtos = rooms.Select(r => new AdminRoomDto
        {
            RoomId = r.RoomId,
            RoomCode = r.RoomCode,
            RoomName = r.RoomName,
            Status = r.Status,
            ChairsCount = r.DentalChairs.Count,
            ActiveChairsCount = r.DentalChairs.Count(c => c.Status == "Active")
        }).ToList();

        return ApiResponse<List<AdminRoomDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AdminRoomDto>> GetRoomByIdAsync(long roomId, CancellationToken ct = default)
    {
        var r = await _dbContext.Rooms
            .AsNoTracking()
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
            Status = r.Status,
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
            Status = status
        };

        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CREATE",
            "Room",
            room.RoomId.ToString(),
            null,
            JsonSerializer.Serialize(new { room.RoomCode, room.RoomName, room.Status }),
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

        var oldValues = JsonSerializer.Serialize(new { room.RoomCode, room.RoomName, room.Status });

        room.RoomCode = trimmedCode;
        room.RoomName = string.IsNullOrWhiteSpace(request.RoomName) ? room.RoomName : request.RoomName.Trim();
        room.Status = status;

        await _dbContext.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { room.RoomCode, room.RoomName, room.Status });

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

        // 2. From PerformedServices if appointments completed
        var performedServiceRevenue = await _dbContext.PerformedServices
            .AsNoTracking()
            .Where(p => p.TreatmentSession.Appointment.Status == AppointmentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

        // 3. Fallback: estimate from completed appointment requested services price
        var completedApptPriceRevenue = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Completed && a.RequestedService != null)
            .SumAsync(a => (decimal?)a.RequestedService!.Price, ct) ?? 0;

        var totalRevenue = paidInvoiceRevenue > 0
            ? paidInvoiceRevenue
            : (performedServiceRevenue > 0 ? performedServiceRevenue : completedApptPriceRevenue);

        // Monthly Revenues for the current year
        var currentYear = DateTime.UtcNow.Year;
        var monthlyList = new List<MonthlyRevenueItemDto>();
        for (int m = 1; m <= 12; m++)
        {
            var apptsInMonth = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => a.ScheduledStart != null &&
                            a.ScheduledStart.Value.Year == currentYear &&
                            a.ScheduledStart.Value.Month == m)
                .ToListAsync(ct);

            var count = apptsInMonth.Count;
            var rev = apptsInMonth
                .Where(a => a.Status == AppointmentStatus.Completed && a.RequestedService != null)
                .Sum(a => a.RequestedService?.Price ?? 0);

            monthlyList.Add(new MonthlyRevenueItemDto
            {
                Year = currentYear,
                Month = m,
                MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                Revenue = rev,
                AppointmentCount = count
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
}
