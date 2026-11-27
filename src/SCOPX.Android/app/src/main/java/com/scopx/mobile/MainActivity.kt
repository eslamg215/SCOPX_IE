package com.scopx.mobile

import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.Gravity
import android.view.View
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.*
import androidx.appcompat.app.AppCompatActivity
import androidx.webkit.WebViewAssetLoader
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.Executors

class MainActivity : AppCompatActivity() {
    private val executor = Executors.newSingleThreadExecutor()
    private val main = Handler(Looper.getMainLooper())
    private var token: String? = null
    private lateinit var status: TextView
    private lateinit var baseUrl: EditText
    private lateinit var email: EditText
    private lateinit var password: EditText
    private lateinit var webView: WebView
    private lateinit var assetLoader: WebViewAssetLoader
    private lateinit var loginPanel: View
    override fun onCreate(savedInstanceState: Bundle?) { super.onCreate(savedInstanceState); buildUi() }
    private fun buildUi() {
        val root = LinearLayout(this).apply { orientation=LinearLayout.VERTICAL; setPadding(32,28,32,28); setBackgroundColor(0xFF071426.toInt()) }
        val title=TextView(this).apply { text="SCOPX.I.E"; textSize=27f; setTextColor(0xFF00B8D9.toInt()) }
        val subtitle=TextView(this).apply { text="Mobile companion · Demo and API login"; setTextColor(0xFFB9C9D8.toInt()); setPadding(0,0,0,20) }
        val demo=Button(this).apply { text="Open Offline Demo App"; setOnClickListener { openDemo() } }
        baseUrl=EditText(this).apply { hint="API Base URL (HTTPS)"; setSingleLine() }; baseUrl.setText("https://10.0.2.2")
        email=EditText(this).apply { hint="Email"; setSingleLine(); inputType=33 }
        password=EditText(this).apply { hint="Password"; setSingleLine(); inputType=129 }
        val login=Button(this).apply { text="Sign in to API"; setOnClickListener { signIn() } }
        status=TextView(this).apply { text="Synthetic-data demo. Do not use with real patient data."; setTextColor(0xFFFFC857.toInt()); setPadding(0,18,0,0) }
        root.addView(title); root.addView(subtitle); root.addView(demo); root.addView(baseUrl); root.addView(email); root.addView(password); root.addView(login); root.addView(status)
        loginPanel=root
        assetLoader=WebViewAssetLoader.Builder().addPathHandler("/assets/",WebViewAssetLoader.AssetsPathHandler(this)).build()
        webView=WebView(this).apply {
            settings.javaScriptEnabled=true; settings.domStorageEnabled=true; settings.allowFileAccess=false; settings.allowContentAccess=false
            settings.javaScriptCanOpenWindowsAutomatically=false; settings.setSupportMultipleWindows(false)
            webViewClient=object:WebViewClient(){ override fun shouldInterceptRequest(view:WebView,request:android.webkit.WebResourceRequest):android.webkit.WebResourceResponse?=assetLoader.shouldInterceptRequest(request.url) }
            setBackgroundColor(0xFF06111D.toInt())
        }
        setContentView(root)
    }
    private fun openDemo(){ setContentView(webView); if(webView.url.isNullOrBlank()) webView.loadUrl("https://appassets.androidplatform.net/assets/scopx/index.html") }
    private fun signIn(){
        val base=baseUrl.text.toString().trim().trimEnd('/'); val userEmail=email.text.toString().trim(); val pass=password.text.toString()
        if(base.isBlank()||userEmail.isBlank()||pass.isBlank()){status.text="Enter server URL and credentials.";return}
        if (!ApiEndpointPolicy.isAllowedBaseUrl(base)) {
            status.text = "Use an HTTPS API base URL without credentials, path, query, or fragment."
            return
        }
        status.text="Connecting…"
        executor.execute { try {
            val body=JSONObject().put("email",userEmail).put("password",pass).toString()
            val response=request("$base/api/v1/auth/login","POST",body,null,base)
            val access=JSONObject(response).optString("access_token"); if(access.isBlank())throw IllegalStateException("No access token in response")
            token=access; val hospitals=request("$base/api/v1/hospitals/mine","GET",null,access,base)
            main.post{password.setText("");status.text="Signed in. Hospital memberships retrieved.\n$hospitals"}
        } catch(e:Exception){main.post{status.text="Connection/login failed. Check the HTTPS server URL and credentials."}} }
    }
    private fun request(endpoint:String,method:String,body:String?,bearer:String?,baseOrigin:String):String{
        if (!ApiEndpointPolicy.isAllowedRequestUrl(baseOrigin, endpoint))
            throw IllegalArgumentException("Request URL is outside the configured HTTPS API origin.")
        val conn=(URL(endpoint).openConnection() as HttpURLConnection)
        try {
            conn.instanceFollowRedirects = false
            conn.requestMethod=method
            conn.connectTimeout=10000
            conn.readTimeout=15000
            conn.setRequestProperty("Accept","application/json")
            if(bearer!=null)conn.setRequestProperty("Authorization","Bearer $bearer")
            if(body!=null){
                conn.doOutput=true
                conn.setRequestProperty("Content-Type","application/json")
                conn.outputStream.use{it.write(body.toByteArray(Charsets.UTF_8))}
            }
            val code=conn.responseCode
            if(code !in 200..299)throw IllegalStateException("HTTP $code")
            return conn.inputStream.bufferedReader().use{it.readText()}
        } finally {
            conn.disconnect()
        }
    }
    override fun onBackPressed(){ if(webView.parent!=null) setContentView(loginPanel) else super.onBackPressed() }
    override fun onDestroy(){executor.shutdownNow();token=null;webView.destroy();super.onDestroy()}
}
