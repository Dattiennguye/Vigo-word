using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Java.Interop;
using Vigo.Api;


namespace Vigo.Android
{
    [Activity(Label = "VIGO Empire", MainLauncher = true, Exported = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.UiMode)]
    public class MainActivity : Activity
    {
        internal const string Host = "https://vigo.local";
        private WebView _web;
        private GameApi _api;
        internal readonly ConcurrentDictionary<int, byte[]> Results = new ConcurrentDictionary<int, byte[]>();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            _api = new GameApi(Path.Combine(FilesDir.AbsolutePath, "saves"));
            _web = new WebView(this);
            _web.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#1d2a36"));
            var s = _web.Settings; s.JavaScriptEnabled = true; s.DomStorageEnabled = true; s.MediaPlaybackRequiresUserGesture = false;
            s.AllowFileAccess = false; s.AllowContentAccess = false; s.SetSupportZoom(false); s.BuiltInZoomControls = false;
            _web.OverScrollMode = OverScrollMode.Never; _web.HorizontalScrollBarEnabled = false; _web.VerticalScrollBarEnabled = false;
            _web.SetWebViewClient(new Client(this));
            _web.AddJavascriptInterface(new Bridge(this), "VigoNative");
            SetContentView(_web);
            HideSystemUi();
            _web.LoadUrl(Host + "/index.html");
        }

        private void HideSystemUi()
        {
#pragma warning disable CA1422
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(int)(SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutFullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.Fullscreen | SystemUiFlags.ImmersiveSticky);
#pragma warning restore CA1422
        }

        public override void OnWindowFocusChanged(bool hasFocus) { base.OnWindowFocusChanged(hasFocus); if (hasFocus) HideSystemUi(); }

        protected override void OnPause()
        {
            base.OnPause();
            try { _api.Handle("POST", "/api/save", "{\"name\":\"tudongluu\"}"); } catch { }
            _web?.OnPause();
        }
        protected override void OnResume() { base.OnResume(); _web?.OnResume(); }

#pragma warning disable CA1422
        public override void OnBackPressed()
        {
            _web.EvaluateJavascript("(window.__vigoBack ? window.__vigoBack() : false)", new BackCallback(this));
        }
#pragma warning restore CA1422

        private sealed class BackCallback : Java.Lang.Object, IValueCallback
        {
            private readonly MainActivity _a; public BackCallback(MainActivity a) { _a = a; }
            public void OnReceiveValue(Java.Lang.Object value) { if ((value?.ToString() ?? "false") != "true") _a.MoveTaskToBack(true); }
        }

        /// <summary>Cầu nối JS → C#: giao diện gửi lệnh, kết quả lấy lại qua /__r/{id} (tránh nhét JSON lớn vào chuỗi JS).</summary>
        private sealed class Bridge : Java.Lang.Object
        {
            private readonly MainActivity _a; public Bridge(MainActivity a) { _a = a; }

            [JavascriptInterface]
            [Export("call")]
            public void Call(int id, string method, string url, string body)
            {
                Task.Run(() =>
                {
                    (int status, string json) r;
                    try { r = _a._api.Handle(method, url, body); }
                    catch (Exception e) { r = (500, "{\"ok\":false,\"error\":" + System.Text.Json.JsonSerializer.Serialize(e.Message) + "}"); }
                    _a.Results[id] = System.Text.Encoding.UTF8.GetBytes(r.json);
                    _a.RunOnUiThread(() => _a._web.EvaluateJavascript("window.__vigoDone(" + id + "," + r.status + ")", null));
                });
            }
        }

        private sealed class Client : WebViewClient
        {
            private readonly MainActivity _a; public Client(MainActivity a) { _a = a; }

            private static string Mime(string p)
            {
                p = p.ToLowerInvariant();
                if (p.EndsWith(".html")) return "text/html"; if (p.EndsWith(".js")) return "application/javascript"; if (p.EndsWith(".css")) return "text/css";
                if (p.EndsWith(".woff2")) return "font/woff2"; if (p.EndsWith(".png")) return "image/png"; if (p.EndsWith(".svg")) return "image/svg+xml"; if (p.EndsWith(".json")) return "application/json";
                return "application/octet-stream";
            }

            public override WebResourceResponse ShouldInterceptRequest(WebView view, IWebResourceRequest request)
            {
                var uri = request.Url; if (uri == null || uri.Host != "vigo.local") return new WebResourceResponse("text/plain", "utf-8", 403, "Blocked", null, new MemoryStream());
                string path = uri.Path ?? "/"; if (path == "/") path = "/index.html";
                try
                {
                    if (path.StartsWith("/__r/"))
                    {
                        int id = int.Parse(path.Substring(5));
                        if (_a.Results.TryRemove(id, out var bytes)) return new WebResourceResponse("application/json", "utf-8", new MemoryStream(bytes));
                        return new WebResourceResponse("application/json", "utf-8", 404, "Not found", null, new MemoryStream());
                    }
                    var stream = _a.Assets.Open("wwwroot" + path);
                    return new WebResourceResponse(Mime(path), "utf-8", stream);
                }
                catch (Exception) { return new WebResourceResponse("text/plain", "utf-8", 404, "Not found", null, new MemoryStream()); }
            }
        }
    }
}
