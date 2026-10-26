using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Blog.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IBlogService
{
    // ==========================================
    // PUBLIC / PATIENT / GUEST
    // ==========================================
    Task<ApiResponse<BlogPostPagedResultDto>> GetPublicPostsAsync(BlogFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<BlogPostDto>> GetPostBySlugAsync(string slug, bool incrementView = true, CancellationToken ct = default);
    Task<ApiResponse<List<BlogPostListItemDto>>> GetLatestPostsAsync(int count = 3, CancellationToken ct = default);
    Task<ApiResponse<List<BlogCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default);

    // ==========================================
    // DENTIST / DEPARTMENT MANAGER ("BÀI VIẾT CỦA TÔI")
    // ==========================================
    Task<ApiResponse<BlogPostPagedResultDto>> GetMyPostsAsync(long authorUserId, BlogFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<BlogPostDto>> GetPostByIdForAuthorAsync(long postId, long currentUserId, bool isAdmin = false, CancellationToken ct = default);
    Task<ApiResponse<BlogPostDto>> CreatePostAsync(long authorUserId, CreateBlogPostRequest request, CancellationToken ct = default);
    Task<ApiResponse<BlogPostDto>> UpdatePostAsync(long postId, long currentUserId, bool isAdmin, UpdateBlogPostRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeletePostAsync(long postId, long currentUserId, bool isAdmin, CancellationToken ct = default);

    // ==========================================
    // SYSTEM ADMINISTRATOR
    // ==========================================
    Task<ApiResponse<BlogPostPagedResultDto>> GetAllPostsForAdminAsync(BlogFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<BlogPostDto>> ReviewPostAsync(long postId, long adminUserId, ReviewBlogPostRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> ToggleFeaturedAsync(long postId, long adminUserId, CancellationToken ct = default);

    // CATEGORY CRUD
    Task<ApiResponse<BlogCategoryDto>> CreateCategoryAsync(long adminUserId, CreateBlogCategoryRequest request, CancellationToken ct = default);
    Task<ApiResponse<BlogCategoryDto>> UpdateCategoryAsync(int categoryId, long adminUserId, UpdateBlogCategoryRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteCategoryAsync(int categoryId, long adminUserId, CancellationToken ct = default);
}
