using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Assignment : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    /// <summary>Null if it belongs to the whole Module (not a specific course).</summary>
    public Guid? CourseId { get; set; }
    public Course? Course { get; set; }

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public AssignmentType AssignmentType { get; set; }

    public int MaxPoints { get; set; }

    /// <summary>Minimum grade required to pass this assignment.</summary>
    public decimal PassScore { get; set; }

    /// <summary>If true, this assignment must be passed for the module to be completed.</summary>
    public bool IsRequiredForModulePass { get; set; } = true;

    public DateTime? DueDate { get; set; }

    public bool AllowShuffle { get; set; } = true;

    // ── Question-Bank quiz configuration ──
    // When QuestionBankId is set, the quiz draws random questions from the linked bank
    // at serve-time. When null, questions are attached directly to this assignment
    // via the QuizQuestions collection (manual / non-bank mode).

    /// <summary>
    /// References the question bank this quiz pulls from.
    /// Null = questions are added directly to this assignment (no bank).
    /// </summary>
    public Guid? QuestionBankId { get; set; }
    public QuestionBank? QuestionBank { get; set; }

    /// <summary>
    /// Number of questions to randomly pick from the bank per attempt.
    /// Null = include every question in the bank.
    /// Ignored when QuestionBankId is null.
    /// </summary>
    public int? QuestionCount { get; set; }

    /// <summary>Randomize the order of options within each question when serving the quiz.</summary>
    public bool ShuffleOptions { get; set; } = true;

    /// <summary>
    /// Percentage of drawn questions that should come from difficulty level 1–2.
    /// EasyPercent + MediumPercent + HardPercent must equal 100.
    /// Ignored when QuestionBankId is null.
    /// </summary>
    public int EasyPercent { get; set; }

    /// <summary>
    /// Percentage of drawn questions that should come from difficulty level 3.
    /// </summary>
    public int MediumPercent { get; set; }

    /// <summary>
    /// Percentage of drawn questions that should come from difficulty level 4–5.
    /// </summary>
    public int HardPercent { get; set; }

    /// <summary>Time limit in minutes for each quiz attempt. Null = unlimited.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>Maximum number of times a student can retake this quiz.</summary>
    public int MaxAttempts { get; set; } = 1;

    // Navigation
    public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
