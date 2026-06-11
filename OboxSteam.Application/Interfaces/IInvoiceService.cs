using OboxSteam.Application.DTOs.InvoiceDTO;

namespace OboxSteam.Application.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceResponseDto> GetById(Guid invoiceId);

    Task<InvoiceResponseDto> GetByPaymentId(Guid paymentId);
   
    Task<List<InvoiceResponseDto>> GetMyInvoices();
}
