# AstroLearn Chat Gemini Setup

## Unity `.env`

Add these variables to your project `.env`:

```env
GEMINI_CHAT_ENDPOINT=https://your-project-ref.supabase.co/functions/v1/chat-assistant
GEMINI_CHAT_BEARER_TOKEN=
```

Notes:

- `GEMINI_CHAT_ENDPOINT` is the Supabase Edge Function URL for the chatbot.
- `GEMINI_CHAT_BEARER_TOKEN` can stay blank for now if your function accepts anon requests.
- if `GEMINI_CHAT_ENDPOINT` is blank, AstroLearn will still work using the built-in local fallback replies and local guest chat history

## Supabase Edge Function Secrets

Add these in Supabase:

```text
GEMINI_API_KEY=your_google_ai_studio_key
GEMINI_MODEL=gemini-1.5-flash
```

## Included Function File

This project now includes a starter function here:

`supabase/functions/chat-assistant/index.ts`

It already does these:

- accepts chat requests from Unity
- keeps the bot focused on space and astronomy
- refuses off-topic or inappropriate prompts
- forwards in-scope questions to Gemini
- returns JSON in the shape `{ "reply": "..." }`

## Request Body From Unity

```json
{
  "message": "How many moons does Jupiter have?",
  "sessionId": "abc123",
  "history": [
    { "role": "user", "text": "Tell me about Jupiter." },
    { "role": "assistant", "text": "Jupiter is the largest planet..." }
  ]
}
```

## Response Body Expected By Unity

```json
{
  "reply": "Jupiter currently has many known moons, including the four large Galilean moons: Io, Europa, Ganymede, and Callisto."
}
```

## Deploy Flow

1. In Supabase, create a new function named `chat-assistant`.
2. Replace its default code with the contents of `supabase/functions/chat-assistant/index.ts`.
3. Save and deploy the function.
4. Copy the deployed function URL.
5. Put that URL into `GEMINI_CHAT_ENDPOINT` in your `.env`.
6. Reopen Play Mode in Unity and test `AskAIButton`.

## Current Chat Features Already Wired

- `AskAIButton` opens the chatbot UI
- `ExitButton` returns to the Solar System UI
- `New Chat` starts a fresh conversation
- chat history is saved locally for guest mode
- clicking a history item reloads that chat session
- off-topic prompts are rejected inside the app and inside the function
