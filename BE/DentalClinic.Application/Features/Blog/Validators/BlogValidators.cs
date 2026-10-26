using System;
using System.IO;
using System.Linq;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Domain.Enums;
using FluentValidation;

namespace DentalClinic.Application.Features.Blog.Validators;

public class CreateBlogPostRequestValidator : AbstractValidator<CreateBlogPostRequest>
{
    public CreateBlogPostRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Vui lòng chọn chuyên mục bài viết hợp lệ.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề bài viết không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề bài viết không được vượt quá 200 ký tự.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung bài viết không được để trống.");

        RuleFor(x => x.Summary)
            .MaximumLength(1000).WithMessage("Tóm tắt bài viết không được vượt quá 1000 ký tự.");

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrEmpty(s) || s == BlogPostStatus.Draft || s == BlogPostStatus.PendingReview)
            .WithMessage("Trạng thái khởi tạo chỉ có thể là Lưu nháp (Draft) hoặc Chờ duyệt (PendingReview).");
    }
}

public class UpdateBlogPostRequestValidator : AbstractValidator<UpdateBlogPostRequest>
{
    public UpdateBlogPostRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Vui lòng chọn chuyên mục bài viết hợp lệ.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề bài viết không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề bài viết không được vượt quá 200 ký tự.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung bài viết không được để trống.");

        RuleFor(x => x.Summary)
            .MaximumLength(1000).WithMessage("Tóm tắt bài viết không được vượt quá 1000 ký tự.");

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrEmpty(s) || BlogPostStatus.IsValid(s))
            .WithMessage("Trạng thái bài viết không hợp lệ.");
    }
}

public class ReviewBlogPostRequestValidator : AbstractValidator<ReviewBlogPostRequest>
{
    public ReviewBlogPostRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Vui lòng chọn trạng thái duyệt bài viết.")
            .Must(s => s == BlogPostStatus.Published || s == BlogPostStatus.Archived || s == BlogPostStatus.Draft)
            .WithMessage("Trạng thái duyệt chỉ chấp nhận: Published (Đã xuất bản), Archived (Lưu trữ), hoặc Draft (Yêu cầu chỉnh sửa).");

        RuleFor(x => x.ReviewNotes)
            .MaximumLength(500).WithMessage("Ghi chú duyệt không được vượt quá 500 ký tự.");
    }
}

public class CreateBlogCategoryRequestValidator : AbstractValidator<CreateBlogCategoryRequest>
{
    public CreateBlogCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên chuyên mục không được để trống.")
            .MaximumLength(100).WithMessage("Tên chuyên mục không được vượt quá 100 ký tự.");
    }
}
