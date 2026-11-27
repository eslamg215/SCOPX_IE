namespace SCOPX.Workstation;

/// <summary>Restricts workstation API endpoints to HTTPS, except local development loopback.</summary>
internal static class ApiEndpointPolicy
{
    public static bool IsAllowedBaseUri(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            return false;

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && uri.IsLoopback;
    }
}
