package com.scopx.mobile

import java.net.URI
import java.net.URISyntaxException

object ApiEndpointPolicy {
    fun isAllowedBaseUrl(value: String): Boolean {
        if (value.isBlank() || value != value.trim()) return false
        return try {
            val uri = URI(value)
            val path = uri.rawPath
            uri.isAbsolute
                && uri.scheme.equals("https", ignoreCase = true)
                && !uri.host.isNullOrBlank()
                && uri.rawUserInfo == null
                && uri.rawQuery == null
                && uri.rawFragment == null
                && (path.isNullOrEmpty() || path == "/")
                && (uri.port == -1 || uri.port in 1..65535)
        } catch (_: URISyntaxException) {
            false
        }
    }

    fun isAllowedRequestUrl(baseUrl: String, requestUrl: String): Boolean {
        if (!isAllowedBaseUrl(baseUrl)) return false
        return try {
            val base = URI(baseUrl.trimEnd('/'))
            val request = URI(requestUrl)
            request.isAbsolute
                && request.scheme.equals("https", ignoreCase = true)
                && !request.host.isNullOrBlank()
                && request.host.equals(base.host, ignoreCase = true)
                && request.port == base.port
                && request.rawUserInfo == null
                && request.rawQuery == null
                && request.rawFragment == null
                && request.rawPath.startsWith("/api/v1/")
        } catch (_: URISyntaxException) {
            false
        }
    }
}
