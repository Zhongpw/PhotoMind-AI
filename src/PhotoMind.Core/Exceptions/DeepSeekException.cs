namespace PhotoMind.Core.Exceptions;

/// <summary>Classifies a DeepSeek API failure so the UI can show a friendly, specific message.</summary>
public enum DeepSeekErrorKind
{
    /// <summary>401/403 — invalid or expired API key.</summary>
    Authentication,

    /// <summary>429 or rate-limit body — too many requests.</summary>
    RateLimit,

    /// <summary>402 or balance/insufficient-funds body — account out of credit.</summary>
    InsufficientBalance,

    /// <summary>5xx — transient server error (retryable).</summary>
    Server,

    /// <summary>Timeout / connection failure / DNS — transient network error (retryable).</summary>
    Network,

    /// <summary>200 OK but unparseable or empty content.</summary>
    InvalidResponse,

    /// <summary>Any other failure.</summary>
    Request,
}

public sealed class DeepSeekException : PhotoMindException
{
    public DeepSeekErrorKind Kind { get; }
    public int? HttpStatusCode { get; }

    public DeepSeekException(DeepSeekErrorKind kind, string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner ?? new InvalidOperationException(message))
    {
        Kind = kind;
        HttpStatusCode = statusCode;
    }

    /// <summary>True for errors that are worth retrying (transient).</summary>
    public bool IsRetryable => Kind is DeepSeekErrorKind.Server or DeepSeekErrorKind.Network or DeepSeekErrorKind.RateLimit;
}
