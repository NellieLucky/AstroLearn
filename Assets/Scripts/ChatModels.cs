using System;
using System.Collections.Generic;

[Serializable]
public class ChatMessageData
{
    public string role;
    public string text;
    public string timestampUtc;
}

[Serializable]
public class ChatSessionData
{
    public string sessionId;
    public string title;
    public string createdAtUtc;
    public string updatedAtUtc;
    public string preferredLanguage = "English";
    public List<ChatMessageData> messages = new List<ChatMessageData>();
}

[Serializable]
public class ChatSessionCollection
{
    public string activeSessionId;
    public List<ChatSessionData> sessions = new List<ChatSessionData>();
}

[Serializable]
public class ChatRequestPayload
{
    public string message;
    public string sessionId;
    public List<ChatMessagePayload> history = new List<ChatMessagePayload>();
}

[Serializable]
public class ChatMessagePayload
{
    public string role;
    public string text;
}

[Serializable]
public class ChatResponsePayload
{
    public string reply;
    public string error;
    public string details;
    public int status;
    public string statusText;
    public string model;
}

[Serializable]
public class OllamaGenerateRequestPayload
{
    public string model;
    public string prompt;
    public string format;
    public bool stream;
    public string keep_alive;
    public OllamaOptionsPayload options;
}

[Serializable]
public class OllamaGenerateResponsePayload
{
    public string response;
    public string error;
}

[Serializable]
public class OllamaOptionsPayload
{
    public int num_predict;
    public float temperature;
}

[Serializable]
public class ChatScopeGlossaryData
{
    public List<string> keywords = new List<string>();
}
