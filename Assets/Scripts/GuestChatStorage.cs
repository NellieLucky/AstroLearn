using System.IO;
using UnityEngine;

public static class GuestChatStorage
{
    private const string ChatSessionsKey = "guest.chat.sessions";
    private const string DebugFolderName = "GuestChatDebug";
    private const string SessionsFileName = "guest-chat-sessions.json";
    private const string ReadmeFileName = "README.txt";

    public static ChatSessionCollection LoadSessions()
    {
        if (!PlayerPrefs.HasKey(ChatSessionsKey))
        {
            return new ChatSessionCollection();
        }

        string json = PlayerPrefs.GetString(ChatSessionsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ChatSessionCollection();
        }

        ChatSessionCollection sessions = JsonUtility.FromJson<ChatSessionCollection>(json);
        return sessions ?? new ChatSessionCollection();
    }

    public static void SaveSessions(ChatSessionCollection sessions)
    {
        if (sessions == null)
        {
            return;
        }

        string json = JsonUtility.ToJson(sessions);
        PlayerPrefs.SetString(ChatSessionsKey, json);
        PlayerPrefs.Save();
        WriteDebugFile(SessionsFileName, JsonUtility.ToJson(sessions, true));
    }

    public static void ClearSessions()
    {
        PlayerPrefs.DeleteKey(ChatSessionsKey);
        PlayerPrefs.Save();
        DeleteDebugFile(SessionsFileName);
    }

    public static string GetDebugFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, DebugFolderName);
    }

    private static void WriteDebugFile(string fileName, string content)
    {
        string folderPath = GetDebugFolderPath();
        Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, fileName);
        File.WriteAllText(filePath, string.IsNullOrWhiteSpace(content) ? "{}" : content);
        WriteReadme(folderPath);
        Debug.Log($"[GuestChatStorage] Wrote debug chat data to: {filePath}");
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
            "Guest Chat Debug Files\r\n" +
            "----------------------\r\n" +
            $"{SessionsFileName} = saved local guest chatbot sessions and messages\r\n" +
            "\r\n" +
            "These files mirror PlayerPrefs data for easier debugging in development.\r\n";

        File.WriteAllText(readmePath, readmeContent);
    }
}
