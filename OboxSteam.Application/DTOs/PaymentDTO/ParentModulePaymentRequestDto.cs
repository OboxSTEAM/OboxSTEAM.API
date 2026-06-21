using System;
using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Request body for student to request parent to pay for a module retake fee.</summary>
public class ParentModulePaymentRequestDto
{
    [Required(ErrorMessage = "ModuleEnrollmentId is required.")]
    public Guid ModuleEnrollmentId { get; set; }

    [Required(ErrorMessage = "ParentId is required.")]
    public Guid ParentId { get; set; }
}
