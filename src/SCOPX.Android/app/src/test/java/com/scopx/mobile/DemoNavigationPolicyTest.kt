package com.scopx.mobile

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DemoNavigationPolicyTest {
    @Test
    fun allowsBundledDemoAssets() {
        assertTrue(DemoNavigationPolicy.isAllowed("https://appassets.androidplatform.net/assets/scopx/index.html"))
        assertTrue(DemoNavigationPolicy.isAllowed("https://appassets.androidplatform.net/assets/scopx/styles.css"))
    }

    @Test
    fun rejectsExternalAndCleartextNavigation() {
        assertFalse(DemoNavigationPolicy.isAllowed("https://example.com/"))
        assertFalse(DemoNavigationPolicy.isAllowed("http://appassets.androidplatform.net/assets/scopx/index.html"))
    }

    @Test
    fun rejectsFileJavascriptAndCredentialUrls() {
        assertFalse(DemoNavigationPolicy.isAllowed("file:///android_asset/index.html"))
        assertFalse(DemoNavigationPolicy.isAllowed("javascript:alert(1)"))
        assertFalse(DemoNavigationPolicy.isAllowed("https://user@appassets.androidplatform.net/assets/scopx/index.html"))
        assertFalse(DemoNavigationPolicy.isAllowed("https:///assets/scopx/index.html"))
        assertFalse(DemoNavigationPolicy.isAllowed("https://appassets.androidplatform.net/assets/%2e%2e/secret"))
        assertFalse(DemoNavigationPolicy.isAllowed("https://appassets.androidplatform.net/assets\\\\..\\\\secret"))
    }

    @Test
    fun rejectsQueriesFragmentsAndTraversal() {
        assertFalse(DemoNavigationPolicy.isAllowed("https://appassets.androidplatform.net/assets/scopx/index.html?x=1"))
        assertFalse(DemoNavigationPolicy.isAllowed("https://appassets.androidplatform.net/assets/scopx/index.html#top"))
        assertFalse(DemoNavigationPolicy.isAllowed("https://appassets.androidplatform.net/assets/scopx/../secret"))
    }
}
