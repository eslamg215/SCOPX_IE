package com.scopx.mobile

import java.net.URI
import java.net.URISyntaxException

object DemoNavigationPolicy {
    private const val ASSET_HOST = "appassets.androidplatform.net"
    private const val ASSET_PREFIX = "/assets/"

    fun isAllowed(value: String): Boolean {
        if (value.isBlank() || value != value.trim()) return false
        return try {
            val uri = URI(value)
            val path = uri.rawPath ?: return false
            uri.isAbsolute
                && uri.scheme.equals("https", ignoreCase = true)
                && uri.host.equals(ASSET_HOST, ignoreCase = true)
                && uri.port == -1
                && uri.rawUserInfo == null
                && uri.rawQuery == null
                && uri.rawFragment == null
                && path.startsWith(ASSET_PREFIX)
                && !path.contains("/../")
                && !path.endsWith("/..")
        } catch (_: URISyntaxException) {
            false
        }
    }
}
