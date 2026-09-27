using Godot;
using System;
using System.Text;

/// <summary>
/// Creates local, shareable beta-feedback reports without uploading player data.
/// </summary>
public static class FeedbackReportService
{
    private const string FeedbackDirectory = "user://feedback";
    public const string BetaVersion = "0.4.0";

    public static bool TryCreate(string playerMessage, out string absolutePath, out string errorMessage)
    {
        absolutePath = string.Empty;
        errorMessage = string.Empty;

        var directory = ProjectSettings.GlobalizePath(FeedbackDirectory);
        var directoryError = DirAccess.MakeDirRecursiveAbsolute(directory);
        if (directoryError != Error.Ok)
        {
            errorMessage = directoryError.ToString();
            return false;
        }

        var filename = $"feedback_{Time.GetDatetimeStringFromSystem().Replace(':', '-').Replace('T', '_')}.txt";
        var reportPath = $"{FeedbackDirectory}/{filename}";
        using var reportFile = FileAccess.Open(reportPath, FileAccess.ModeFlags.Write);
        if (reportFile == null)
        {
            errorMessage = FileAccess.GetOpenError().ToString();
            return false;
        }

        reportFile.StoreString(BuildReport(playerMessage));
        absolutePath = ProjectSettings.GlobalizePath(reportPath);
        return true;
    }

    internal static string BuildReport(string playerMessage)
    {
        var report = new StringBuilder();
        report.AppendLine("FORGOTTEN THREE KINGDOMS FEEDBACK REPORT");
        report.AppendLine($"Created (local): {Time.GetDatetimeStringFromSystem()}");
        report.AppendLine($"Build: {BetaVersion}");
        report.AppendLine($"Platform: {OS.GetName()}");
        report.AppendLine($"Language: {Localization.CurrentLanguage}");
        report.AppendLine();
        report.AppendLine("PLAYER REPORT");
        report.AppendLine(playerMessage.Trim());
        report.AppendLine();
        report.AppendLine("RUN SNAPSHOT");
        report.AppendLine($"Character: {GameManager.CurrentCharacterId}");
        report.AppendLine($"Chapter: {GameManager.CurrentChapter}");
        report.AppendLine($"Node: {GameManager.CurrentNodeId}");
        report.AppendLine($"Gold: {GameManager.Gold}");
        report.AppendLine($"Forage: {GameManager.Forage}");
        report.AppendLine($"Mana: {GameManager.CurrentMana:0.##}");
        report.AppendLine($"Route: {GameManager.CurrentChapterRoute}");
        report.AppendLine($"Variant: {GameManager.CurrentChapterVariant}");
        report.AppendLine();
        report.AppendLine("BATTLE LOG SNAPSHOT (JSON)");
        report.AppendLine(BattleLogRuntime.Service.ExportJson());
        return report.ToString();
    }
}
