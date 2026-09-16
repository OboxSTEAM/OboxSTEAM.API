using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedPaymentsAsync()
    {
        _loggerService.LogInformation("Starting seed payments from program enrollments");

        var enrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted,
            pe => pe.Student,
            pe => pe.Program);
        if (enrollments.Count == 0)
        {
            _loggerService.LogWarning("No program enrollments found. Skipping payment seeding.");
            await SeedPendingPaymentRequestsAsync();
            return;
        }

        var existingPayments = await _unitOfWork.Payments.GetAllAsync(p => !p.IsDeleted);
        var paidEnrollmentIds = existingPayments
            .Where(p => p.ProgramEnrollmentId.HasValue)
            .Select(p => p.ProgramEnrollmentId!.Value)
            .ToHashSet();
        var existingInvoices = await _unitOfWork.Invoices.GetAllAsync(i => !i.IsDeleted);
        var invoicePaymentIds = existingInvoices.Select(i => i.PaymentId).ToHashSet();

        var payments = new List<Payment>();
        var invoices = new List<Invoice>();
        var paymentIndex = existingPayments.Count + 1;
        var gateways = new[] { PaymentGateway.VnPay, PaymentGateway.Stripe, PaymentGateway.BankTransfer };

        foreach (var enrollment in enrollments)
        {
            if (enrollment.Status == EnrollmentStatus.PendingPayment
                || paidEnrollmentIds.Contains(enrollment.Id))
            {
                continue;
            }

            var student = enrollment.Student
                ?? await _unitOfWork.Users.GetByIdAsync(enrollment.StudentId);
            var program = enrollment.Program
                ?? await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId);
            if (student == null || program == null)
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
                Status = PaymentStatus.Success,
                PaidAt = paidAt,
                CreatedAt = paidAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            payments.Add(payment);
            paidEnrollmentIds.Add(enrollment.Id);

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
            invoicePaymentIds.Add(payment.Id);
            paymentIndex++;
        }

        // Backfill invoices for existing successful payments that never got one.
        foreach (var payment in existingPayments
                     .Where(p => p.Status == PaymentStatus.Success && !invoicePaymentIds.Contains(p.Id)))
        {
            var student = await _unitOfWork.Users.GetByIdAsync(payment.StudentId);
            if (student == null)
            {
                continue;
            }

            string itemDescription = "Program tuition";
            if (payment.ProgramEnrollmentId.HasValue)
            {
                var enrollment = enrollments.FirstOrDefault(e => e.Id == payment.ProgramEnrollmentId.Value)
                    ?? await _unitOfWork.ProgramEnrollments.GetByIdAsync(
                        payment.ProgramEnrollmentId.Value,
                        pe => pe.Program);
                if (enrollment?.Program != null)
                {
                    itemDescription = $"{enrollment.Program.Name} tuition";
                }
            }

            invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV-SEED-{paymentIndex:D3}",
                PaymentId = payment.Id,
                IssuedToId = student.Id,
                BillingName = student.FullName ?? student.Email,
                BillingEmail = student.Email,
                ItemDescription = itemDescription,
                SubTotal = payment.Amount,
                TotalAmount = payment.Amount,
                Currency = "VND",
                CreatedAt = payment.PaidAt ?? payment.CreatedAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
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
            "Finished seed payments — backfilled {PaymentCount} payment(s), {InvoiceCount} invoice(s).",
            payments.Count,
            invoices.Count);

        await SeedPendingPaymentRequestsAsync();
    }

    private async Task SeedPendingPaymentRequestsAsync()
    {
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

        var existingRequests = await _unitOfWork.PaymentRequests.GetAllAsync(
            pr => !pr.IsDeleted
                  && pr.ProgramId == program.Id
                  && pr.ParentId == parent.Id);
        var coveredEnrollmentIds = existingRequests
            .Where(pr => pr.ProgramEnrollmentId.HasValue)
            .Select(pr => pr.ProgramEnrollmentId!.Value)
            .ToHashSet();

        var requests = new List<PaymentRequest>();
        var requestIndex = existingRequests.Count + 1;
        foreach (var enrollment in pendingEnrollments.Where(pe => linkedStudentIds.Contains(pe.StudentId)))
        {
            if (coveredEnrollmentIds.Contains(enrollment.Id))
            {
                continue;
            }

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
            coveredEnrollmentIds.Add(enrollment.Id);
            requestIndex++;
        }

        // Refresh expiry on existing pending tokens so re-seed keeps them usable.
        var refreshed = 0;
        foreach (var request in existingRequests.Where(pr => pr.Status == PaymentRequestStatus.Pending))
        {
            if (request.ExpiresAt > _seedNow.AddDays(1))
            {
                continue;
            }

            request.ExpiresAt = _seedNow.AddDays(7);
            request.UpdatedAt = _seedNow;
            request.UpdatedBy = Guid.Empty;
            await _unitOfWork.PaymentRequests.Update(request);
            refreshed++;
        }

        if (requests.Count > 0)
        {
            await _unitOfWork.PaymentRequests.AddRangeAsync(requests);
        }

        if (requests.Count > 0 || refreshed > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed pending payment requests — added {Count}, refreshed {Refreshed}.",
            requests.Count,
            refreshed);
    }
}
