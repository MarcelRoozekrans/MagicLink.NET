namespace MagicLink;

/// <summary>What became of a typed code.</summary>
internal enum CodeOutcome
{
    /// <summary>The code was right; the request is used up and the person may be signed in.</summary>
    SignedIn,

    /// <summary>The code was wrong; attempts are left.</summary>
    Wrong,

    /// <summary>The request was changed at the same moment; the code was not compared and the attempt not counted.</summary>
    TryAgain,

    /// <summary>The request is used, expired, or out of attempts.</summary>
    NoLongerValid,
}
