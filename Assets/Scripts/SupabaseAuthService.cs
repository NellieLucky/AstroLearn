using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SupabaseAuthService : MonoBehaviour
{
    private const string AccessTokenKey = "supabase.access_token";
    private const string RefreshTokenKey = "supabase.refresh_token";
    private const string UserEmailKey = "supabase.user_email";
    private const string PendingEmailKey = "supabase.pending_email";
    private const string GuestModeKey = "supabase.is_guest";
    private const string UserDisplayNameKey = "supabase.user_display_name";
    private const string UserIdKey = "supabase.user_id";

    private SupabaseConfig config;

    public static SupabaseAuthService Instance { get; private set; }

    public bool IsConfigured => config != null && config.IsConfigured;
    public bool HasStoredSession => !string.IsNullOrWhiteSpace(GetAccessToken()) || !string.IsNullOrWhiteSpace(GetRefreshToken());
    public bool IsGuestMode => PlayerPrefs.GetInt(GuestModeKey, 0) == 1;
    public string CurrentUserEmail => PlayerPrefs.GetString(UserEmailKey, string.Empty);
    public string PendingEmail => PlayerPrefs.GetString(PendingEmailKey, string.Empty);
    public string CurrentUserDisplayName => PlayerPrefs.GetString(UserDisplayNameKey, string.Empty);
    public string CurrentUserId => PlayerPrefs.GetString(UserIdKey, string.Empty);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<SupabaseAuthService>() != null)
        {
            return;
        }

        GameObject serviceObject = new GameObject("SupabaseAuthService");
        serviceObject.AddComponent<SupabaseAuthService>();
    }

    private void Awake()
    {
        SupabaseAuthService[] services = FindObjectsByType<SupabaseAuthService>(FindObjectsSortMode.None);
        if (services.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveConfig();
        DontDestroyOnLoad(gameObject);
    }

    public void ResolveConfig()
    {
        if (config != null && config.IsConfigured)
        {
            return;
        }

        config = FindFirstObjectByType<SupabaseConfig>();

        if (config == null)
        {
            GameObject configObject = FindObjectByName("BackendConfig");
            if (configObject != null)
            {
                config = configObject.GetComponent<SupabaseConfig>();
            }
        }
    }

    public IEnumerator SignUp(string email, string password, string displayName, Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        SignOut();
        SavePendingEmail(email);

        SignUpRequest payload = new SignUpRequest
        {
            email = email,
            password = password,
            data = new UserMetadata { display_name = displayName }
        };

        yield return SendRequest(
            "POST",
            $"{config.SupabaseUrl}/auth/v1/signup",
            JsonUtility.ToJson(payload),
            config.SupabaseAnonKey,
            request =>
            {
                if (!IsRequestSuccessful(request))
                {
                    callback?.Invoke(CreateFailureResult(request, "Unable to create account."));
                    return;
                }

                string responseText = request.downloadHandler.text;
                AuthResponse authResponse = ParseJson<AuthResponse>(responseText);

                string email = null;
                bool emailVerified = false;
                bool hasSession = false;
                bool isExistingUser = false;

                // Check if user is nested (when session is returned immediately)
                if (authResponse != null && authResponse.user != null && !string.IsNullOrWhiteSpace(authResponse.user.email))
                {
                    email = authResponse.user.email;
                    emailVerified = !string.IsNullOrWhiteSpace(authResponse.user.email_confirmed_at);
                    hasSession = !string.IsNullOrWhiteSpace(authResponse.access_token);

                    if (authResponse.user.identities == null || authResponse.user.identities.Length == 0)
                    {
                        isExistingUser = true;
                    }

                    if (hasSession && !isExistingUser)
                    {
                        string displayName = (authResponse.user != null && authResponse.user.user_metadata != null)
                            ? authResponse.user.user_metadata.display_name
                            : string.Empty;
                        string userId = (authResponse.user != null) ? authResponse.user.id : string.Empty;
                        SaveSession(authResponse.access_token, authResponse.refresh_token, email, displayName, userId);
                    }
                }
                else
                {
                    // Check if user is returned at root level (when email confirmation is required and no session is returned)
                    SupabaseUser rootUser = ParseJson<SupabaseUser>(responseText);
                    if (rootUser != null && !string.IsNullOrWhiteSpace(rootUser.email))
                    {
                        email = rootUser.email;
                        emailVerified = !string.IsNullOrWhiteSpace(rootUser.email_confirmed_at);
                        hasSession = false;

                        if (rootUser.identities == null || rootUser.identities.Length == 0)
                        {
                            isExistingUser = true;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    callback?.Invoke(CreateFailureResult(request, "Unable to create account."));
                    return;
                }

                if (isExistingUser)
                {
                    callback?.Invoke(new AuthOperationResult
                    {
                        Success = false,
                        Message = "User already registered."
                    });
                    return;
                }

                callback?.Invoke(new AuthOperationResult
                {
                    Success = true,
                    Email = email,
                    EmailVerified = emailVerified,
                    HasSession = hasSession,
                    Message = "Account created. Check your email to verify your account."
                });
            });
    }

    public IEnumerator SignIn(string email, string password, Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        SavePendingEmail(email);

        PasswordGrantRequest payload = new PasswordGrantRequest
        {
            email = email,
            password = password
        };

        yield return SendRequest(
            "POST",
            $"{config.SupabaseUrl}/auth/v1/token?grant_type=password",
            JsonUtility.ToJson(payload),
            config.SupabaseAnonKey,
            request =>
            {
                AuthResponse response = ParseJson<AuthResponse>(request.downloadHandler.text);
                bool hasUser = response != null && response.user != null && !string.IsNullOrWhiteSpace(response.user.email);
                if (!IsRequestSuccessful(request) || !hasUser || string.IsNullOrWhiteSpace(response.access_token))
                {
                    callback?.Invoke(CreateFailureResult(request, "Unable to log in. Check your credentials."));
                    return;
                }

                string displayName = (response.user != null && response.user.user_metadata != null)
                    ? response.user.user_metadata.display_name
                    : string.Empty;
                string userId = (response.user != null) ? response.user.id : string.Empty;
                SaveSession(response.access_token, response.refresh_token, response.user.email, displayName, userId);

                callback?.Invoke(new AuthOperationResult
                {
                    Success = true,
                    Email = response.user.email,
                    EmailVerified = !string.IsNullOrWhiteSpace(response.user.email_confirmed_at),
                    HasSession = true,
                    Message = !string.IsNullOrWhiteSpace(response.user.email_confirmed_at)
                        ? "Login successful."
                        : "Your email is not verified yet."
                });
            });
    }

    public IEnumerator SendPasswordReset(string email, Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        PasswordRecoveryRequest payload = new PasswordRecoveryRequest
        {
            email = email
        };

        yield return SendRequest(
            "POST",
            $"{config.SupabaseUrl}/auth/v1/recover",
            JsonUtility.ToJson(payload),
            config.SupabaseAnonKey,
            request =>
            {
                if (!IsRequestSuccessful(request))
                {
                    callback?.Invoke(CreateFailureResult(request, "Unable to send password reset email."));
                    return;
                }

                callback?.Invoke(new AuthOperationResult
                {
                    Success = true,
                    Email = email,
                    Message = "Password reset email sent. Check your inbox."
                });
            });
    }

    public IEnumerator ResendVerification(string email, Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        ResendVerificationRequest payload = new ResendVerificationRequest
        {
            type = "signup",
            email = email
        };

        yield return SendRequest(
            "POST",
            $"{config.SupabaseUrl}/auth/v1/resend",
            JsonUtility.ToJson(payload),
            config.SupabaseAnonKey,
            request =>
            {
                if (!IsRequestSuccessful(request))
                {
                    callback?.Invoke(CreateFailureResult(request, "Unable to resend verification email."));
                    return;
                }

                callback?.Invoke(new AuthOperationResult
                {
                    Success = true,
                    Email = email,
                    Message = "Verification email sent again."
                });
            });
    }

    public IEnumerator VerifyOTP(string email, string token, string type, Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        VerifyOTPRequest payload = new VerifyOTPRequest
        {
            email = email,
            token = token,
            type = type
        };

        yield return SendRequest(
            "POST",
            $"{config.SupabaseUrl}/auth/v1/verify",
            JsonUtility.ToJson(payload),
            config.SupabaseAnonKey,
            request =>
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[SupabaseAuthService] VerifyOTP response ({request.responseCode}): {responseText}");

                AuthResponse response = ParseJson<AuthResponse>(responseText);
                bool hasUser = response != null && response.user != null && !string.IsNullOrWhiteSpace(response.user.email);
                if (!IsRequestSuccessful(request) || !hasUser || string.IsNullOrWhiteSpace(response.access_token))
                {
                    callback?.Invoke(CreateFailureResult(request, "Invalid or expired verification code."));
                    return;
                }

                string displayName = (response.user != null && response.user.user_metadata != null)
                    ? response.user.user_metadata.display_name
                    : string.Empty;
                string userId = (response.user != null) ? response.user.id : string.Empty;
                SaveSession(response.access_token, response.refresh_token, response.user.email, displayName, userId);

                callback?.Invoke(new AuthOperationResult
                {
                    Success = true,
                    Email = response.user.email,
                    EmailVerified = !string.IsNullOrWhiteSpace(response.user.email_confirmed_at),
                    HasSession = true,
                    Message = "Verification successful."
                });
            });
    }

    public IEnumerator UpdatePassword(string newPassword, Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        string accessToken = GetAccessToken();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            callback?.Invoke(new AuthOperationResult
            {
                Success = false,
                Message = "No active session found. Please log in or verify your recovery code again."
            });
            yield break;
        }

        UpdateUserRequest payload = new UpdateUserRequest
        {
            password = newPassword
        };

        yield return SendRequest(
            "PUT",
            $"{config.SupabaseUrl}/auth/v1/user",
            JsonUtility.ToJson(payload),
            accessToken,
            request =>
            {
                if (!IsRequestSuccessful(request))
                {
                    callback?.Invoke(CreateFailureResult(request, "Unable to update password."));
                    return;
                }

                callback?.Invoke(new AuthOperationResult
                {
                    Success = true,
                    Message = "Password updated successfully."
                });
            });
    }

    public IEnumerator CheckVerification(Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        string accessToken = GetAccessToken();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            callback?.Invoke(new AuthOperationResult
            {
                Success = false,
                Message = "No active session found. Please log in after verifying your email."
            });
            yield break;
        }

        yield return GetCurrentUser(
            accessToken,
            request =>
            {
                UserResponse response = ParseJson<UserResponse>(request.downloadHandler.text);
                bool isVerified = response != null && !string.IsNullOrWhiteSpace(response.email_confirmed_at);

                callback?.Invoke(new AuthOperationResult
                {
                    Success = IsRequestSuccessful(request),
                    Email = response != null ? response.email : CurrentUserEmail,
                    EmailVerified = isVerified,
                    HasSession = true,
                    Message = isVerified
                        ? "Email verified."
                        : "Your email is not verified yet. Check your inbox and try again."
                });
            });
    }

    public IEnumerator TryRestoreSession(Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        if (IsGuestMode)
        {
            callback?.Invoke(new AuthOperationResult
            {
                Success = false,
                Message = "Guest mode is active."
            });
            yield break;
        }

        string accessToken = GetAccessToken();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            yield return GetCurrentUser(
                accessToken,
                request =>
                {
                    if (IsRequestSuccessful(request))
                    {
                        SupabaseUser response = ParseJson<SupabaseUser>(request.downloadHandler.text);
                        string displayName = (response != null && response.user_metadata != null)
                            ? response.user_metadata.display_name
                            : string.Empty;
                        string userId = (response != null) ? response.id : string.Empty;
                        SaveSession(accessToken, GetRefreshToken(), response != null ? response.email : CurrentUserEmail, displayName, userId);

                        callback?.Invoke(new AuthOperationResult
                        {
                            Success = true,
                            Email = response != null ? response.email : CurrentUserEmail,
                            EmailVerified = response != null && !string.IsNullOrWhiteSpace(response.email_confirmed_at),
                            HasSession = true,
                            Message = "Session restored."
                        });
                        return;
                    }

                    StartCoroutine(RefreshSession(callback));
                });
            yield break;
        }

        yield return RefreshSession(callback);
    }

    public IEnumerator UpdateDisplayName(string newDisplayName, Action<AuthOperationResult> callback)
    {
        ResolveConfig();
        if (!EnsureConfigured(callback))
        {
            yield break;
        }

        string accessToken = GetAccessToken();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            callback?.Invoke(new AuthOperationResult
            {
                Success = false,
                Message = "No active session found."
            });
            yield break;
        }

        string userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            Debug.Log("[SupabaseAuthService] userId is empty in PlayerPrefs, fetching current user details...");
            yield return GetCurrentUser(accessToken, request =>
            {
                if (IsRequestSuccessful(request))
                {
                    SupabaseUser user = ParseJson<SupabaseUser>(request.downloadHandler.text);
                    if (user != null && !string.IsNullOrWhiteSpace(user.id))
                    {
                        userId = user.id;
                        PlayerPrefs.SetString(UserIdKey, userId);
                        PlayerPrefs.Save();
                        Debug.Log($"[SupabaseAuthService] Dynamically resolved and saved userId: {userId}");
                    }
                }
                else
                {
                    Debug.LogWarning("[SupabaseAuthService] Failed to resolve userId dynamically: " + request.downloadHandler.text);
                }
            });
        }

        UpdateUserMetadataRequest metadataPayload = new UpdateUserMetadataRequest
        {
            data = new UserMetadata { display_name = newDisplayName }
        };

        bool authSuccess = false;
        string errorMessage = "Unable to update display name.";

        yield return SendRequest(
            "PUT",
            $"{config.SupabaseUrl}/auth/v1/user",
            JsonUtility.ToJson(metadataPayload),
            accessToken,
            request =>
            {
                if (IsRequestSuccessful(request))
                {
                    authSuccess = true;
                }
                else
                {
                    errorMessage = "Auth metadata update failed: " + request.downloadHandler.text;
                }
            });

        if (!authSuccess)
        {
            callback?.Invoke(new AuthOperationResult
            {
                Success = false,
                Message = errorMessage
            });
            yield break;
        }

        PlayerPrefs.SetString(UserDisplayNameKey, newDisplayName);
        PlayerPrefs.Save();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            ProfilesUpdateRequest profilesPayload = new ProfilesUpdateRequest
            {
                display_name = newDisplayName
            };

            yield return SendRequest(
                "PATCH",
                $"{config.SupabaseUrl}/rest/v1/profiles?id=eq.{userId}",
                JsonUtility.ToJson(profilesPayload),
                accessToken,
                request =>
                {
                    if (IsRequestSuccessful(request))
                    {
                        Debug.Log("[SupabaseAuthService] Profiles table display_name updated successfully.");
                    }
                    else
                    {
                        Debug.LogWarning("[SupabaseAuthService] Profiles table display_name update failed: " + request.downloadHandler.text);
                    }
                });
        }

        callback?.Invoke(new AuthOperationResult
        {
            Success = true,
            Message = "Display name updated successfully."
        });
    }

    public void SignOut()
    {
        PlayerPrefs.DeleteKey(AccessTokenKey);
        PlayerPrefs.DeleteKey(RefreshTokenKey);
        PlayerPrefs.DeleteKey(UserEmailKey);
        PlayerPrefs.DeleteKey(UserDisplayNameKey);
        PlayerPrefs.DeleteKey(UserIdKey);
        PlayerPrefs.Save();
        SetGuestMode(false);
    }

    public void SetGuestMode(bool isGuest)
    {
        PlayerPrefs.SetInt(GuestModeKey, isGuest ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SavePendingEmail(string email)
    {
        PlayerPrefs.SetString(PendingEmailKey, email ?? string.Empty);
        PlayerPrefs.Save();
    }

    private IEnumerator RefreshSession(Action<AuthOperationResult> callback)
    {
        string refreshToken = GetRefreshToken();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            callback?.Invoke(new AuthOperationResult
            {
                Success = false,
                Message = "No saved session found."
            });
            yield break;
        }

        RefreshTokenRequest payload = new RefreshTokenRequest
        {
            refresh_token = refreshToken
        };

        yield return SendRequest(
            "POST",
            $"{config.SupabaseUrl}/auth/v1/token?grant_type=refresh_token",
            JsonUtility.ToJson(payload),
            config.SupabaseAnonKey,
            request =>
            {
                AuthResponse response = ParseJson<AuthResponse>(request.downloadHandler.text);
                bool hasUser = response != null && response.user != null && !string.IsNullOrWhiteSpace(response.user.email);
                if (!IsRequestSuccessful(request) || !hasUser || string.IsNullOrWhiteSpace(response.access_token))
                {
                    SignOut();
                    callback?.Invoke(CreateFailureResult(request, "Stored session expired. Please log in again."));
                    return;
                }

                string displayName = (response.user != null && response.user.user_metadata != null)
                    ? response.user.user_metadata.display_name
                    : string.Empty;
                string userId = (response.user != null) ? response.user.id : string.Empty;
                SaveSession(response.access_token, response.refresh_token, response.user.email, displayName, userId);
                callback?.Invoke(new AuthOperationResult
                {
                    Success = true,
                    Email = response.user.email,
                    EmailVerified = !string.IsNullOrWhiteSpace(response.user.email_confirmed_at),
                    HasSession = true,
                    Message = "Session restored."
                });
            });
    }

    private IEnumerator GetCurrentUser(string accessToken, Action<UnityWebRequest> callback)
    {
        using (UnityWebRequest request = new UnityWebRequest($"{config.SupabaseUrl}/auth/v1/user", "GET"))
        {
            request.timeout = 20;
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplyBaseHeaders(request);
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            yield return request.SendWebRequest();
            callback?.Invoke(request);
        }
    }

    private IEnumerator SendRequest(string method, string url, string jsonBody, string bearerToken, Action<UnityWebRequest> callback)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.timeout = 20;
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrWhiteSpace(jsonBody))
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            }

            ApplyBaseHeaders(request);

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            }

            yield return request.SendWebRequest();
            callback?.Invoke(request);
        }
    }

    private void ApplyBaseHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("apikey", config.SupabaseAnonKey);
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json");
    }

    private bool EnsureConfigured(Action<AuthOperationResult> callback)
    {
        if (IsConfigured)
        {
            return true;
        }

        callback?.Invoke(new AuthOperationResult
        {
            Success = false,
            Message = "Supabase is not configured. Attach SupabaseConfig to BackendConfig and fill in the URL and anon key."
        });
        return false;
    }

    private void SaveSession(string accessToken, string refreshToken, string email, string displayName = "", string userId = "")
    {
        PlayerPrefs.SetString(AccessTokenKey, accessToken ?? string.Empty);
        PlayerPrefs.SetString(RefreshTokenKey, refreshToken ?? string.Empty);
        PlayerPrefs.SetString(UserEmailKey, email ?? string.Empty);
        if (!string.IsNullOrEmpty(displayName))
        {
            PlayerPrefs.SetString(UserDisplayNameKey, displayName);
        }
        if (!string.IsNullOrEmpty(userId))
        {
            PlayerPrefs.SetString(UserIdKey, userId);
        }
        PlayerPrefs.SetInt(GuestModeKey, 0);
        PlayerPrefs.Save();
    }

    private string GetAccessToken()
    {
        return PlayerPrefs.GetString(AccessTokenKey, string.Empty);
    }

    private string GetRefreshToken()
    {
        return PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
    }

    private static bool IsRequestSuccessful(UnityWebRequest request)
    {
        return request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
    }

    private static T ParseJson<T>(string json) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new T();
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch
        {
            return new T();
        }
    }

    private static AuthOperationResult CreateFailureResult(string json, string fallbackMessage)
    {
        ErrorResponse error = ParseJson<ErrorResponse>(json);
        string message =
            FirstNonEmpty(error.error_description, error.msg, error.message, fallbackMessage);

        return new AuthOperationResult
        {
            Success = false,
            Message = message
        };
    }

    private static AuthOperationResult CreateFailureResult(UnityWebRequest request, string fallbackMessage)
    {
        string payload = request != null && request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        ErrorResponse error = ParseJson<ErrorResponse>(payload);
        string message = FirstNonEmpty(
            error.error_description,
            error.msg,
            error.message,
            request != null ? request.error : string.Empty,
            fallbackMessage);

        return new AuthOperationResult
        {
            Success = false,
            Message = message
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static GameObject FindObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject rootObject in objects)
        {
            if (rootObject == null || rootObject.hideFlags != HideFlags.None)
            {
                continue;
            }

            if (rootObject.name == objectName)
            {
                return rootObject;
            }
        }

        return null;
    }

    [Serializable]
    public class AuthOperationResult
    {
        public bool Success;
        public bool EmailVerified;
        public bool HasSession;
        public string Email;
        public string Message;
    }

    [Serializable]
    private class SignUpRequest
    {
        public string email;
        public string password;
        public UserMetadata data;
    }

    [Serializable]
    private class PasswordGrantRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    private class PasswordRecoveryRequest
    {
        public string email;
    }

    [Serializable]
    private class ResendVerificationRequest
    {
        public string type;
        public string email;
    }

    [Serializable]
    private class VerifyOTPRequest
    {
        public string email;
        public string token;
        public string type;
    }

    [Serializable]
    private class UpdateUserRequest
    {
        public string password;
    }

    [Serializable]
    private class RefreshTokenRequest
    {
        public string refresh_token;
    }

    [Serializable]
    private class AuthResponse
    {
        public string access_token;
        public string refresh_token;
        public SupabaseUser user;
        public string msg;
        public string error_description;
    }

    [Serializable]
    private class UserResponse
    {
        public string id;
        public string email;
        public string email_confirmed_at;
    }

    [Serializable]
    private class Identity
    {
        public string id;
    }

    [Serializable]
    private class SupabaseUser
    {
        public string id;
        public string email;
        public string email_confirmed_at;
        public UserMetadata user_metadata;
        public Identity[] identities;
    }

    [Serializable]
    private class UserMetadata
    {
        public string display_name;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string msg;
        public string message;
        public string error_description;
    }

    [Serializable]
    private class UpdateUserMetadataRequest
    {
        public UserMetadata data;
    }

    [Serializable]
    private class ProfilesUpdateRequest
    {
        public string display_name;
    }
}
