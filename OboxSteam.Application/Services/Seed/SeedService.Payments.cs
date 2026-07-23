using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedPaymentsAsync()
    {
        _loggerService.LogInformation("Starting seed payments");
        var existingPayments = await _unitOfWork.Payments.GetAllAsync();
        if (existingPayments.Any())
        {
            _loggerService.LogInformation("Payments already exist, skipping seeding");
            await SeedPendingPaymentRequestsAsync();
            return;
        }

        var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
        var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
        var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
        var student4 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-004");
        if (student1 == null)
        {
            _loggerService.LogWarning("STD-001 not found. Skipping payment seeding.");
            return;
        }

        var enrollments = (await _unitOfWork.ProgramEnrollments.GetAllAsync(pe => !pe.IsDeleted)).ToList();
        if (enrollments.Count == 0)
        {
            _loggerService.LogWarning("No program enrollments found. Skipping payment seeding.");
            return;
        }

        var now = DateTime.UtcNow;
        Payment PickEnrollmentPayment(
            string code,
            User student,
            ProgramEnrollment? enrollment,
            decimal amount,
            PaymentGateway gateway,
            PaymentStatus status,
            int? paidDaysAgo,
            string? txnId)
        {
            return new Payment
            {
                Id = Guid.NewGuid(),
                Code = code,
                StudentId = student.Id,
                PaidById = student.Id,
                ProgramEnrollmentId = enrollment?.Id,
                ModuleEnrollmentId = null,
                Amount = amount,
                Gateway = gateway,
                TransactionId = txnId,
                Status = status,
                PaidAt = paidDaysAgo.HasValue ? now.AddDays(-paidDaysAgo.Value) : null,
                CreatedAt = now.AddDays(-(paidDaysAgo ?? 1)),
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };
        }

        var pe1 = enrollments.FirstOrDefault(e => e.StudentId == student1.Id) ?? enrollments[0];
        var pe2 = student2 == null
            ? pe1
            : enrollments.FirstOrDefault(e => e.StudentId == student2.Id) ?? pe1;
        var pe3 = student3 == null
            ? pe1
            : enrollments.FirstOrDefault(e => e.StudentId == student3.Id) ?? pe1;
        var pe4 = student4 == null
            ? pe1
            : enrollments.FirstOrDefault(e => e.StudentId == student4.Id) ?? pe1;

        var payments = new List<Payment>
        {
            PickEnrollmentPayment("INV-26001", student1, pe1, 1_200_000m, PaymentGateway.Stripe, PaymentStatus.Success, 14, "STRIPE-TXN-001"),
            PickEnrollmentPayment("INV-26002", student1, pe1, 850_000m, PaymentGateway.VnPay, PaymentStatus.Success, 45, "VNPAY-TXN-002"),
            PickEnrollmentPayment("INV-26003", student2 ?? student1, pe2, 990_000m, PaymentGateway.Stripe, PaymentStatus.Success, 7, "STRIPE-TXN-003"),
            PickEnrollmentPayment("INV-26004", student2 ?? student1, pe2, 750_000m, PaymentGateway.BankTransfer, PaymentStatus.Success, 120, "BANK-TXN-004"),
            PickEnrollmentPayment("INV-26005", student3 ?? student1, pe3, 1_050_000m, PaymentGateway.VnPay, PaymentStatus.Success, 200, "VNPAY-TXN-005"),
            PickEnrollmentPayment("INV-26006", student3 ?? student1, pe3, 500_000m, PaymentGateway.Stripe, PaymentStatus.Failed, 3, "STRIPE-TXN-FAIL-006"),
            PickEnrollmentPayment("INV-26007", student4 ?? student1, pe4, 300_000m, PaymentGateway.VnPay, PaymentStatus.Refunded, 20, "VNPAY-TXN-REF-007"),
            PickEnrollmentPayment("INV-26008", student4 ?? student1, pe4, 1_100_000m, PaymentGateway.BankTransfer, PaymentStatus.Pending, null, null),
            PickEnrollmentPayment("INV-26009", student1, pe1, 680_000m, PaymentGateway.Stripe, PaymentStatus.Success, 2, "STRIPE-TXN-009"),
            PickEnrollmentPayment("INV-26010", student2 ?? student1, pe2, 720_000m, PaymentGateway.VnPay, PaymentStatus.Success, 60, "VNPAY-TXN-010"),
        };

        await _unitOfWork.Payments.AddRangeAsync(payments);
        await _unitOfWork.SaveChangesAsync();

        var invoices = payments
            .Where(p => p.Status == PaymentStatus.Success)
            .Select((p, index) => new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV-SEED-{index + 1:D3}",
                PaymentId = p.Id,
                IssuedToId = p.StudentId,
                BillingName = "Seed Student",
                BillingEmail = "seed-student@oboxsteam.com",
                ItemDescription = "Program enrollment fee",
                SubTotal = p.Amount,
                TotalAmount = p.Amount,
                Currency = "VND",
                CreatedAt = p.PaidAt ?? now,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            })
            .ToList();

        if (invoices.Count > 0)
        {
            await _unitOfWork.Invoices.AddRangeAsync(invoices);
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

        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Role == RoleType.Parent && !u.IsDeleted);
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
        var enrollment = student == null || program == null
            ? null
            : await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted);

        if (student == null || parent == null || program == null)
        {
            _loggerService.LogWarning("Missing student/parent/program for pending payment request seed.");
            return;
        }

        var now = DateTime.UtcNow;
        await _unitOfWork.PaymentRequests.AddAsync(new PaymentRequest
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ParentId = parent.Id,
            ProgramId = program.Id,
            ProgramEnrollmentId = enrollment?.Id,
            Amount = 1_250_000m,
            Currency = "VND",
            Token = "SEED-PENDING-PAYREQ-001",
            ExpiresAt = now.AddDays(7),
            Status = PaymentRequestStatus.Pending,
            CreatedAt = now,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        });

        await _unitOfWork.PaymentRequests.AddAsync(new PaymentRequest
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ParentId = parent.Id,
            ProgramId = program.Id,
            ProgramEnrollmentId = enrollment?.Id,
            Amount = 450_000m,
            Currency = "VND",
            Token = "SEED-PENDING-PAYREQ-002",
            ExpiresAt = now.AddDays(3),
            Status = PaymentRequestStatus.Pending,
            CreatedAt = now,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        });

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed pending payment requests.");
    }
}
