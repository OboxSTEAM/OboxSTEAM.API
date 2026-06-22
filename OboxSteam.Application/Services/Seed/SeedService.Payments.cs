using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedPaymentsAsync()
    {
        _loggerService.LogInformation("Starting seed payments");
        var existingPayments = await _unitOfWork.Payments.GetAllAsync();
        if (!existingPayments.Any())
        {
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync();

            if (student1 != null && programEnrollment != null)
            {
                var payments = new List<Payment>
                {
                    new Payment
                    {
                        Id = Guid.NewGuid(),
                        Code = "INV-26001",
                        StudentId = student1.Id,
                        PaidById = student1.Id,
                        ProgramEnrollmentId = programEnrollment.Id,
                        ModuleEnrollmentId = null,
                        Amount = 1_200_000m,
                        Gateway = PaymentGateway.Stripe,
                        TransactionId = "STRIPE-TXN-001",
                        Status = PaymentStatus.Success,
                        PaidAt = DateTime.UtcNow.AddDays(-14),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
                };

                await _unitOfWork.Payments.AddRangeAsync(payments);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed payments");
            }
        }
        else
        {
            _loggerService.LogInformation("Payments already exist, skipping seeding");
        }

    }
}

