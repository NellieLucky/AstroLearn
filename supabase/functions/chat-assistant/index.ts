const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

type ChatMessagePayload = {
  role?: string;
  text?: string;
};

type ChatRequestPayload = {
  message?: string;
  sessionId?: string;
  history?: ChatMessagePayload[];
};

const SPACE_KEYWORDS = [
  "space",
  "planet",
  "planets",
  "solar system",
  "sun",
  "moon",
  "moons",
  "earth",
  "mars",
  "jupiter",
  "saturn",
  "venus",
  "mercury",
  "uranus",
  "neptune",
  "pluto",
  "comet",
  "asteroid",
  "asteroids",
  "asteroit",
  "phobos",
  "deimos",
  "titan",
  "triton",
  "io",
  "europa",
  "ganymede",
  "callisto",
  "charon",
  "enceladus",
  "mimas",
  "kuiper belt",
  "oort cloud",
  "dwarf planet",
  "galaxy",
  "galaxies",
  "star",
  "stars",
  "nebula",
  "black hole",
  "orbit",
  "astronomy",
  "cosmos",
  "universe",
  "meteor",
  "meteorite",
  "eclipse",
  "constellation",
  "rocket",
  "telescope",
  "kepler",
  "exoplanet",
  "nasa",
  "esa",
  "observatory",
  "milky way",
  "andromeda",
  "cosmic",
  "absolute zero",
  "gravity",
  "vacuum",
  "light year",
];

const BLOCKED_TERMS = [
  "sex",
  "porn",
  "nude",
  "kill",
  "murder",
  "bomb",
  "drugs",
  "suicide",
  "hate",
  "racist",
];

const FRIENDLY_SHORT_PROMPTS = ["hi", "hello", "hey", "help", "what can you do"];

