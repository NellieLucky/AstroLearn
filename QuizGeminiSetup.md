# AstroLearn Quiz Gemini Setup

## Unity Side

The quiz flow now expects these environment variables in your project `.env` file:

```env
GEMINI_QUIZ_ENDPOINT=https://your-project.functions.supabase.co/generate-quiz
GEMINI_QUIZ_BEARER_TOKEN=your_optional_edge_function_token
```

If `GEMINI_QUIZ_ENDPOINT` is empty, the app automatically uses local fallback questions so you can still test the full quiz flow offline.

## Expected Request Body

Unity sends:

```json
{
  "topic": "Jupiter",
  "difficulty": "Easy",
  "questionCount": 10
}
```

## Expected Response Body

Your backend should return JSON in this exact shape:

```json
{
  "topic": "Jupiter",
  "difficulty": "Easy",
  "questions": [
    {
      "question": "Which planet is known as the Red Planet?",
      "choices": ["Earth", "Mars", "Venus", "Jupiter"],
      "correctAnswer": "Mars",
      "explanation": "Mars appears red because of iron oxide on its surface."
    }
  ]
}
```

Rules:

- return exactly 10 questions
- each question must have exactly 4 choices
- `correctAnswer` must match one of the 4 choices
- return JSON only
- do not wrap the JSON in markdown

## Recommended Gemini Prompt

```text
You are an educational quiz generator for a Solar System learning application called AstroLearn.

Generate exactly 10 multiple-choice questions about {TOPIC}.

Requirements:
- Audience: Grade 7 to Grade 12 students
- Difficulty: {DIFFICULTY}
- Each question must have exactly 4 answer choices
- Only 1 correct answer
- Include a short educational explanation
- Avoid duplicate questions
- Use clear and scientifically accurate wording
- Return valid JSON only
- Do not include markdown, code fences, or extra text

JSON format:
{
  "topic": "",
  "difficulty": "",
  "questions": [
    {
      "question": "",
      "choices": ["", "", "", ""],
      "correctAnswer": "",
      "explanation": ""
    }
  ]
}
```

## Suggested Supabase Edge Function Flow

1. Receive `topic`, `difficulty`, and `questionCount` from Unity.
2. Validate the request body.
3. Call Gemini from the Edge Function, not from Unity.
4. Parse Gemini output.
5. Validate the JSON structure.
6. Return the cleaned JSON to Unity.

## Unity Behavior Already Implemented

- topic selection
- difficulty selection
- intro prompt
- start quiz generation
- fallback local quiz generation
- one-question-at-a-time flow
- answer recording
- score tracking
- timer-based auto-advance on timeout
- result page
- answer breakdown page
- guest offline save of current session
- guest offline save of quiz history
