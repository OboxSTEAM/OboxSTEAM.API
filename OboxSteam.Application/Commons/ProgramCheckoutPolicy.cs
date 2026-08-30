namespace OboxSteam.Application.Commons;

/// <summary>Shared checkout window for program tuition seat holds and parent payment tokens.</summary>
public static class ProgramCheckoutPolicy
{
    /// <summary>Soft hold while the student is picking a class or a parent has not opened checkout yet.</summary>
    public const int CheckoutWindowMinutes = 5;

    /// <summary>
    /// Seat hold after a Stripe Checkout session exists. Matches Stripe's default session expiry (24 hours).
    /// </summary>
    public const int StripeCheckoutHoldMinutes = 24 * 60;
}
