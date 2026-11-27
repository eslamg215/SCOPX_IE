package com.scopx.mobile

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ApiEndpointPolicyTest {
    @Test
    fun acceptsHttpsHostAndOptionalPort() {
        assertTrue(ApiEndpointPolicy.isAllowedBaseUrl("https://api.scopx.example"))
        assertTrue(ApiEndpointPolicy.isAllowedBaseUrl("https://api.scopx.example:8443"))
        assertTrue(ApiEndpointPolicy.isAllowedBaseUrl("https://api.scopx.example/"))
    }

    @Test
    fun rejectsCleartextAndEmbeddedCredentials() {
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl("http://api.scopx.example"))
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl("https://user:password@api.scopx.example"))
    }

    @Test
    fun rejectsPathQueryAndFragment() {
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl("https://api.scopx.example/v1"))
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl("https://api.scopx.example?debug=true"))
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl("https://api.scopx.example#fragment"))
    }

    @Test
    fun rejectsMalformedHostPortAndWhitespace() {
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl("https:///missing-host"))
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl("https://api.scopx.example:99999"))
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl(" https://api.scopx.example"))
        assertFalse(ApiEndpointPolicy.isAllowedBaseUrl("not a url"))
    }

    @Test
    fun requestMustStayOnTheConfiguredHttpsOrigin() {
        val base = "https://api.scopx.example:8443"
        assertTrue(ApiEndpointPolicy.isAllowedRequestUrl(base, "https://api.scopx.example:8443/api/v1/auth/login"))
        assertFalse(ApiEndpointPolicy.isAllowedRequestUrl(base, "https://other.scopx.example:8443/api/v1/auth/login"))
        assertFalse(ApiEndpointPolicy.isAllowedRequestUrl(base, "http://api.scopx.example:8443/api/v1/auth/login"))
        assertFalse(ApiEndpointPolicy.isAllowedRequestUrl(base, "https://user@api.scopx.example:8443/api/v1/auth/login"))
        assertFalse(ApiEndpointPolicy.isAllowedRequestUrl(base, "https://api.scopx.example:8443/api/v1/auth/login?x=1"))
        assertFalse(ApiEndpointPolicy.isAllowedRequestUrl(base, "https://api.scopx.example:8443/other/path"))
    }
}
