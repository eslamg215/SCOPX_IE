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
}
