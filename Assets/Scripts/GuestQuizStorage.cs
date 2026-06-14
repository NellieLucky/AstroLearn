using System.IO;
using UnityEngine;

public static class GuestQuizStorage
{
    private const string CurrentQuizKey = "guest.quiz.current";
    private const string QuizHistoryKey = "guest.quiz.history";
    private const string DebugFolderName = "GuestQuizDebug";
    private const string CurrentQuizFileName = "guest-quiz-current.json";
    private const string QuizHistoryFileName = "guest-quiz-history.json";
    private const string ReadmeFileName = "README.txt";

    public static void SaveCurrentSession(QuizSessionData session)
    {
        if (session == null)
        {
            return;
        }

        PlayerPrefs.SetString(CurrentQuizKey, JsonUtility.ToJson(session));
        PlayerPrefs.Save();
        WriteDebugFile(CurrentQuizFileName, JsonUtility.ToJson(session, true));
    }

    public static QuizSessionData LoadCurrentSession()
    {
        if (!PlayerPrefs.HasKey(CurrentQuizKey))
        {
            return null;
        }

        string json = PlayerPrefs.GetString(CurrentQuizKey, string.Empty);
        return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<QuizSessionData>(json);
    }

    public static void ClearCurrentSession()
    {
        PlayerPrefs.DeleteKey(CurrentQuizKey);
        PlayerPrefs.Save();
        DeleteDebugFile(CurrentQuizFileName);
    }

    public static QuizHistoryCollection LoadHistory()
    {
        if (!PlayerPrefs.HasKey(QuizHistoryKey))
        {
            return new QuizHistoryCollection();
        }

        string json = PlayerPrefs.GetString(QuizHistoryKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new QuizHistoryCollection();
        }

        QuizHistoryCollection history = JsonUtility.FromJson<QuizHistoryCollection>(json);
        return history ?? new QuizHistoryCollection();
    }

    public static void AppendHistory(QuizHistoryEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        QuizHistoryCollection history = LoadHistory();
        history.entries.Add(entry);
        PlayerPrefs.SetString(QuizHistoryKey, JsonUtility.ToJson(history));
        PlayerPrefs.Save();
        WriteDebugFile(QuizHistoryFileName, JsonUtility.ToJson(history, true));
    }

    public static string GetDebugFolderPath()
    {
        string basePath = Application.persistentDataPath;
        return Path.Combine(basePath, DebugFolderName);
    }

    private static void WriteDebugFile(string fileName, string content)
    {
        string folderPath = GetDebugFolderPath();
        Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, fileName);
        File.WriteAllText(filePath, string.IsNullOrWhiteSpace(content) ? "{}" : content);
        WriteReadme(folderPath);

        Debug.Log($"[GuestQuizStorage] Wrote debug quiz data to: {filePath}");
    }

    private static void DeleteDebugFile(string fileName)
    {
        string filePath = Path.Combine(GetDebugFolderPath(), fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static void WriteReadme(string folderPath)
    {
        string readmePath = Path.Combine(folderPath, ReadmeFileName);
        string readmeContent =
            "Guest Quiz Debug Files\r\n" +
            "----------------------\r\n" +
            $"{CurrentQuizFileName} = current active guest quiz session\r\n" +
            $"{QuizHistoryFileName} = saved guest quiz history\r\n" +
            "\r\n" +
            "These files are mirrors of PlayerPrefs data written for easier inspection during development.\r\n";

        File.WriteAllText(readmePath, readmeContent);
    }
}
