namespace OboxSteam.Domain.Enums;

/// <summary>
/// Lifecycle for a discount code. Stored as text via EF string enum conversion.
/// Draft until <c>StartsAt</c>; Active once the start instant is reached.
/// </summary>
public enum VoucherStatus
{
    /// <summary>Issued but not yet usable; waiting for <c>StartsAt</c>.</summary>
    Draft = 0,

    /// <summary>Usable at checkout, subject to expiry and usage caps.</summary>
    Active = 1
}
