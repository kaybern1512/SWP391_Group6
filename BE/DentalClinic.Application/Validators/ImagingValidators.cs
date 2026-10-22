using System;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using FluentValidation;

namespace DentalClinic.Application.Validators;

public class UploadImagingRecordRequestValidator : AbstractValidator<UploadImagingRecordRequest>
{
    public UploadImagingRecordRequestValidator()
    {
        RuleFor(x => x.ClinicalEntryId)
            .GreaterThan(0).WithMessage("Mã đợt khám (ClinicalEntryId) không hợp lệ.");

        RuleFor(x => x.ImagingType)
            .NotEmpty().WithMessage("Loại phim chụp không được để trống.")
            .Must(DentalImagingType.IsValid)
            .WithMessage("Loại phim chụp không hợp lệ. Chỉ chấp nhận 4 loại: Panorama, Periapical, Cephalometric, Intraoral.");

        RuleFor(x => x.ResultSummary)
            .MaximumLength(2000).WithMessage("Tóm tắt kết quả chẩn đoán không được vượt quá 2000 ký tự.");
    }
}

public class UpdateImagingResultSummaryRequestValidator : AbstractValidator<UpdateImagingResultSummaryRequest>
{
    public UpdateImagingResultSummaryRequestValidator()
    {
        RuleFor(x => x.ImagingRecordId)
            .GreaterThan(0).WithMessage("Mã bản ghi hình ảnh không hợp lệ.");

        RuleFor(x => x.ResultSummary)
            .NotEmpty().WithMessage("Kết quả chẩn đoán hình ảnh không được để trống.")
            .MaximumLength(2000).WithMessage("Kết quả chẩn đoán hình ảnh không được vượt quá 2000 ký tự.");
    }
}
