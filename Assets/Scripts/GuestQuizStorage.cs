using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class GuestQuizStorage
{
    private const string CurrentQuizKey = "guest.quiz.current";
    private const string QuizHistoryKey = "guest.quiz.history";
    private const string QuizTemplateCacheKey = "guest.quiz.templates";
    private const string DebugFolderName = "GuestQuizDebug";
    private const string CurrentQuizFileName = "guest-quiz-current.json";
    private const string QuizHistoryFileName = "guest-quiz-history.json";
    private const string QuizTemplateCacheFileName = "guest-quiz-templates.json";
    private const string ReadmeFileName = "README.txt";
    private const int MaxTemplateCacheEntries = 12;

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

    public static QuizSessionData LoadCachedTemplateSession(string topic, string difficulty)
    {
        QuizTemplateCacheCollection cache = LoadTemplateCache();
        if (cache == null || cache.entries == null || cache.entries.Count == 0)
        {
            return null;
        }

        string normalizedTopic = NormalizeKey(topic);
        string normalizedDifficulty = NormalizeKey(difficulty);
        for (int i = 0; i < cache.entries.Count; i++)
        {
            QuizTemplateCacheEntry entry = cache.entries[i];
            if (entry == null ||
                NormalizeKey(entry.topic) != normalizedTopic ||
                NormalizeKey(entry.difficulty) != normalizedDifficulty ||
                entry.questions == null ||
                entry.questions.Count == 0)
            {
                continue;
            }

            return new QuizSessionData
            {
                sessionId = Guid.NewGuid().ToString("N"),
                topic = string.IsNullOrWhiteSpace(entry.topic) ? topic : entry.topic,
                difficulty = string.IsNullOrWhiteSpace(entry.difficulty) ? difficulty : entry.difficulty,
                score = 0,
                currentQuestionIndex = 0,
                isComplete = false,
                createdAtUtc = DateTime.UtcNow.ToString("o"),
                completedAtUtc = string.Empty,
                selectedAnswers = BuildBlankSelectedAnswers(entry.questions.Count),
                questions = CloneQuestions(entry.questions)
            };
        }

        return null;
    }

    public static void SaveGeneratedTemplate(string topic, string difficulty, List<QuizQuestionData> questions)
    {
        if (string.IsNullOrWhiteSpace(topic) ||
            string.IsNullOrWhiteSpace(difficulty) ||
            questions == null ||
            questions.Count == 0)
        {
            return;
        }

        QuizTemplateCacheCollection cache = LoadTemplateCache();
        if (cache.entries == null)
        {
            cache.entries = new List<QuizTemplateCacheEntry>();
        }

        cache.entries.RemoveAll(entry =>
            entry != null &&
            string.Equals(entry.topic, topic, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.difficulty, difficulty, StringComparison.OrdinalIgnoreCase));

        cache.entries.Insert(0, new QuizTemplateCacheEntry
        {
            topic = topic.Trim(),
            difficulty = difficulty.Trim(),
            cachedAtUtc = DateTime.UtcNow.ToString("o"),
            questions = CloneQuestions(questions)
        });

        while (cache.entries.Count > MaxTemplateCacheEntries)
        {
            cache.entries.RemoveAt(cache.entries.Count - 1);
        }

        PlayerPrefs.SetString(QuizTemplateCacheKey, JsonUtility.ToJson(cache));
        PlayerPrefs.Save();
        WriteDebugFile(QuizTemplateCacheFileName, JsonUtility.ToJson(cache, true));
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
            $"{QuizTemplateCacheFileName} = cached generated quiz templates by topic and difficulty\r\n" +
            "\r\n" +
            "These files are mirrors of PlayerPrefs data written for easier inspection during development.\r\n";

        File.WriteAllText(readmePath, readmeContent);
    }

    private static QuizTemplateCacheCollection LoadTemplateCache()
    {
        if (!PlayerPrefs.HasKey(QuizTemplateCacheKey))
        {
            return new QuizTemplateCacheCollection();
        }

        string json = PlayerPrefs.GetString(QuizTemplateCacheKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new QuizTemplateCacheCollection();
        }

        QuizTemplateCacheCollection cache = JsonUtility.FromJson<QuizTemplateCacheCollection>(json);
        return cache ?? new QuizTemplateCacheCollection();
    }

    private static List<QuizQuestionData> CloneQuestions(List<QuizQuestionData> questions)
    {
        List<QuizQuestionData> clones = new List<QuizQuestionData>();
        if (questions == null)
        {
            return clones;
        }

        for (int i = 0; i < questions.Count; i++)
        {
            QuizQuestionData question = questions[i];
            if (question == null)
            {
                continue;
            }

            clones.Add(new QuizQuestionData
            {
                question = question.question,
                choices = question.choices != null ? new List<string>(question.choices) : new List<string>(),
                correctAnswer = question.correctAnswer,
                explanation = question.explanation
            });
        }

        return clones;
    }

    private static List<string> BuildBlankSelectedAnswers(int count)
    {
        List<string> answers = new List<string>();
        for (int i = 0; i < count; i++)
        {
            answers.Add(string.Empty);
        }

        return answers;
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
