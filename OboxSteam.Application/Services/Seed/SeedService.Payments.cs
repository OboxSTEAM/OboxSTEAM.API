using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedPaymentsAsync()
    {
        _loggerService.LogInformation("Starting seed payments from program enrollments");
        var existingPayments = await _unitOfWork.Payments.GetAllAsync();
        if (existingPayments.Any())
        {
            _loggerService.LogInformation("Payments already exist, skipping seeding");
            await SeedPendingPaymentRequestsAsync();
            return;
        }

        var enrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted,
            pe => pe.Student,
            pe => pe.Program);
        if (enrollments.Count == 0)
        {
            _loggerService.LogWarning("No program enrollments found. Skipping payment seeding.");
            return;
        }

        var payments = new List<Payment>();
        var invoices = new List<Invoice>();
        var paymentIndex = 1;
        var gateways = new[] { PaymentGateway.VnPay, PaymentGateway.Stripe, PaymentGateway.BankTransfer };

        foreach (var enrollment in enrollments)
        {
            var student = enrollment.Student
                ?? await _unitOfWork.Users.GetByIdAsync(enrollment.StudentId);
            var program = enrollment.Program
                ?? await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId);
            if (student == null || program == null)
            {
                continue;
            }

            if (enrollment.Status == EnrollmentStatus.PendingPayment)
            {
                continue;
            }

            // Failed/Dropped already paid the original purchase; rebuy is a new payment.
            ProgramEnrollment? source = null;
            if (enrollment.SourceProgramEnrollmentId.HasValue)
            {
                source = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
                    enrollment.SourceProgramEnrollmentId.Value);
            }

            var amount = ProgramPurchaseLifecycle.ResolveCheckoutAmount(program, source, _seedNow);
            var isRebuy = source != null;
            var paidAt = (enrollment.EnrolledAt ?? _seedNow).AddDays(-1);
            var status = PaymentStatus.Success;
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                Code = $"INV-{_seedNow:yy}{paymentIndex:D3}",
                StudentId = student.Id,
                PaidById = student.Id,
                ProgramEnrollmentId = enrollment.Id,
                Amount = amount,
                Gateway = gateways[paymentIndex % gateways.Length],
                TransactionId = $"SEED-TXN-{paymentIndex:D4}",
                Status = status,
                PaidAt = paidAt,
                CreatedAt = paidAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            payments.Add(payment);

            if (status == PaymentStatus.Success)
            {
                invoices.Add(new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = $"INV-SEED-{paymentIndex:D3}",
                    PaymentId = payment.Id,
                    IssuedToId = student.Id,
                    BillingName = student.FullName ?? student.Email,
                    BillingEmail = student.Email,
                    ItemDescription = isRebuy
                        ? $"{program.Name} chuyen ca"
                        : $"{program.Name} tuition",
                    SubTotal = payment.Amount,
                    TotalAmount = payment.Amount,
                    Currency = "VND",
                    CreatedAt = paidAt,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }

            paymentIndex++;
        }

        if (payments.Count > 0)
        {
            await _unitOfWork.Payments.AddRangeAsync(payments);
        }

        if (invoices.Count > 0)
        {
            await _unitOfWork.Invoices.AddRangeAsync(invoices);
        }

        if (payments.Count > 0 || invoices.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed payments — {PaymentCount} payment(s), {InvoiceCount} invoice(s).",
            payments.Count,
            invoices.Count);

        await SeedPendingPaymentRequestsAsync();
    }

    private async Task SeedPendingPaymentRequestsAsync()
    {
        var existing = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
            pr => pr.Token == "SEED-PENDING-PAYREQ-001" && !pr.IsDeleted);
        if (existing != null)
        {
            _loggerService.LogInformation("Pending payment requests already seeded, skipping");
            return;
        }

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "PRT-001" && !u.IsDeleted);
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-GAMEDEV" && !p.IsDeleted);
        if (parent == null || program == null)
        {
            _loggerService.LogWarning("Missing parent/program for pending payment request seed.");
            return;
        }

        var pendingEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted
                  && pe.Status == EnrollmentStatus.PendingPayment
                  && pe.ProgramId == program.Id);
        var parentLinks = await _unitOfWork.ParentStudents.GetAllAsync(
            ps => ps.ParentId == parent.Id && ps.IsVerified && !ps.IsDeleted);
        var linkedStudentIds = parentLinks.Select(ps => ps.StudentId).ToHashSet();

        var requests = new List<PaymentRequest>();
        var requestIndex = 1;
        foreach (var enrollment in pendingEnrollments.Where(pe => linkedStudentIds.Contains(pe.StudentId)))
        {
            var createdAt = enrollment.EnrolledAt ?? AtDays(-12);
            requests.Add(new PaymentRequest
            {
                Id = Guid.NewGuid(),
                StudentId = enrollment.StudentId,
                ParentId = parent.Id,
                ProgramId = program.Id,
                ProgramEnrollmentId = enrollment.Id,
                Amount = program.Price ?? 0m,
                Currency = "VND",
                Token = $"SEED-PENDING-PAYREQ-{requestIndex:D3}",
                ExpiresAt = _seedNow.AddDays(7),
                Status = PaymentRequestStatus.Pending,
                CreatedAt = createdAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            requestIndex++;
        }

        if (requests.Count == 0)
        {
            return;
        }

        await _unitOfWork.PaymentRequests.AddRangeAsync(requests);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed pending payment requests — {Count} request(s).",
            requests.Count);
    }
}