function isMessageInScope(message: string): boolean {
  const normalized = message.trim().toLowerCase();
  if (!normalized) {
    return false;
  }

  if (BLOCKED_TERMS.some((term) => normalized.includes(term))) {
    return false;
  }

  if (isLanguagePreferenceRequest(normalized)) {
    return true;
  }

  if (SPACE_KEYWORDS.some((keyword) => normalized.includes(keyword))) {
    return true;
  }

  const tokens = normalized.split(/[\s?!.,:;\-_/\\()[\]"]+/).filter(Boolean);
  for (const token of tokens) {
    for (const keyword of SPACE_KEYWORDS) {
      if (areCloseKeywordMatch(token, keyword)) {
        return true;
      }
    }
  }

  return FRIENDLY_SHORT_PROMPTS.some((prompt) => normalized.includes(prompt));
}

function buildOffTopicReply(language: string): string {
  if (isTagalogLanguage(language)) {
    return "Makakatulong lang ako sa mga paksang may kinalaman sa kalawakan tulad ng mga planeta, buwan, bituin, galaxy, Solar System, at astronomy. Magtanong ka ng bagay na sakop ng paksang iyon.";
  }

  return "I can only help with space-related topics like planets, moons, stars, galaxies, the Solar System, and astronomy. Please ask something within that scope.";
}

function stripCodeFences(raw: string): string {
  return raw
    .replace(/^```json\s*/i, "")
    .replace(/^```\s*/i, "")
    .replace(/\s*```$/i, "")
    .trim();
}

function normalizeReply(raw: string): string {
  return stripCodeFences(raw)
    .replace(/^\{\s*"reply"\s*:\s*"/i, "")
    .replace(/"\s*\}\s*$/i, "")
    .replace(/^Here is the JSON requested:\s*/i, "")
    .replace(/^Here is your JSON:\s*/i, "")
    .replace(/\\"/g, '"')
    .trim();
}

function stringIsNullOrWhiteSpace(value: string | null | undefined): boolean {
  return !value || value.trim().length === 0;
}

function isLanguagePreferenceRequest(normalized: string): boolean {
  if (!normalized) {
    return false;
  }

  const mentionsLanguage =
    normalized.includes("tagalog") ||
    normalized.includes("filipino") ||
    normalized.includes("english");

  const asksForSpeakingStyle =
    normalized.includes("speak") ||
    normalized.includes("talk") ||
    normalized.includes("answer") ||
    normalized.includes("reply") ||
    normalized.includes("use ") ||
    normalized.includes("can you") ||
    normalized.includes("pwede") ||
    normalized.includes("puwede") ||
    normalized.includes("mag") ||
    normalized.includes("gamit");

  return mentionsLanguage && asksForSpeakingStyle;
}

function detectPreferredLanguage(history: ChatMessagePayload[] | undefined, latestMessage: string): string {
  const normalizedLatest = latestMessage.trim().toLowerCase();
  if (normalizedLatest.includes("tagalog") || normalizedLatest.includes("filipino")) {
    return "Tagalog";
  }

  if (normalizedLatest.includes("english")) {
    return "English";
  }

  const entries = Array.isArray(history) ? history : [];
  for (let i = entries.length - 1; i >= 0; i--) {
    const text = (entries[i]?.text || "").toLowerCase();
    if (text.includes("sasagot ako sa tagalog") || text.includes("sa tagalog")) {
      return "Tagalog";
    }

    if (text.includes("answering in english") || text.includes("in english")) {
      return "English";
    }
  }

  return "English";
}

function buildLanguagePreferenceReply(language: string): string {
  if (isTagalogLanguage(language)) {
    return "Sige, sasagot ako sa Tagalog hangga't kaya at mananatili ako sa mga paksang may kinalaman sa kalawakan.";
  }

  return "Sure, I will continue answering in English and stay focused on space-related topics.";
}

function isTagalogLanguage(language: string): boolean {
  const normalized = (language || "").toLowerCase();
  return normalized.includes("tagalog") || normalized.includes("filipino");
}

function areCloseKeywordMatch(token: string, keyword: string): boolean {
  const normalizedToken = token.trim().toLowerCase();
  const normalizedKeyword = keyword.trim().toLowerCase();

  if (!normalizedToken || !normalizedKeyword || normalizedKeyword.includes(" ")) {
    return false;
  }

  if (normalizedToken === normalizedKeyword) {
    return true;
  }

  if (normalizedToken.length < 4 || normalizedKeyword.length < 4) {
    return false;
  }

  if (Math.abs(normalizedToken.length - normalizedKeyword.length) > 1) {
    return false;
  }

  return computeEditDistance(normalizedToken, normalizedKeyword) <= 1;
}

function computeEditDistance(a: string, b: string): number {
  const dp: number[][] = Array.from({ length: a.length + 1 }, () => Array(b.length + 1).fill(0));

  for (let i = 0; i <= a.length; i++) dp[i][0] = i;
  for (let j = 0; j <= b.length; j++) dp[0][j] = j;

  for (let i = 1; i <= a.length; i++) {
    for (let j = 1; j <= b.length; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      dp[i][j] = Math.min(
        dp[i - 1][j] + 1,
        dp[i][j - 1] + 1,
        dp[i - 1][j - 1] + cost,
      );
    }
  }

  return dp[a.length][b.length];
}

Deno.serve(async (request) => {
  if (request.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  if (request.method !== "POST") {
    return Response.json({ error: "Method not allowed" }, { status: 405, headers: corsHeaders });
  }

  const apiKey = Deno.env.get("GEMINI_API_KEY");
  const model = Deno.env.get("GEMINI_MODEL") || "gemini-3.5-flash";

  if (!apiKey) {
    return Response.json({ error: "Missing GEMINI_API_KEY secret" }, { status: 500, headers: corsHeaders });
  }

  let body: ChatRequestPayload;
  try {
    body = await request.json();
  } catch {
    return Response.json({ error: "Invalid JSON body" }, { status: 400, headers: corsHeaders });
  }

  const latestMessage = (body.message || "").trim();
  if (!latestMessage) {
    return Response.json({ error: "Message is required" }, { status: 400, headers: corsHeaders });
  }

  const preferredLanguage = detectPreferredLanguage(body.history, latestMessage);

  if (isLanguagePreferenceRequest(latestMessage.toLowerCase())) {
    const targetLanguage =
      latestMessage.toLowerCase().includes("tagalog") || latestMessage.toLowerCase().includes("filipino")
        ? "Tagalog"
        : "English";

    return Response.json({ reply: buildLanguagePreferenceReply(targetLanguage) }, { headers: corsHeaders });
  }

  if (!isMessageInScope(latestMessage)) {
    return Response.json({ reply: buildOffTopicReply(preferredLanguage) }, { headers: corsHeaders });
  }

  const history = Array.isArray(body.history) ? body.history.slice(-10) : [];
  const conversationText = history
    .map((entry) => {
      const role = (entry.role || "user").trim().toLowerCase();
      const text = (entry.text || "").trim();
      if (!text) {
        return "";
      }

      return `${role === "assistant" ? "Assistant" : "User"}: ${text}`;
    })
    .filter(Boolean)
    .join("\n");

  const prompt = [
    "You are AstroLearn AI Chatbot.",
    "You only answer astronomy and space-related questions.",
    "Scope includes the Solar System, planets, moons, stars, galaxies, black holes, comets, asteroids, dwarf planets, gravity, thermodynamics in space, and related space science.",
    "Treat small spelling mistakes in space-related words as valid when the intent is clear.",
    "If the user asks something off-topic, unsafe, or unnecessary for this scope, politely refuse and redirect them to a space-related question.",
    "Keep replies educational, clear, and suitable for Grade 7 to Grade 12 students.",
    "Prefer concise but helpful answers.",
    `Reply in ${preferredLanguage}.`,
    "Reply in plain text only.",
    "Do not return JSON.",
    "Do not use markdown code fences.",
    "",
    "Conversation history:",
    conversationText || "(no prior history)",
    "",
    `Latest user message: ${latestMessage}`,
  ].join("\n");

  const geminiResponse = await fetch(
    `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent?key=${apiKey}`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        contents: [
          {
            role: "user",
            parts: [{ text: prompt }],
          },
        ],
        generationConfig: {
          temperature: 0.6,
          topP: 0.9,
          maxOutputTokens: 300,
          responseMimeType: "text/plain",
        },
      }),
    },
  );

  if (!geminiResponse.ok) {
    const errorText = await geminiResponse.text();
    return Response.json(
      { error: "Gemini request failed", details: errorText },
      { status: 502, headers: corsHeaders },
    );
  }

  const rawGemini = await geminiResponse.json();
  const rawText =
    rawGemini?.candidates?.[0]?.content?.parts?.[0]?.text ||
    rawGemini?.candidates?.[0]?.content?.parts?.map((part: { text?: string }) => part?.text || "").join("") ||
    "";

  if (!rawText) {
    return Response.json({ error: "Gemini returned an empty response" }, { status: 502, headers: corsHeaders });
  }

  const normalizedReply = normalizeReply(rawText);
  if (normalizedReply) {
    try {
      const parsed = JSON.parse(normalizedReply);
      const reply = typeof parsed?.reply === "string" ? parsed.reply.trim() : "";
      if (!stringIsNullOrWhiteSpace(reply)) {
        return Response.json({ reply }, { headers: corsHeaders });
      }
    } catch {
      return Response.json({ reply: normalizedReply }, { headers: corsHeaders });
    }
  }

  return Response.json(
    {
      reply: isTagalogLanguage(preferredLanguage)
        ? "Makakatulong ako sa mga tanong tungkol sa kalawakan tulad ng mga planeta, bituin, galaxy, at Solar System. Subukan mong itanong muli nang mas malinaw."
        : "I can help with space-related questions about planets, stars, galaxies, and the Solar System. Please try asking your question again in a clear way.",
    },
    { headers: corsHeaders },
  );
});
