using System;
using System.Collections.Generic;

[Serializable]
public class QuizQuestionData
{
    public string question;
    public List<string> choices = new List<string>();
    public string correctAnswer;
    public string explanation;
}

[Serializable]
public class QuizSessionData
{
    public string sessionId;
    public string topic;
    public string difficulty;
    public int score;
    public int currentQuestionIndex;
    public bool isComplete;
    public string createdAtUtc;
    public string completedAtUtc;
    public List<string> selectedAnswers = new List<string>();
    public List<QuizQuestionData> questions = new List<QuizQuestionData>();
}

[Serializable]
public class QuizHistoryEntry
{
    public string sessionId;
    public string topic;
    public string difficulty;
    public int score;
    public int totalQuestions;
    public string createdAtUtc;
    public string completedAtUtc;
    public List<string> selectedAnswers = new List<string>();
    public List<QuizQuestionData> questions = new List<QuizQuestionData>();
}

[Serializable]
public class QuizHistoryCollection
{
    public List<QuizHistoryEntry> entries = new List<QuizHistoryEntry>();
}

[Serializable]
public class QuizGenerationRequest
{
    public string topic;
    public string difficulty;
    public int questionCount;
}
