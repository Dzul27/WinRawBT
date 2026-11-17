namespace WinRawBT.Models;

/// <summary>Outcome of a pairing attempt (TASK-18).</summary>
public sealed class PairResult
{
    public PairResult(bool success, string detail, bool canceled = false)
    {
        Success = success;
        Detail = detail;
        Canceled = canceled;
    }

    /// <summary>True when the user canceled the attempt (not a pairing failure).</summary>
    public bool Canceled { get; }

    public bool Success { get; }

    /// <summary>Human-readable explanation (which PIN worked, or why every attempt failed).</summary>
    public string Detail { get; }
}
