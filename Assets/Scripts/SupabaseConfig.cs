using UnityEngine;

[DisallowMultipleComponent]
public class SupabaseConfig : MonoBehaviour
{
    [SerializeField] private string supabaseUrl;
    [SerializeField] private string supabaseAnonKey;

    private string resolvedUrl;
    private string resolvedAnonKey;

    private void Awake()
    {
        ResolveValues();
    }

    /// <summary>
    /// Resolves the final values by checking the Inspector fields first,
    /// then falling back to the .env file if they are empty.
    /// </summary>
    private void ResolveValues()
    {
        // Inspector values take priority
        if (!string.IsNullOrWhiteSpace(supabaseUrl))
        {
            resolvedUrl = supabaseUrl.Trim().TrimEnd('/');
        }
        else
        {
            string envUrl = EnvFileLoader.Get("SUPABASE_URL");
            resolvedUrl = string.IsNullOrWhiteSpace(envUrl) ? string.Empty : envUrl.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(resolvedUrl))
            {
                Debug.Log("[SupabaseConfig] Loaded SUPABASE_URL from .env file.");
            }
        }

        if (!string.IsNullOrWhiteSpace(supabaseAnonKey))
        {
            resolvedAnonKey = supabaseAnonKey.Trim();
        }
        else
        {
            string envKey = EnvFileLoader.Get("SUPABASE_ANON_KEY");
            resolvedAnonKey = string.IsNullOrWhiteSpace(envKey) ? string.Empty : envKey.Trim();
            if (!string.IsNullOrWhiteSpace(resolvedAnonKey))
            {
                Debug.Log("[SupabaseConfig] Loaded SUPABASE_ANON_KEY from .env file.");
            }
        }
    }

    public string SupabaseUrl
    {
        get
        {
            if (resolvedUrl == null) ResolveValues();
            return resolvedUrl;
        }
    }

    public string SupabaseAnonKey
    {
        get
        {
            if (resolvedAnonKey == null) ResolveValues();
            return resolvedAnonKey;
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SupabaseUrl) && !string.IsNullOrWhiteSpace(SupabaseAnonKey);
}
