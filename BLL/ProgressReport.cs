namespace BLL;

/// <summary>
/// A simple struct for reporting progress to the UI.
/// </summary>
public readonly struct ProgressReport
{
    /// <summary>
    /// The percentage complete (0-100).
    /// </summary>
    public int Percentage { get; }

    /// <summary>
    /// A message for the UI, e.g., "Compressing 'file.txt'..."
    /// </summary>
    public string Message { get; }

    public ProgressReport(int percentage, string message)
    {
        Percentage = percentage;
        Message = message;
    }
}