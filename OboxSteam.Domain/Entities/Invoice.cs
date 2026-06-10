using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class Invoice : BaseEntity
{
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = null!;

    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public Guid IssuedToId { get; set; }
    public User IssuedTo { get; set; } = null!;

    [MaxLength(255)]
    public string BillingName { get; set; } = null!;

    [MaxLength(255)]
    public string BillingEmail { get; set; } = null!;

    [MaxLength(255)]
    public string ItemDescription { get; set; } = null!;

    public decimal SubTotal { get; set; }

    public decimal TotalAmount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "VND";
}
