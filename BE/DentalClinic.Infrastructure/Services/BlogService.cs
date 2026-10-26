using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Helpers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class BlogService : IBlogService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<BlogService> _logger;
    private readonly HtmlSanitizer _htmlSanitizer;

    public BlogService(
        DentalClinicDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<BlogService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;

        _htmlSanitizer = new HtmlSanitizer();
        _htmlSanitizer.AllowedSchemes.Add("data");
    }

    // ==========================================
    // PUBLIC / GUEST / PATIENT
    // ==========================================

    public async Task<ApiResponse<BlogPostPagedResultDto>> GetPublicPostsAsync(BlogFilterRequest request, CancellationToken ct = default)
    {
        try
        {
            var query = _dbContext.BlogPosts
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .Include(p => p.AuthorUser).ThenInclude(u => u.PatientProfile)
                .Where(p => p.Status == BlogPostStatus.Published);

            if (!string.IsNullOrWhiteSpace(request.CategorySlug))
            {
                var trimmedSlug = request.CategorySlug.Trim().ToLowerInvariant();
                query = query.Where(p => p.Category.Slug == trimmedSlug);
            }
            else if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == request.CategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var kw = request.Keyword.Trim();
                query = query.Where(p => p.Title.Contains(kw) || (p.Summary != null && p.Summary.Contains(kw)));
            }

            if (request.IsFeatured.HasValue)
            {
                query = query.Where(p => p.IsFeatured == request.IsFeatured.Value);
            }

            var totalCount = await query.CountAsync(ct);

            var page = Math.Max(1, request.Page);
            var pageSize = request.PageSize > 0 ? request.PageSize : 9;

            var posts = await query
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.PublishedAt ?? p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var result = new BlogPostPagedResultDto
            {
                Items = posts.Select(MapToListItemDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<BlogPostPagedResultDto>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách bài viết công khai.");
            return ApiResponse<BlogPostPagedResultDto>.Fail("Không thể tải danh sách bài viết.");
        }
    }

    public async Task<ApiResponse<BlogPostDto>> GetPostBySlugAsync(string slug, bool incrementView = true, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return ApiResponse<BlogPostDto>.Fail("Slug bài viết không hợp lệ.");
            }

            var cleanSlug = slug.Trim().ToLowerInvariant();

            var post = await _dbContext.BlogPosts
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .Include(p => p.AuthorUser).ThenInclude(u => u.PatientProfile)
                .FirstOrDefaultAsync(p => p.Slug == cleanSlug && p.Status == BlogPostStatus.Published, ct);

            if (post == null)
            {
                return ApiResponse<BlogPostDto>.Fail("Bài viết không tồn tại hoặc chưa được xuất bản.");
            }

            if (incrementView)
            {
                post.ViewCount += 1;
                await _dbContext.SaveChangesAsync(ct);
            }

            var dto = MapToDetailDto(post);

            // Related posts: same category, published, exclude current
            var related = await _dbContext.BlogPosts
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .Where(p => p.CategoryId == post.CategoryId && p.BlogPostId != post.BlogPostId && p.Status == BlogPostStatus.Published)
                .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
                .Take(4)
                .ToListAsync(ct);

            dto.RelatedPosts = related.Select(MapToListItemDto).ToList();

            return ApiResponse<BlogPostDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải chi tiết bài viết slug: {Slug}", slug);
            return ApiResponse<BlogPostDto>.Fail("Không thể tải chi tiết bài viết.");
        }
    }

    public async Task<ApiResponse<List<BlogPostListItemDto>>> GetLatestPostsAsync(int count = 3, CancellationToken ct = default)
    {
        try
        {
            var take = count > 0 ? Math.Min(count, 12) : 3;

            var posts = await _dbContext.BlogPosts
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .Where(p => p.Status == BlogPostStatus.Published)
                .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
                .Take(take)
                .ToListAsync(ct);

            var dtos = posts.Select(MapToListItemDto).ToList();
            return ApiResponse<List<BlogPostListItemDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách bài viết mới nhất.");
            return ApiResponse<List<BlogPostListItemDto>>.Fail("Không thể tải danh sách bài viết mới nhất.");
        }
    }

    public async Task<ApiResponse<List<BlogCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var categories = await _dbContext.BlogCategories
                .AsNoTracking()
                .Select(c => new BlogCategoryDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Slug = c.Slug,
                    PostCount = c.BlogPosts.Count(p => p.Status == BlogPostStatus.Published),
                    CreatedAt = c.CreatedAt
                })
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

            return ApiResponse<List<BlogCategoryDto>>.Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh mục bài viết.");
            return ApiResponse<List<BlogCategoryDto>>.Fail("Không thể tải danh mục bài viết.");
        }
    }

    // ==========================================
    // DENTIST / AUTHOR
    // ==========================================

    public async Task<ApiResponse<BlogPostPagedResultDto>> GetMyPostsAsync(long authorUserId, BlogFilterRequest request, CancellationToken ct = default)
    {
        try
        {
            var query = _dbContext.BlogPosts
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .Where(p => p.AuthorUserId == authorUserId);

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(p => p.Status == request.Status);
            }

            if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == request.CategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var kw = request.Keyword.Trim();
                query = query.Where(p => p.Title.Contains(kw) || (p.Summary != null && p.Summary.Contains(kw)));
            }

            var totalCount = await query.CountAsync(ct);

            var page = Math.Max(1, request.Page);
            var pageSize = request.PageSize > 0 ? request.PageSize : 10;

            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var result = new BlogPostPagedResultDto
            {
                Items = posts.Select(MapToListItemDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<BlogPostPagedResultDto>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách bài viết của tác giả {AuthorUserId}", authorUserId);
            return ApiResponse<BlogPostPagedResultDto>.Fail("Không thể tải bài viết của bạn.");
        }
    }

    public async Task<ApiResponse<BlogPostDto>> GetPostByIdForAuthorAsync(long postId, long currentUserId, bool isAdmin = false, CancellationToken ct = default)
    {
        try
        {
            var post = await _dbContext.BlogPosts
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .FirstOrDefaultAsync(p => p.BlogPostId == postId, ct);

            if (post == null)
            {
                return ApiResponse<BlogPostDto>.Fail("Không tìm thấy bài viết.");
            }

            if (!isAdmin && post.AuthorUserId != currentUserId)
            {
                return ApiResponse<BlogPostDto>.Fail("Bạn không có quyền truy cập bài viết này.");
            }

            return ApiResponse<BlogPostDto>.Ok(MapToDetailDto(post));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy chi tiết bài viết {PostId}", postId);
            return ApiResponse<BlogPostDto>.Fail("Không thể tải bài viết.");
        }
    }

    public async Task<ApiResponse<BlogPostDto>> CreatePostAsync(long authorUserId, CreateBlogPostRequest request, CancellationToken ct = default)
    {
        try
        {
            var categoryExists = await _dbContext.BlogCategories.AnyAsync(c => c.CategoryId == request.CategoryId, ct);
            if (!categoryExists)
            {
                return ApiResponse<BlogPostDto>.Fail("Chuyên mục bài viết không tồn tại.");
            }

            var sanitizedContent = _htmlSanitizer.Sanitize(request.Content ?? string.Empty);
            var slug = await GenerateUniqueSlugAsync(request.Title, null, ct);

            var status = string.Equals(request.Status, BlogPostStatus.PendingReview, StringComparison.OrdinalIgnoreCase)
                ? BlogPostStatus.PendingReview
                : BlogPostStatus.Draft;

            var post = new BlogPost
            {
                CategoryId = request.CategoryId,
                AuthorUserId = authorUserId,
                Title = request.Title.Trim(),
                Slug = slug,
                Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim(),
                Content = sanitizedContent,
                ThumbnailUrl = string.IsNullOrWhiteSpace(request.ThumbnailUrl) ? null : request.ThumbnailUrl.Trim(),
                Status = status,
                IsFeatured = false,
                ViewCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                PublishedAt = null
            };

            _dbContext.BlogPosts.Add(post);
            await _dbContext.SaveChangesAsync(ct);

            // Re-load relationships for mapping
            await _dbContext.Entry(post).Reference(p => p.Category).LoadAsync(ct);
            await _dbContext.Entry(post).Reference(p => p.AuthorUser).Query()
                .Include(u => u.StaffProfile)
                .LoadAsync(ct);

            return ApiResponse<BlogPostDto>.Ok(MapToDetailDto(post), "Tạo bài viết thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo bài viết bởi người dùng {AuthorUserId}", authorUserId);
            return ApiResponse<BlogPostDto>.Fail("Không thể tạo bài viết.");
        }
    }

    public async Task<ApiResponse<BlogPostDto>> UpdatePostAsync(long postId, long currentUserId, bool isAdmin, UpdateBlogPostRequest request, CancellationToken ct = default)
    {
        try
        {
            var post = await _dbContext.BlogPosts
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .FirstOrDefaultAsync(p => p.BlogPostId == postId, ct);

            if (post == null)
            {
                return ApiResponse<BlogPostDto>.Fail("Không tìm thấy bài viết.");
            }

            if (!isAdmin && post.AuthorUserId != currentUserId)
            {
                return ApiResponse<BlogPostDto>.Fail("Bạn không có quyền chỉnh sửa bài viết này.");
            }

            var categoryExists = await _dbContext.BlogCategories.AnyAsync(c => c.CategoryId == request.CategoryId, ct);
            if (!categoryExists)
            {
                return ApiResponse<BlogPostDto>.Fail("Chuyên mục bài viết không tồn tại.");
            }

            var oldTitle = post.Title;
            var oldStatus = post.Status;

            // If title changed, re-generate unique slug
            if (!string.Equals(post.Title, request.Title.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                post.Slug = await GenerateUniqueSlugAsync(request.Title, postId, ct);
            }

            post.Title = request.Title.Trim();
            post.CategoryId = request.CategoryId;
            post.Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim();
            post.Content = _htmlSanitizer.Sanitize(request.Content ?? string.Empty);
            post.ThumbnailUrl = string.IsNullOrWhiteSpace(request.ThumbnailUrl) ? null : request.ThumbnailUrl.Trim();
            post.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (isAdmin)
                {
                    if (BlogPostStatus.IsValid(request.Status))
                    {
                        post.Status = request.Status;
                        if (post.Status == BlogPostStatus.Published && post.PublishedAt == null)
                        {
                            post.PublishedAt = DateTime.UtcNow;
                        }
                    }
                }
                else
                {
                    // Dentists can only set Draft or PendingReview
                    if (request.Status == BlogPostStatus.Draft || request.Status == BlogPostStatus.PendingReview)
                    {
                        post.Status = request.Status;
                    }
                    else if (post.Status == BlogPostStatus.Published)
                    {
                        // Editing a published post by dentist resets it to PendingReview
                        post.Status = BlogPostStatus.PendingReview;
                    }
                }
            }

            await _dbContext.SaveChangesAsync(ct);

            // Reload category if changed
            await _dbContext.Entry(post).Reference(p => p.Category).LoadAsync(ct);

            // Audit log if modified by admin or status changed
            if (isAdmin || oldStatus != post.Status)
            {
                await _auditLogService.LogAsync(
                    currentUserId,
                    "UpdateBlogPost",
                    "BlogPost",
                    post.BlogPostId.ToString(),
                    JsonSerializer.Serialize(new { Title = oldTitle, Status = oldStatus }),
                    JsonSerializer.Serialize(new { Title = post.Title, Status = post.Status }),
                    ct);
            }

            return ApiResponse<BlogPostDto>.Ok(MapToDetailDto(post), "Cập nhật bài viết thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật bài viết {PostId}", postId);
            return ApiResponse<BlogPostDto>.Fail("Không thể cập nhật bài viết.");
        }
    }

    public async Task<ApiResponse<bool>> DeletePostAsync(long postId, long currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        try
        {
            var post = await _dbContext.BlogPosts.FirstOrDefaultAsync(p => p.BlogPostId == postId, ct);
            if (post == null)
            {
                return ApiResponse<bool>.Fail("Không tìm thấy bài viết.");
            }

            if (!isAdmin && post.AuthorUserId != currentUserId)
            {
                return ApiResponse<bool>.Fail("Bạn không có quyền xóa bài viết này.");
            }

            var deletedTitle = post.Title;
            var deletedStatus = post.Status;

            _dbContext.BlogPosts.Remove(post);
            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                currentUserId,
                "DeleteBlogPost",
                "BlogPost",
                postId.ToString(),
                JsonSerializer.Serialize(new { Title = deletedTitle, Status = deletedStatus }),
                null,
                ct);

            return ApiResponse<bool>.Ok(true, "Xóa bài viết thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa bài viết {PostId}", postId);
            return ApiResponse<bool>.Fail("Không thể xóa bài viết.");
        }
    }

    // ==========================================
    // SYSTEM ADMINISTRATOR
    // ==========================================

    public async Task<ApiResponse<BlogPostPagedResultDto>> GetAllPostsForAdminAsync(BlogFilterRequest request, CancellationToken ct = default)
    {
        try
        {
            var query = _dbContext.BlogPosts
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(p => p.Status == request.Status);
            }

            if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == request.CategoryId.Value);
            }

            if (request.AuthorUserId.HasValue && request.AuthorUserId.Value > 0)
            {
                query = query.Where(p => p.AuthorUserId == request.AuthorUserId.Value);
            }

            if (request.IsFeatured.HasValue)
            {
                query = query.Where(p => p.IsFeatured == request.IsFeatured.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var kw = request.Keyword.Trim();
                query = query.Where(p => p.Title.Contains(kw) || (p.Summary != null && p.Summary.Contains(kw)));
            }

            var totalCount = await query.CountAsync(ct);

            var page = Math.Max(1, request.Page);
            var pageSize = request.PageSize > 0 ? request.PageSize : 15;

            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var result = new BlogPostPagedResultDto
            {
                Items = posts.Select(MapToListItemDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<BlogPostPagedResultDto>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách bài viết cho Admin.");
            return ApiResponse<BlogPostPagedResultDto>.Fail("Không thể tải danh sách bài viết.");
        }
    }

    public async Task<ApiResponse<BlogPostDto>> ReviewPostAsync(long postId, long adminUserId, ReviewBlogPostRequest request, CancellationToken ct = default)
    {
        try
        {
            var post = await _dbContext.BlogPosts
                .Include(p => p.Category)
                .Include(p => p.AuthorUser).ThenInclude(u => u.StaffProfile)
                .FirstOrDefaultAsync(p => p.BlogPostId == postId, ct);

            if (post == null)
            {
                return ApiResponse<BlogPostDto>.Fail("Không tìm thấy bài viết.");
            }

            if (!BlogPostStatus.IsValid(request.Status))
            {
                return ApiResponse<BlogPostDto>.Fail("Trạng thái duyệt không hợp lệ.");
            }

            var oldStatus = post.Status;
            post.Status = request.Status;

            if (request.Status == BlogPostStatus.Published && post.PublishedAt == null)
            {
                post.PublishedAt = DateTime.UtcNow;
            }

            if (request.IsFeatured.HasValue)
            {
                post.IsFeatured = request.IsFeatured.Value;
            }

            post.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                adminUserId,
                "ReviewBlogPost",
                "BlogPost",
                post.BlogPostId.ToString(),
                JsonSerializer.Serialize(new { Status = oldStatus }),
                JsonSerializer.Serialize(new { Status = post.Status, IsFeatured = post.IsFeatured, Notes = request.ReviewNotes }),
                ct);

            return ApiResponse<BlogPostDto>.Ok(MapToDetailDto(post), $"Đã chuyển trạng thái bài viết sang {request.Status}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi duyệt bài viết {PostId}", postId);
            return ApiResponse<BlogPostDto>.Fail("Không thể cập nhật trạng thái duyệt bài viết.");
        }
    }

    public async Task<ApiResponse<bool>> ToggleFeaturedAsync(long postId, long adminUserId, CancellationToken ct = default)
    {
        try
        {
            var post = await _dbContext.BlogPosts.FirstOrDefaultAsync(p => p.BlogPostId == postId, ct);
            if (post == null)
            {
                return ApiResponse<bool>.Fail("Không tìm thấy bài viết.");
            }

            post.IsFeatured = !post.IsFeatured;
            post.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                adminUserId,
                "ToggleFeaturedBlogPost",
                "BlogPost",
                post.BlogPostId.ToString(),
                JsonSerializer.Serialize(new { IsFeatured = !post.IsFeatured }),
                JsonSerializer.Serialize(new { IsFeatured = post.IsFeatured }),
                ct);

            return ApiResponse<bool>.Ok(post.IsFeatured, post.IsFeatured ? "Đã ghim bài viết nổi bật." : "Đã bỏ ghim nổi bật.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thay đổi ghim nổi bật cho bài viết {PostId}", postId);
            return ApiResponse<bool>.Fail("Không thể thay đổi ghim bài viết nổi bật.");
        }
    }

    // ==========================================
    // CATEGORY MANAGEMENT (CRUD)
    // ==========================================

    public async Task<ApiResponse<BlogCategoryDto>> CreateCategoryAsync(long adminUserId, CreateBlogCategoryRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return ApiResponse<BlogCategoryDto>.Fail("Tên chuyên mục không được để trống.");
            }

            var slug = !string.IsNullOrWhiteSpace(request.Slug)
                ? SlugHelper.GenerateSlug(request.Slug)
                : await GenerateUniqueCategorySlugAsync(request.Name, null, ct);

            var duplicate = await _dbContext.BlogCategories.AnyAsync(c => c.Slug == slug, ct);
            if (duplicate)
            {
                return ApiResponse<BlogCategoryDto>.Fail("Chuyên mục với slug này đã tồn tại.");
            }

            var category = new BlogCategory
            {
                Name = request.Name.Trim(),
                Slug = slug,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.BlogCategories.Add(category);
            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                adminUserId,
                "CreateBlogCategory",
                "BlogCategory",
                category.CategoryId.ToString(),
                null,
                JsonSerializer.Serialize(new { category.Name, category.Slug }),
                ct);

            return ApiResponse<BlogCategoryDto>.Ok(new BlogCategoryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Slug = category.Slug,
                PostCount = 0,
                CreatedAt = category.CreatedAt
            }, "Tạo chuyên mục thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo chuyên mục bài viết.");
            return ApiResponse<BlogCategoryDto>.Fail("Không thể tạo chuyên mục.");
        }
    }

    public async Task<ApiResponse<BlogCategoryDto>> UpdateCategoryAsync(int categoryId, long adminUserId, UpdateBlogCategoryRequest request, CancellationToken ct = default)
    {
        try
        {
            var category = await _dbContext.BlogCategories
                .Include(c => c.BlogPosts)
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);

            if (category == null)
            {
                return ApiResponse<BlogCategoryDto>.Fail("Không tìm thấy chuyên mục.");
            }

            var oldName = category.Name;
            var oldSlug = category.Slug;

            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var newSlug = SlugHelper.GenerateSlug(request.Slug);
                var duplicate = await _dbContext.BlogCategories.AnyAsync(c => c.Slug == newSlug && c.CategoryId != categoryId, ct);
                if (duplicate)
                {
                    return ApiResponse<BlogCategoryDto>.Fail("Slug chuyên mục đã tồn tại.");
                }
                category.Slug = newSlug;
            }
            else if (!string.Equals(category.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                category.Slug = await GenerateUniqueCategorySlugAsync(request.Name, categoryId, ct);
            }

            category.Name = request.Name.Trim();
            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                adminUserId,
                "UpdateBlogCategory",
                "BlogCategory",
                categoryId.ToString(),
                JsonSerializer.Serialize(new { Name = oldName, Slug = oldSlug }),
                JsonSerializer.Serialize(new { category.Name, category.Slug }),
                ct);

            return ApiResponse<BlogCategoryDto>.Ok(new BlogCategoryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Slug = category.Slug,
                PostCount = category.BlogPosts.Count(p => p.Status == BlogPostStatus.Published),
                CreatedAt = category.CreatedAt
            }, "Cập nhật chuyên mục thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật chuyên mục {CategoryId}", categoryId);
            return ApiResponse<BlogCategoryDto>.Fail("Không thể cập nhật chuyên mục.");
        }
    }

    public async Task<ApiResponse<bool>> DeleteCategoryAsync(int categoryId, long adminUserId, CancellationToken ct = default)
    {
        try
        {
            var category = await _dbContext.BlogCategories.FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);
            if (category == null)
            {
                return ApiResponse<bool>.Fail("Không tìm thấy chuyên mục.");
            }

            var hasPosts = await _dbContext.BlogPosts.AnyAsync(p => p.CategoryId == categoryId, ct);
            if (hasPosts)
            {
                return ApiResponse<bool>.Fail("Không thể xóa chuyên mục đang có bài viết. Vui lòng chuyển hoặc xóa bài viết trước.");
            }

            var oldName = category.Name;
            _dbContext.BlogCategories.Remove(category);
            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                adminUserId,
                "DeleteBlogCategory",
                "BlogCategory",
                categoryId.ToString(),
                JsonSerializer.Serialize(new { Name = oldName }),
                null,
                ct);

            return ApiResponse<bool>.Ok(true, "Xóa chuyên mục thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa chuyên mục {CategoryId}", categoryId);
            return ApiResponse<bool>.Fail("Không thể xóa chuyên mục.");
        }
    }

    // ==========================================
    // HELPERS & MAPPERS
    // ==========================================

    private async Task<string> GenerateUniqueSlugAsync(string title, long? currentPostId = null, CancellationToken ct = default)
    {
        var baseSlug = SlugHelper.GenerateSlug(title);
        var slug = baseSlug;
        var counter = 1;

        while (await _dbContext.BlogPosts.AnyAsync(p => p.Slug == slug && (!currentPostId.HasValue || p.BlogPostId != currentPostId.Value), ct))
        {
            counter++;
            slug = $"{baseSlug}-{counter}";
        }

        return slug;
    }

    private async Task<string> GenerateUniqueCategorySlugAsync(string name, int? currentCategoryId = null, CancellationToken ct = default)
    {
        var baseSlug = SlugHelper.GenerateSlug(name);
        var slug = baseSlug;
        var counter = 1;

        while (await _dbContext.BlogCategories.AnyAsync(c => c.Slug == slug && (!currentCategoryId.HasValue || c.CategoryId != currentCategoryId.Value), ct))
        {
            counter++;
            slug = $"{baseSlug}-{counter}";
        }

        return slug;
    }

    private static BlogPostListItemDto MapToListItemDto(BlogPost post)
    {
        return new BlogPostListItemDto
        {
            BlogPostId = post.BlogPostId,
            CategoryId = post.CategoryId,
            CategoryName = post.Category?.Name ?? string.Empty,
            CategorySlug = post.Category?.Slug ?? string.Empty,
            AuthorUserId = post.AuthorUserId,
            AuthorName = GetAuthorName(post.AuthorUser),
            AuthorAvatarUrl = post.AuthorUser?.AvatarUrl,
            AuthorRole = post.AuthorUser?.Role,
            Title = post.Title,
            Slug = post.Slug,
            Summary = post.Summary,
            ThumbnailUrl = post.ThumbnailUrl,
            Status = post.Status,
            IsFeatured = post.IsFeatured,
            ViewCount = post.ViewCount,
            PublishedAt = post.PublishedAt,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt
        };
    }

    private static BlogPostDto MapToDetailDto(BlogPost post)
    {
        return new BlogPostDto
        {
            BlogPostId = post.BlogPostId,
            CategoryId = post.CategoryId,
            CategoryName = post.Category?.Name ?? string.Empty,
            CategorySlug = post.Category?.Slug ?? string.Empty,
            AuthorUserId = post.AuthorUserId,
            AuthorName = GetAuthorName(post.AuthorUser),
            AuthorAvatarUrl = post.AuthorUser?.AvatarUrl,
            AuthorRole = post.AuthorUser?.Role,
            Title = post.Title,
            Slug = post.Slug,
            Summary = post.Summary,
            Content = post.Content,
            ThumbnailUrl = post.ThumbnailUrl,
            Status = post.Status,
            IsFeatured = post.IsFeatured,
            ViewCount = post.ViewCount,
            PublishedAt = post.PublishedAt,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            RelatedPosts = new List<BlogPostListItemDto>()
        };
    }

    private static string GetAuthorName(UserAccount? user)
    {
        if (user == null) return "Nha khoa DentalClinic";
        if (user.StaffProfile != null && !string.IsNullOrWhiteSpace(user.StaffProfile.FullName))
        {
            return (user.Role == UserRole.Dentist || user.Role == UserRole.DepartmentManager)
                ? $"BS. {user.StaffProfile.FullName}"
                : user.StaffProfile.FullName;
        }
        if (user.PatientProfile != null && !string.IsNullOrWhiteSpace(user.PatientProfile.FullName))
        {
            return user.PatientProfile.FullName;
        }
        return !string.IsNullOrWhiteSpace(user.Email) ? user.Email : "Chuyên gia Nha khoa";
    }
}
