using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.AspNetCore.Http;

namespace DentalClinic.Web.Services;

public interface IBlogApiService
{
    // Public
    Task<ApiResult<BlogPostPagedResultDto>> GetPublicPostsAsync(BlogFilterRequest request);
    Task<ApiResult<BlogPostDto>> GetPostBySlugAsync(string slug);
    Task<ApiResult<List<BlogPostListItemDto>>> GetLatestPostsAsync(int count = 3);
    Task<ApiResult<List<BlogCategoryDto>>> GetCategoriesAsync();

    // Dentist / Author
    Task<ApiResult<BlogPostPagedResultDto>> GetMyPostsAsync(BlogFilterRequest request);
    Task<ApiResult<BlogPostDto>> GetPostByIdAsync(long id);
    Task<ApiResult<BlogPostDto>> CreatePostAsync(CreateBlogPostRequest request);
    Task<ApiResult<BlogPostDto>> UpdatePostAsync(long id, UpdateBlogPostRequest request);
    Task<ApiResult<bool>> DeletePostAsync(long id);
    Task<ApiResult<string>> UploadThumbnailAsync(IFormFile file);

    // Admin
    Task<ApiResult<BlogPostPagedResultDto>> GetAllPostsForAdminAsync(BlogFilterRequest request);
    Task<ApiResult<BlogPostDto>> ReviewPostAsync(long id, ReviewBlogPostRequest request);
    Task<ApiResult<bool>> ToggleFeaturedAsync(long id);
    Task<ApiResult<BlogCategoryDto>> CreateCategoryAsync(CreateBlogCategoryRequest request);
    Task<ApiResult<BlogCategoryDto>> UpdateCategoryAsync(int id, UpdateBlogCategoryRequest request);
    Task<ApiResult<bool>> DeleteCategoryAsync(int id);
    // Media Upload & Storage
    Task<ApiResult<BlogMediaDto>> UploadMediaAsync(IFormFile file, string? mediaType = null, long? blogPostId = null);
    Task<ApiResult<bool>> DeleteMediaAsync(long mediaId);
}
