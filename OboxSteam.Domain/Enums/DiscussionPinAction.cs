namespace OboxSteam.Domain.Enums;

public enum DiscussionPinAction
{
    /// <summary>Manager/Admin: Open → Addressed.</summary>
    MarkAddressed,

    /// <summary>Advisor or board expert: Addressed/Resolved → Open.</summary>
    Reopen,

    /// <summary>Advisor or board expert: Open/Addressed → Resolved.</summary>
    Resolve,
}
