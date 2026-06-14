using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads key-value pairs from a .env file located at the project root.
/// Supports comments (#) and blank lines. Keys and values are trimmed.
/// </summary>
public static class EnvFileLoader
{
    private static Dictionary<string, string> cachedValues;

    /// <summary>
    /// Loads the .env file and returns the value for the given key,
    /// or defaultValue if the key is not found.
    /// </summary>
    public static string Get(string key, string defaultValue = "")
    {
        if (cachedValues == null)
        {
            Load();
        }

        if (cachedValues.TryGetValue(key, out string value))
        {
            return value;
        }

        return defaultValue;
    }

    /// <summary>
    /// Forces a reload of the .env file from disk.
    /// </summary>
    public static void Reload()
    {
        cachedValues = null;
        Load();
    }

    private static void Load()
    {
        cachedValues = new Dictionary<string, string>();

        // In the Unity Editor, Application.dataPath points to Assets/
        // so the project root is one level up.
        // In a build, we look next to the executable.
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string envPath = Path.Combine(projectRoot, ".env");

        if (!File.Exists(envPath))
        {
            Debug.LogWarning($"[EnvFileLoader] .env file not found at: {envPath}");
            return;
        }

        string[] lines = File.ReadAllLines(envPath);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
            {
                continue;
            }

            int equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            string key = line.Substring(0, equalsIndex).Trim();
            string value = line.Substring(equalsIndex + 1).Trim();

            // Remove surrounding quotes if present
            if (value.Length >= 2 &&
                ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                 (value.StartsWith("'") && value.EndsWith("'"))))
            {
                value = value.Substring(1, value.Length - 2);
            }

            if (!string.IsNullOrEmpty(key))
            {
                cachedValues[key] = value;
            }
        }

        Debug.Log($"[EnvFileLoader] Loaded {cachedValues.Count} variable(s) from .env");
    }
}
