using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Web.WebView2.Core;
using Nexus.Application.Services;
using Nexus.Core;
using Nexus.Core.Abstractions;
using Nexus.Core.Entities;
using Nexus.UI.ViewModels;

namespace Nexus.UI.Views;

public partial class ShellWindow : Window
{
    private static readonly System.Net.Http.HttpClient _http = new();
    private readonly AiOrchestrator _ai;
    private readonly IBookmarkRepository _bookmarks;
    private readonly IHistoryRepository _history;
    private readonly TabManager _tabs;
    private string _homeUri = "";
    private bool _forceNewTab;
    private readonly Dictionary<string, CoreWebView2DownloadOperation> _downloads = new();
    private readonly Dictionary<string, DateTimeOffset> _dlStart = new();
    private System.Windows.Rect _restore;
    private bool _maxed;
    private bool _vaultOn;
    private int _blocked;
    private bool _openConstAfterLoad;
    private bool _switchingTab;
    private bool _gesturesOn;

    // ===== DNS SECURISE (DoH) =====
    // Template DoH applique au demarrage. Cloudflare par defaut ; alternatives :
    // Google : https://dns.google/dns-query
    // Quad9 : https://dns.quad9.net/dns-query
    private const string DohTemplate = "https://cloudflare-dns.com/dns-query";
    private static bool _dohApplied;

    // I18N : langue courante de l'interface. Pilotee par la page (localStorage nexus-lang)
    // via le message {type:'set-lang', lang}. Defaut 'fr'.
    private string _lang = "fr";

    // ADBLOCK : bouton separe, toujours actif par defaut. Independant du Vault.
    private bool _adblockOn = true;
    private int _adBlocked;

    // MODELE IA : choisi par l'utilisateur dans Personnalisation. null = fallback provider.
    private string? _aiModel;
	
	private readonly ISettingsStore _settings;
	private string? _aiProvider;

    private string _thumbDir = "";
    private const string ThumbHost = "nexus-thumbs";

    // Trackers du Vault (confidentialite).
    private static readonly string[] _trackers = {
        "doubleclick.net","google-analytics.com","googletagmanager.com","googlesyndication.com",
        "adservice.google.com","connect.facebook.net","adnxs.com","scorecardresearch.com",
        "hotjar.com","segment.io","mixpanel.com","criteo.com","taboola.com","outbrain.com",
        "amazon-adsystem.com","adsafeprotected.com","quantserve.com","moatads.com"
    };

    // ADBLOCK : blocklist condensee (regies pub + analytics). Legere et autonome.
    private static readonly string[] _adHosts = {
        "doubleclick.net","googlesyndication.com","googleadservices.com","google-analytics.com",
        "googletagmanager.com","googletagservices.com","adservice.google.com","pagead2.googlesyndication.com",
        "partner.googleadservices.com","2mdn.net","adsense.google.com",
        "connect.facebook.net","facebook.com/tr","business.facebook.com/tr",
        "amazon-adsystem.com","assoc-amazon.com","aax.amazon-adsystem.com",
        "adnxs.com","rubiconproject.com","pubmatic.com","openx.net","criteo.com","criteo.net",
        "casalemedia.com","smartadserver.com","adform.net","adroll.com","taboola.com","outbrain.com",
        "media.net","sharethrough.com","3lift.com","gumgum.com","yieldmo.com","teads.tv",
        "spotxchange.com","spotx.tv","indexww.com","contextweb.com","bidswitch.net","adsrvr.org",
        "moatads.com","adsafeprotected.com","adcolony.com","applovin.com","unityads.unity3d.com",
        "inmobi.com","mopub.com","chartboost.com","smaato.net","pubnative.net","mgid.com",
        "revcontent.com","zergnet.com","content.ad","adblade.com","propellerads.com","popads.net",
        "popcash.net","exoclick.com","juicyads.com","trafficjunky.com","adsterra.com","hilltopads.net",
        "scorecardresearch.com","quantserve.com","quantcount.com","hotjar.com","mixpanel.com",
        "segment.io","segment.com","fullstory.com","mouseflow.com","crazyegg.com","clarity.ms",
        "newrelic.com","nr-data.net","bugsnag.com","sentry.io","amplitude.com","heap.io",
        "kissmetrics.com","chartbeat.com","parsely.com","cxense.com","branch.io","appsflyer.com",
        "adjust.com","kochava.com","onesignal.com","optimizely.com","hs-analytics.net","hsubspot.com",
        "matomo.cloud","statcounter.com","yandex.ru/metrika","mc.yandex.ru","clicktale.net",
        "cookielaw.org","onetrust.com","quantcast.mgr.consensu.org","usercentrics.eu","sourcepoint.com",
        "cdn.cookielaw.org","privacy-mgmt.com","ad-delivery.net","adserver.org","ads.yahoo.com",
        "advertising.com","bluekai.com","krxd.net","demdex.net","everesttech.net","rlcdn.com",
        "agkn.com","tapad.com","crwdcntrl.net","turn.com","mathtag.com","serving-sys.com",
        "flashtalking.com","doubleverify.com","imrworldwide.com","nielsen.com","zqtk.net"
    };

    // ADBLOCK : filtrage cosmetique. Masque les emplacements pub injectes. Injecte a chaque document.
    private const string CosmeticCss = @"
[id^='google_ads_'],[id^='div-gpt-ad'],[id*='adsbygoogle'],ins.adsbygoogle,
[class*='ad-banner'],[class*='ad-slot'],[class*='ad-container'],[class*='ad-wrapper'],
[class^='ad-'],[class$='-ad'],[class*=' ad '],[class*='advert'],[class*='sponsored'],
[id*='banner-ad'],[id*='ad-banner'],[data-ad],[data-ad-slot],[data-ad-client],
iframe[src*='doubleclick'],iframe[src*='googlesyndication'],iframe[src*='adnxs'],
iframe[src*='amazon-adsystem'],iframe[src*='/ads/'],aside[aria-label*='publicit'],
.adsbygoogle,.ad-placeholder,.sponsored-content,.promoted-content{
display:none !important;visibility:hidden !important;height:0 !important;min-height:0 !important;
}";

    // Le script cosmetique lit un flag pose par le C# (window.__nxAdblock) et n'agit que si actif.
    private const string CosmeticScript = @"(function(){
if(window.__nxCosmeticInit) return; window.__nxCosmeticInit=true;
function apply(){
if(!window.__nxAdblock){ var e=document.getElementById('__nxAdCss'); if(e) e.remove(); return; }
if(document.getElementById('__nxAdCss')) return;
var st=document.createElement('style'); st.id='__nxAdCss';
st.textContent=" + "\"" + "__CSS__" + "\"" + @";
(document.head||document.documentElement).appendChild(st);
}
apply();
document.addEventListener('DOMContentLoaded', apply);
window.__nxApplyAdblock = apply;
})();";

    private const string GestureScript = @"(function(){
if(window.__nxGestInit) return; window.__nxGestInit=true;
var down=false,moved=false,x=0,y=0;
document.addEventListener('mousedown',function(e){ if(e.button!==2) return; down=true; moved=false; x=e.clientX; y=e.clientY; }, true);
document.addEventListener('mousemove',function(e){ if(!down) return; if(Math.abs(e.clientX-x)>14||Math.abs(e.clientY-y)>14) moved=true; }, true);
document.addEventListener('mouseup',function(e){ if(!down||e.button!==2){ down=false; return; } down=false; if(!moved) return;
var dx=e.clientX-x, dy=e.clientY-y, a=null;
if(Math.abs(dx)>Math.abs(dy)) a=(dx<0?'forward':'back'); else if(dy>0) a='reload';
if(a && window.chrome && window.chrome.webview) window.chrome.webview.postMessage({type:'nav',action:a});
}, true);
document.addEventListener('contextmenu',function(e){ if(moved){ e.preventDefault(); moved=false; } }, true);
})();";

    public ShellWindow(AiOrchestrator ai, IBookmarkRepository bookmarks, IHistoryRepository history, TabManager tabs, ISettingsStore settings)
    {
        _ai = ai;
        _bookmarks = bookmarks;
        _history = history;
        _tabs = tabs;
		_settings = settings;
        // DNS SECURISE : doit etre pose AVANT toute creation du moteur WebView2,
        // donc avant InitializeComponent (qui instancie le controle WebView2).
        if (!_dohApplied)
        {
            _dohApplied = true;
            try
            {
                var existing = Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS") ?? "";
                var dohArgs = "--dns-over-https-mode=secure --dns-over-https-templates=" + DohTemplate;
                if (!existing.Contains("dns-over-https"))
                {
                    Environment.SetEnvironmentVariable(
                        "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
                        (existing + " " + dohArgs).Trim());
                }
            }
            catch { /* pas de DoH plutot qu'un crash */ }
        }

        InitializeComponent();
        var wa = SystemParameters.WorkArea;
        Left = wa.Left; Top = wa.Top;
        Width = wa.Width; Height = wa.Height;
        _maxed = true;
        Loaded += OnLoaded;
        TrySetWindowIcon();
    }

    private void TrySetWindowIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "nexus-transparent_icons", "icon_256x256.ico");
            if (File.Exists(path))
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                    new Uri(path), System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        }
        catch { /* pas d'icone plutot qu'un crash */ }
    }

    private string T(string key)
    {
        var lang = _lang == "en" || _lang == "ar" ? _lang : "fr";
        if (_i18n.TryGetValue(key, out var row) && row.TryGetValue(lang, out var val))
            return val;
        if (row != null && row.TryGetValue("fr", out var fr)) return fr;
        return key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> _i18n = new()
    {
        ["vault.pill"] = new() { ["fr"] = "Vault - {0} bloques", ["en"] = "Vault - {0} blocked", ["ar"] = "الخزنة - {0} محظور" },
        ["fav.nothing"] = new() { ["fr"] = "Rien a ajouter", ["en"] = "Nothing to add", ["ar"] = "لا شيء لإضافته" },
        ["fav.already"] = new() { ["fr"] = "Deja en favori", ["en"] = "Already saved", ["ar"] = "مضاف مسبقًا" },
        ["fav.added"] = new() { ["fr"] = "★ Ajoute !", ["en"] = "★ Saved!", ["ar"] = "★ تمت الإضافة!" },
        ["day.today"] = new() { ["fr"] = "Aujourd'hui", ["en"] = "Today", ["ar"] = "اليوم" },
        ["day.yesterday"] = new() { ["fr"] = "Hier", ["en"] = "Yesterday", ["ar"] = "أمس" },
        ["rel.now"] = new() { ["fr"] = "a l'instant", ["en"] = "just now", ["ar"] = "الآن" },
        ["sum.title"] = new() { ["fr"] = "Resume de la page", ["en"] = "Page summary", ["ar"] = "ملخّص الصفحة" },
        ["sum.reading"] = new() { ["fr"] = "Lecture de la page...", ["en"] = "Reading the page...", ["ar"] = "جارٍ قراءة الصفحة..." },
        ["sum.ytFetch"] = new() { ["fr"] = "Recuperation du transcript de la video...", ["en"] = "Fetching the video transcript...", ["ar"] = "جارٍ جلب نص الفيديو..." },
        ["sum.pdfFetch"] = new() { ["fr"] = "Extraction du texte du PDF...", ["en"] = "Extracting PDF text...", ["ar"] = "جارٍ استخراج نص PDF..." },
        ["sum.ytNone"] = new() { ["fr"] = "Cette video n a pas de sous-titres exploitables (active les sous-titres puis reessaie).", ["en"] = "This video has no usable captions (enable captions then retry).", ["ar"] = "لا تتوفّر ترجمة قابلة للاستخدام لهذا الفيديو (فعّل الترجمة ثم أعد المحاولة)." },
        ["sum.ytUnavail"] = new() { ["fr"] = "Transcript indisponible : ", ["en"] = "Transcript unavailable: ", ["ar"] = "النص غير متوفّر: " },
        ["sum.cantRead"] = new() { ["fr"] = "Impossible de lire le contenu de cette page.", ["en"] = "Unable to read this page's content.", ["ar"] = "تعذّرت قراءة محتوى هذه الصفحة." },
        ["sum.none"] = new() { ["fr"] = "Aucun resume genere (verifie qu Ollama tourne).", ["en"] = "No summary generated (check that Ollama is running).", ["ar"] = "لم يُنشأ أي ملخّص (تأكّد من تشغيل Ollama)." },
        ["sum.error"] = new() { ["fr"] = "Erreur : ", ["en"] = "Error: ", ["ar"] = "خطأ: " },
        ["perm.ask"] = new() { ["fr"] = "{0} demande l'acces a : {1}", ["en"] = "{0} wants access to: {1}", ["ar"] = "{0} يطلب الوصول إلى: {1}" },
        ["perm.allow"] = new() { ["fr"] = "Autoriser", ["en"] = "Allow", ["ar"] = "السماح" },
        ["perm.deny"] = new() { ["fr"] = "Refuser", ["en"] = "Deny", ["ar"] = "رفض" },
        ["perm.remember"] = new() { ["fr"] = "Retenir pour ce site", ["en"] = "Remember for this site", ["ar"] = "تذكّر لهذا الموقع" },
        ["perm.mic"] = new() { ["fr"] = "le microphone", ["en"] = "the microphone", ["ar"] = "الميكروفون" },
        ["perm.cam"] = new() { ["fr"] = "la camera", ["en"] = "the camera", ["ar"] = "الكاميرا" },
        ["perm.geo"] = new() { ["fr"] = "la localisation", ["en"] = "your location", ["ar"] = "الموقع الجغرافي" },
        ["perm.notif"] = new() { ["fr"] = "les notifications", ["en"] = "notifications", ["ar"] = "الإشعارات" },
        ["perm.clip"] = new() { ["fr"] = "le presse-papiers", ["en"] = "the clipboard", ["ar"] = "الحافظة" },
        ["perm.sensors"] = new() { ["fr"] = "les capteurs", ["en"] = "device sensors", ["ar"] = "المستشعرات" },
    };

    private System.Globalization.CultureInfo DateCulture() => _lang switch
    {
        "en" => new System.Globalization.CultureInfo("en-US"),
        "ar" => new System.Globalization.CultureInfo("ar"),
        _ => new System.Globalization.CultureInfo("fr-FR"),
    };

    private void UpdateVaultPill()
    {
        VaultPill.Visibility = _vaultOn ? Visibility.Visible : Visibility.Collapsed;
        VaultText.Text = string.Format(T("vault.pill"), _blocked);
    }

    private void ShowLoading(string url)
    {
        LoadingUrl.Text = url;
        LoadingOverlay.BeginAnimation(OpacityProperty, null);
        LoadingOverlay.Opacity = 0;
        LoadingOverlay.Visibility = Visibility.Visible;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        LoadingOverlay.BeginAnimation(OpacityProperty, fade);
    }

    private void HideLoading()
    {
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        fade.Completed += (_, _) =>
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingOverlay.BeginAnimation(OpacityProperty, null);
            LoadingOverlay.Opacity = 1;
        };
        LoadingOverlay.BeginAnimation(OpacityProperty, fade);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Web.EnsureCoreWebView2Async();
        var vm = (ShellViewModel)DataContext;

        try
        {
            _thumbDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Nexus", "thumbs");
            Directory.CreateDirectory(_thumbDir);
            Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                ThumbHost, _thumbDir, CoreWebView2HostResourceAccessKind.Allow);
        }
        catch { /* si indispo, le mode Image retombe sur les apercus generes cote page */ }

        try { await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GestureScript); }
        catch { }

        try
        {
            var cosmetic = CosmeticScript.Replace("__CSS__",
                System.Text.Json.JsonSerializer.Serialize(CosmeticCss).Trim('"'));
            await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                "window.__nxAdblock=" + (_adblockOn ? "true" : "false") + ";");
            await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(cosmetic);
        }
        catch { }

        Web.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        Web.CoreWebView2.WebResourceRequested += (_, args) =>
        {
            var u = args.Request.Uri;

            if (_adblockOn)
            {
                foreach (var ad in _adHosts)
                {
                    if (u.Contains(ad, StringComparison.OrdinalIgnoreCase))
                    {
                        args.Response = Web.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked", "");
                        _adBlocked++;
                        Dispatcher.Invoke(() => Post(new { type = "adblock-blocked", count = _adBlocked }));
                        return;
                    }
                }
            }

            if (!_vaultOn) return;
            foreach (var t in _trackers)
            {
                if (u.Contains(t, StringComparison.OrdinalIgnoreCase))
                {
                    args.Response = Web.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked", "");
                    _blocked++;
                    Dispatcher.Invoke(() => { UpdateVaultPill(); Post(new { type = "vault-blocked", count = _blocked }); });
                    return;
                }
            }
        };

        Web.CoreWebView2.DownloadStarting += OnDownloadStarting;

        Web.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            var target = args.Uri;
            if (string.IsNullOrEmpty(target)) return;
            _forceNewTab = true;
            Dispatcher.Invoke(() =>
            {
                ShowLoading(target);
                Web.CoreWebView2.Navigate(target);
            });
        };

        Web.CoreWebView2.WebMessageReceived += async (_, args) =>
        {
            var msg = System.Text.Json.JsonDocument.Parse(args.WebMessageAsJson).RootElement;
            var type = msg.GetProperty("type").GetString();
            switch (type)
            {
                case "command":
                    var text = msg.GetProperty("text").GetString() ?? "";
                    Dispatcher.Invoke(() => { vm.Orb.Input = text; vm.Orb.SubmitCommand.Execute(null); });
                    break;
                case "ai":
                    var cap = msg.GetProperty("capability").GetString() ?? "";
                    var content = msg.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                    _ = RunAiAsync(cap, content);
                    break;
                case "set-lang":
                    _lang = msg.TryGetProperty("lang", out var lg) ? (lg.GetString() ?? "fr") : "fr";
                    Dispatcher.Invoke(UpdateVaultPill);
                    await PushHistoryAsync();
                    await PushTimelineAsync();
                    break;
                case "set-model":
                    _aiModel = msg.TryGetProperty("model", out var mdl) ? mdl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(_aiModel)) _aiModel = null;
                    break;
                case "list-models":
    var lmProv = msg.TryGetProperty("provider", out var lp) ? lp.GetString() : null;
    _ = PushModelsAsync(lmProv);
    break;
case "list-providers":
    PushProviders();
    break;
case "set-provider":
    _aiProvider = msg.TryGetProperty("key", out var pk) ? pk.GetString() : null;
    if (string.IsNullOrWhiteSpace(_aiProvider)) _aiProvider = null;
    break;
case "set-apikey":
    {
        var prov = msg.TryGetProperty("provider", out var pv) ? pv.GetString() : null;
        var akey = msg.TryGetProperty("key", out var kv) ? kv.GetString() : null;
        if (!string.IsNullOrWhiteSpace(prov))
        {
            _settings.Set("ai." + prov + ".apikey", akey ?? "");
            await _settings.SaveAsync();
        }
    }
    break;
                case "ready":
                    if (msg.TryGetProperty("lang", out var rl))
                    {
                        var rlv = rl.GetString();
                        if (!string.IsNullOrWhiteSpace(rlv)) _lang = rlv!;
                    }
                    await PushBookmarksAsync();
                    await PushHistoryAsync();
                    PushTabs();
                    Dispatcher.Invoke(UpdateVaultPill);
                    Post(new { type = "vault-state", on = _vaultOn, count = _blocked });
                    Post(new { type = "adblock-state", on = _adblockOn, count = _adBlocked });
                    if (_openConstAfterLoad) { _openConstAfterLoad = false; Post(new { type = "open-constellation" }); }
                    break;
                case "add-bookmark":
                    var url = msg.GetProperty("url").GetString() ?? "";
                    var title = msg.TryGetProperty("title", out var tt) ? tt.GetString() ?? url : url;
                    var coll = msg.TryGetProperty("collection", out var cl) ? cl.GetString() ?? "Unsorted" : "Unsorted";
                    if (Uri.TryCreate(url, UriKind.Absolute, out var bu))
                    {
                        var bm = new Bookmark { Url = bu, Title = title };
                        bm.Collection = string.IsNullOrWhiteSpace(coll) ? "Unsorted" : coll.Trim();
                        await _bookmarks.SaveAsync(bm);
                        await PushBookmarksAsync();
                    }
                    break;
                case "remove-bookmark":
                    if (Guid.TryParse(msg.GetProperty("id").GetString(), out var rid))
                    {
                        await _bookmarks.RemoveAsync(rid);
                        await PushBookmarksAsync();
                    }
                    break;
                case "rename-bookmark":
                    if (Guid.TryParse(msg.GetProperty("id").GetString(), out var reid))
                    {
                        var newTitle = msg.TryGetProperty("title", out var rnt) ? rnt.GetString() ?? "" : "";
                        var all = await _bookmarks.GetAllAsync();
                        var bm = all.FirstOrDefault(b => b.Id == reid);
                        if (bm != null && !string.IsNullOrWhiteSpace(newTitle))
                        {
                            bm.Title = newTitle;
                            await _bookmarks.SaveAsync(bm);
                            await PushBookmarksAsync();
                        }
                    }
                    break;
                case "update-bookmark":
                    if (Guid.TryParse(msg.GetProperty("id").GetString(), out var uid))
                    {
                        var all = await _bookmarks.GetAllAsync();
                        var bm = all.FirstOrDefault(b => b.Id == uid);
                        if (bm != null)
                        {
                            if (msg.TryGetProperty("title", out var ut) && !string.IsNullOrWhiteSpace(ut.GetString()))
                                bm.Title = ut.GetString()!;
                            if (msg.TryGetProperty("collection", out var uc))
                            {
                                var v = uc.GetString();
                                bm.Collection = string.IsNullOrWhiteSpace(v) ? "Unsorted" : v!.Trim();
                            }
                            if (msg.TryGetProperty("tags", out var ug) && ug.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                bm.Tags.Clear();
                                foreach (var el in ug.EnumerateArray())
                                {
                                    var tg = el.GetString();
                                    if (!string.IsNullOrWhiteSpace(tg)) bm.Tags.Add(tg!.Trim());
                                }
                            }
                            await _bookmarks.SaveAsync(bm);
                            await PushBookmarksAsync();
                        }
                    }
                    break;
                case "switch-tab":
                    if (Guid.TryParse(msg.GetProperty("id").GetString(), out var sid))
                    {
                        var tab = _tabs.Tabs.FirstOrDefault(t => t.Id == sid);
                        if (tab?.Url != null)
                        {
                            _tabs.Activate(tab);
                            Dispatcher.Invoke(() =>
                            {
                                _switchingTab = true;
                                ShowLoading(tab.Url.ToString());
                                Web.CoreWebView2.Navigate(tab.Url.ToString());
                            });
                            PushTabs();
                        }
                    }
                    break;
                case "close-tab":
                    if (Guid.TryParse(msg.GetProperty("id").GetString(), out var cid))
                    {
                        _tabs.Close(cid);
                        PushTabs();
                    }
                    break;
                case "new-tab":
                    _forceNewTab = true;
                    break;
                case "cancel-download":
                    CancelDownload(msg.GetProperty("id").GetString() ?? "");
                    break;
                case "open-download":
                    OpenDownload(msg.GetProperty("id").GetString() ?? "");
                    break;
                case "clear-downloads":
                    foreach (var k in _downloads.Keys.ToList())
                        if (_downloads[k].State != CoreWebView2DownloadState.InProgress)
                            _downloads.Remove(k);
                    break;
                case "load-timeline":
                    var query = msg.TryGetProperty("query", out var q) ? q.GetString() : null;
                    await PushTimelineAsync(query);
                    break;
                case "clear-history":
                    await _history.ClearAsync();
                    TryClearThumbs();
                    await PushTimelineAsync();
                    await PushHistoryAsync();
                    break;
                case "vault":
                    _vaultOn = msg.TryGetProperty("on", out var vo) && vo.GetBoolean();
                    if (_vaultOn) _blocked = 0;
                    NavigationService.PrivateMode = _vaultOn;
                    await ApplyVaultSessionAsync(_vaultOn);
                    Dispatcher.Invoke(UpdateVaultPill);
                    Post(new { type = "vault-state", on = _vaultOn, count = _blocked });
                    break;
                case "adblock":
                    _adblockOn = msg.TryGetProperty("on", out var ao) && ao.GetBoolean();
                    try
                    {
                        var core2 = Web.CoreWebView2;
                        if (core2 != null)
                        {
                            await core2.ExecuteScriptAsync("window.__nxAdblock=" + (_adblockOn ? "true" : "false") + "; if(window.__nxApplyAdblock) window.__nxApplyAdblock();");
                        }
                    }
                    catch { }
                    Post(new { type = "adblock-state", on = _adblockOn, count = _adBlocked });
                    break;
                case "city-image":
                    var cityImg = msg.TryGetProperty("city", out var ci) ? ci.GetString() ?? "" : "";
                    _ = LoadCityImageAsync(cityImg);
                    break;
                case "set-gestures":
                    _gesturesOn = msg.TryGetProperty("on", out var ge) && ge.GetBoolean();
                    break;
                case "nav":
                    if (_gesturesOn)
                    {
                        var action = msg.TryGetProperty("action", out var na) ? na.GetString() : null;
                        var core = Web.CoreWebView2;
                        if (core != null)
                        {
                            if (action == "back" && core.CanGoBack) core.GoBack();
                            else if (action == "forward" && core.CanGoForward) core.GoForward();
                            else if (action == "reload") core.Reload();
                        }
                    }
                    break;
            }
        };

        vm.NavigateRequested += url => Dispatcher.Invoke(async () =>
        {
            ShowLoading(url.ToString());
            Web.CoreWebView2.Navigate(url.ToString());
            await PushHistoryAsync();
        });

        Web.CoreWebView2.NavigationCompleted += (_, _) =>
        {
            HideLoading();
            var core = Web.CoreWebView2;
            if (core == null) return;
            var src = core.Source;
            if (string.IsNullOrEmpty(src) || src.StartsWith(_homeUri, StringComparison.OrdinalIgnoreCase))
            {
                _switchingTab = false;
                _forceNewTab = false;
                SummarizeBtn.Visibility = Visibility.Collapsed;
                return;
            }
            SummarizeBtn.Visibility = Visibility.Visible;

            if (!_vaultOn) _ = CaptureThumbAsync(core, src);

            Uri.TryCreate(src, UriKind.Absolute, out var srcUri);
            var docTitle = core.DocumentTitle;
            var active = _tabs.Active;

            if (_switchingTab)
            {
                if (active != null)
                {
                    if (srcUri != null) active.Url = srcUri;
                    if (!string.IsNullOrWhiteSpace(docTitle)) active.Title = docTitle;
                    active.Touch();
                }
                _switchingTab = false;
            }
            else if (_forceNewTab || active == null)
            {
                var t = _tabs.Open(srcUri);
                if (!string.IsNullOrWhiteSpace(docTitle)) t.Title = docTitle;
                _forceNewTab = false;
            }
            else if (srcUri != null && active.Url != null &&
                     !string.Equals(active.Url.Host, srcUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                var t = _tabs.Open(srcUri);
                if (!string.IsNullOrWhiteSpace(docTitle)) t.Title = docTitle;
            }
            else
            {
                if (srcUri != null) active.Url = srcUri;
                if (!string.IsNullOrWhiteSpace(docTitle)) active.Title = docTitle;
                active.Touch();
            }
            PushTabs();
        };

        var home = Path.Combine(AppContext.BaseDirectory, "Assets", "home.html");
        if (File.Exists(home))
        {
            _homeUri = new Uri(home).ToString();
            Web.CoreWebView2.Navigate(_homeUri);
        }

        PreviewKeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Oem2 && !vm.Orb.IsOpen) { vm.Orb.IsOpen = true; OrbInput.Focus(); ke.Handled = true; }
            else if (ke.Key == Key.Escape) { vm.Orb.IsOpen = false; vm.ConstellationOpen = false; }
            else if (ke.Key == Key.Enter && vm.Orb.IsOpen) { vm.Orb.SubmitCommand.Execute(null); }
        };
    }

    // ===== MODELES IA : liste des modeles disponibles cote provider (ex. Ollama /api/tags) =====

    private async Task PushModelsAsync(string? providerKey = null)
{
    IReadOnlyList<string> items;
    try { items = await _ai.ListModelsAsync(providerKey ?? _aiProvider); }
    catch { items = Array.Empty<string>(); }
    Post(new { type = "models", items });
}

private void PushProviders()
{
    var items = _ai.Providers().Select(p => new { key = p.Key, name = p.Name, local = p.Local });
    Post(new { type = "providers", items });
}

    // ===== TIMELINE : vignettes reelles =====

    private static string HashUrl(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        var sb = new StringBuilder(64);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private string ThumbFileFor(string url) => Path.Combine(_thumbDir, HashUrl(url) + ".png");

    private async Task CaptureThumbAsync(CoreWebView2 core, string src)
    {
        if (string.IsNullOrEmpty(_thumbDir)) return;
        try
        {
            await Task.Delay(600);
            if (!string.Equals(core.Source, src, StringComparison.OrdinalIgnoreCase)) return;
            var path = ThumbFileFor(src);
            var tmp = path + ".tmp";
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
            }
            File.Copy(tmp, path, overwrite: true);
            File.Delete(tmp);
        }
        catch { /* pas de vignette plutot qu'un crash */ }
    }

    private void TryClearThumbs()
    {
        try
        {
            if (string.IsNullOrEmpty(_thumbDir) || !Directory.Exists(_thumbDir)) return;
            foreach (var f in Directory.EnumerateFiles(_thumbDir, "*.png")) File.Delete(f);
        }
        catch { }
    }

    private string? ThumbUrlFor(string url)
    {
        if (string.IsNullOrEmpty(_thumbDir)) return null;
        try
        {
            var path = ThumbFileFor(url);
            if (!File.Exists(path)) return null;
            var v = File.GetLastWriteTimeUtc(path).Ticks;
            return $"https://{ThumbHost}/{HashUrl(url)}.png?v={v}";
        }
        catch { return null; }
    }

    private async void Fav_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 == null) return;
        var src = Web.CoreWebView2.Source;
        if (string.IsNullOrEmpty(src) || src.StartsWith(_homeUri, StringComparison.OrdinalIgnoreCase))
        {
            FlashFav(T("fav.nothing"), "#EE8859");
            return;
        }
        if (!Uri.TryCreate(src, UriKind.Absolute, out var u)) return;
        var existing = await _bookmarks.GetAllAsync();
        if (existing.Any(b => b.Url == u))
        {
            FlashFav(T("fav.already"), "#F4AC66");
            return;
        }
        var title = Web.CoreWebView2.DocumentTitle;
        if (string.IsNullOrWhiteSpace(title)) title = u.Host;
        await _bookmarks.SaveAsync(new Bookmark { Url = u, Title = title });
        await PushBookmarksAsync();
        FlashFav(T("fav.added"), "#F4AC66");
    }

    // FlashFav : on remplace texte + couleur, puis on rend la main au template
    // via ClearValue (et NON en reappliquant la couleur capturee, qui pouvait
    // etre celle du survol et bloquer le bouton en teinte sombre illisible).
    private void FlashFav(string message, string color)
    {
        var prevText = FavBtn.Content;
        FavBtn.Content = message;
        FavBtn.SetValue(ForegroundProperty, new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)));

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
        timer.Tick += (_, _) =>
        {
            FavBtn.Content = prevText;
            FavBtn.ClearValue(ForegroundProperty);
            timer.Stop();
        };
        timer.Start();
    }

    private async Task LoadCityImageAsync(string city)
    {
        if (string.IsNullOrWhiteSpace(city)) return;
        try
        {
            var imgUrl = await ResolveCityPhotoUrlAsync(city);
            if (string.IsNullOrEmpty(imgUrl)) return;

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var imgReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, imgUrl);
            imgReq.Headers.UserAgent.ParseAdd("NexusBrowser/1.0 (contact@nexus.app)");
            using var imgResp = await _http.SendAsync(imgReq, cts.Token);
            if (!imgResp.IsSuccessStatusCode) return;
            var ctype = imgResp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (!ctype.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return;
            var bytes = await imgResp.Content.ReadAsByteArrayAsync(cts.Token);
            if (bytes.Length < 1024) return;
            var dataUri = "data:" + ctype + ";base64," + Convert.ToBase64String(bytes);
            Post(new { type = "city-image", city, dataUri });
        }
        catch { }
    }

    private async Task<string?> ResolveCityPhotoUrlAsync(string city)
    {
        var viaCommons = await CommonsPhotoAsync(city);
        if (!string.IsNullOrEmpty(viaCommons)) return viaCommons;

        foreach (var lang in new[] { "fr", "en" })
        {
            try
            {
                var api = "https://" + lang + ".wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(city);
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(6));
                using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, api);
                req.Headers.UserAgent.ParseAdd("NexusBrowser/1.0 (contact@nexus.app)");
                using var resp = await _http.SendAsync(req, cts.Token);
                if (!resp.IsSuccessStatusCode) continue;
                var json = await resp.Content.ReadAsStringAsync(cts.Token);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                string? cand = null;
                if (root.TryGetProperty("originalimage", out var orig) && orig.TryGetProperty("source", out var os)) cand = os.GetString();
                else if (root.TryGetProperty("thumbnail", out var th) && th.TryGetProperty("source", out var ts)) cand = ts.GetString();
                if (!string.IsNullOrEmpty(cand) && !IsBadImage(cand)) return cand;
            }
            catch { }
        }
        return null;
    }

    private async Task<string?> CommonsPhotoAsync(string city)
    {
        try
        {
            var api = "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search"
                + "&gsrsearch=" + Uri.EscapeDataString(city + " cityscape skyline")
                + "&gsrnamespace=6&gsrlimit=8&prop=imageinfo&iiprop=url&iiurlwidth=900";
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, api);
            req.Headers.UserAgent.ParseAdd("NexusBrowser/1.0 (contact@nexus.app)");
            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("query", out var query)) return null;
            if (!query.TryGetProperty("pages", out var pages)) return null;
            foreach (var page in pages.EnumerateObject())
            {
                var p = page.Value;
                if (!p.TryGetProperty("title", out var titleEl)) continue;
                var title = titleEl.GetString() ?? "";
                if (IsBadImage(title)) continue;
                if (!p.TryGetProperty("imageinfo", out var infos) || infos.GetArrayLength() == 0) continue;
                var info = infos[0];
                var url = info.TryGetProperty("thumburl", out var tu) ? tu.GetString()
                    : (info.TryGetProperty("url", out var u) ? u.GetString() : null);
                if (!string.IsNullOrEmpty(url) && !IsBadImage(url)) return url;
            }
        }
        catch { }
        return null;
    }

    private static bool IsBadImage(string s)
    {
        var l = s.ToLowerInvariant();
        string[] bad = { "flag", "drapeau", "coat_of_arms", "coat of arms", "blason", "armoiries",
            "seal", "sceau", "emblem", "embleme", "logo", "map", "carte", "locator",
            "icon", ".svg", "wappen", "escudo", "bandera" };
        foreach (var b in bad) if (l.Contains(b)) return true;
        return false;
    }

    private bool _summarizing;

    private async void Summarize_Click(object sender, RoutedEventArgs e)
    {
        var core = Web.CoreWebView2;
        if (core == null || _summarizing) return;
        var src = core.Source;
        if (string.IsNullOrEmpty(src) || src.StartsWith(_homeUri, StringComparison.OrdinalIgnoreCase)) return;

        _summarizing = true;
        try
        {
            var sumTitleJson = System.Text.Json.JsonSerializer.Serialize(T("sum.title"));
            var readingJson = System.Text.Json.JsonSerializer.Serialize(T("sum.reading"));
            var setup = @"(function(){
if(!document.getElementById('__nexusSum')){
var o=document.createElement('div'); o.id='__nexusSum';
o.style.cssText='position:fixed;top:0;right:0;width:420px;max-width:92vw;height:100%;z-index:2147483647;background:rgba(31,23,18,0.97);color:#F7F2ED;font-family:system-ui,Segoe UI,sans-serif;box-shadow:-16px 0 60px rgba(0,0,0,.5);display:flex;flex-direction:column;transform:translateX(100%);transition:transform .35s cubic-bezier(.16,1,.3,1)';
var head=document.createElement('div');
head.style.cssText='display:flex;align-items:center;gap:10px;padding:18px 20px;border-bottom:1px solid rgba(255,255,255,.1)';
var badge=document.createElement('div');
badge.style.cssText='width:30px;height:30px;border-radius:9px;background:#9FD6E2;display:grid;place-items:center;color:#1F1712;font-size:15px';
badge.textContent='✨';
var title=document.createElement('div');
title.style.cssText='font-weight:600;font-size:15px;flex:1'; title.textContent=" + sumTitleJson + @";
var x=document.createElement('button'); x.id='__nexusSumX';
x.style.cssText='width:28px;height:28px;border:none;border-radius:50%;background:rgba(255,255,255,.12);color:#fff;cursor:pointer;font-size:14px';
x.textContent='✕';
head.appendChild(badge); head.appendChild(title); head.appendChild(x);
var body=document.createElement('div'); body.id='__nexusSumBody';
body.style.cssText='padding:18px 20px;overflow-y:auto;font-size:14px;line-height:1.6;white-space:pre-wrap;flex:1';
o.appendChild(head); o.appendChild(body);
document.documentElement.appendChild(o);
x.addEventListener('click',function(){ o.style.transform='translateX(100%)'; });
requestAnimationFrame(function(){ o.style.transform='none'; });
} else {
var el=document.getElementById('__nexusSum'); el.style.transform='none';
}
document.getElementById('__nexusSumBody').textContent=" + readingJson + @";
var t=document.title||''; var b=document.body?document.body.innerText:'';
return (t+String.fromCharCode(10,10)+b).slice(0,4000);
})()";
            var raw = await core.ExecuteScriptAsync(setup);

            string pageText;
            var vid = YouTubeVideoId(src);
            if (vid != null)
            {
                var ytFetchJson = System.Text.Json.JsonSerializer.Serialize(T("sum.ytFetch"));
                await core.ExecuteScriptAsync("(function(){var e=document.getElementById('__nexusSumBody'); if(e) e.textContent=" + ytFetchJson + ";})()");
                var transcript = await FetchYouTubeTranscriptInPageAsync(core, vid);
                if (string.IsNullOrWhiteSpace(transcript.text))
                {
                    var shown = transcript.diag == "NONE"
                        ? T("sum.ytNone")
                        : T("sum.ytUnavail") + transcript.diag;
                    var payloadMsg = System.Text.Json.JsonSerializer.Serialize(shown);
                    await core.ExecuteScriptAsync("(function(){var e=document.getElementById('__nexusSumBody'); if(e) e.textContent=" + payloadMsg + ";})()");
                    _summarizing = false;
                    return;
                }
                var full = transcript.text!;
                pageText = full.Length > 8000 ? full.Substring(0, 8000) : full;
            }
            else if (IsPdf(src))
            {
                var pdfFetchJson = System.Text.Json.JsonSerializer.Serialize(T("sum.pdfFetch"));
                await core.ExecuteScriptAsync("(function(){var e=document.getElementById('__nexusSumBody'); if(e) e.textContent=" + pdfFetchJson + ";})()");
                var pdf = await ExtractPdfTextAsync(src);
                if (string.IsNullOrWhiteSpace(pdf))
                {
                    var cantJson = System.Text.Json.JsonSerializer.Serialize(T("sum.cantRead"));
                    await core.ExecuteScriptAsync("(function(){var e=document.getElementById('__nexusSumBody'); if(e) e.textContent=" + cantJson + ";})()");
                    _summarizing = false;
                    return;
                }
                pageText = pdf.Length > 8000 ? pdf.Substring(0, 8000) : pdf;
            }
            else
            {
                pageText = System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? "";
            }

            if (string.IsNullOrWhiteSpace(pageText))
            {
                var cantJson = System.Text.Json.JsonSerializer.Serialize(T("sum.cantRead"));
                await core.ExecuteScriptAsync("(function(){var e=document.getElementById('__nexusSumBody'); if(e) e.textContent=" + cantJson + ";})()");
                _summarizing = false;
                return;
            }

            await core.ExecuteScriptAsync("(function(){var e=document.getElementById('__nexusSumBody'); if(e) e.textContent='';})()");
            var any = false;
            await foreach (var token in _ai.RunAsync(AiCapability.Summarize, pageText, _aiModel, _aiProvider))
            {
                any = true;
                var payload = System.Text.Json.JsonSerializer.Serialize(token);
                await core.ExecuteScriptAsync(
                    "(function(){var e=document.getElementById('__nexusSumBody'); if(e){ e.textContent += " + payload + "; e.scrollTop=e.scrollHeight; }})()");
            }
            if (!any)
            {
                var noneJson = System.Text.Json.JsonSerializer.Serialize(T("sum.none"));
                await core.ExecuteScriptAsync("(function(){var e=document.getElementById('__nexusSumBody'); if(e) e.textContent=" + noneJson + ";})()");
            }
        }
        catch (Exception ex)
        {
            var msg = System.Text.Json.JsonSerializer.Serialize(T("sum.error") + ex.Message);
            try { await core.ExecuteScriptAsync("(function(){var e=document.getElementById('__nexusSumBody'); if(e) e.textContent=" + msg + ";})()"); } catch { }
        }
        finally { _summarizing = false; }
    }

    private static string? YouTubeVideoId(string url)
    {
        try
        {
            var u = new Uri(url);
            var host = u.Host.Replace("www.", "").Replace("m.", "");
            if (host == "youtu.be")
                return u.AbsolutePath.Trim('/').Split('/')[0] is { Length: > 0 } s ? s : null;
            if (host != "youtube.com" && host != "youtube-nocookie.com") return null;
            string? v = null; foreach (var pair in u.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)) { var kv = pair.Split('=', 2); if (kv.Length == 2 && kv[0] == "v") { v = Uri.UnescapeDataString(kv[1]); break; } }
            if (!string.IsNullOrEmpty(v)) return v;
            var parts = u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
                if (parts[i] is "shorts" or "embed" or "v") return parts[i + 1];
            return null;
        }
        catch { return null; }
    }

    // ===== PDF : detection + extraction texte (PdfPig). Reutilise le pipeline Summarize. =====

    private static bool IsPdf(string url) =>
        url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
        url.Contains(".pdf?", StringComparison.OrdinalIgnoreCase);

    private async Task<string> ExtractPdfTextAsync(string url)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));
            var bytes = await _http.GetByteArrayAsync(url, cts.Token);
            using var doc = UglyToad.PdfPig.PdfDocument.Open(bytes);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
            {
                sb.Append(page.Text).Append((char)10);
                if (sb.Length > 12000) break; // granite 3b : on cape le contexte
            }
            return sb.ToString();
        }
        catch { return ""; }
    }

    private async Task<(string? text, string diag)> FetchYouTubeTranscriptInPageAsync(CoreWebView2 core, string videoId)
    {
        var kickoff = @"(function(){
window.__nxT=undefined;
(async function(){
function decodeEnt(s){
return (s||'')
.replace(/&amp;/g,'&').replace(/&#39;/g,""'"").replace(/&#34;/g,'""').replace(/&quot;/g,'""')
.replace(/&lt;/g,'<').replace(/&gt;/g,'>')
.replace(/&#(\d+);/g,function(_,n){ return String.fromCharCode(parseInt(n,10)); })
.replace(/&#x([0-9a-fA-F]+);/g,function(_,h){ return String.fromCharCode(parseInt(h,16)); });
}
function sliceArray(html, from){
var start = html.indexOf('[', from);
if(start < 0) return null;
var depth=0, inStr=false, esc=false;
for(var p=start; p<html.length; p++){
var ch = html[p];
if(inStr){ if(esc){esc=false;} else if(ch==='\\'){esc=true;} else if(ch==='""'){inStr=false;} }
else { if(ch==='""'){inStr=true;} else if(ch==='['){depth++;} else if(ch===']'){depth--; if(depth===0) return html.substring(start,p+1);} }
}
return null;
}
function extract(payload){
if(!payload) return '';
var s=payload.replace(/^\uFEFF/,'').trim();
if(s.charAt(0)==='{'){
try{ var d=JSON.parse(s); var ev=d&&d.events; if(ev){ var o=[]; for(var i=0;i<ev.length;i++){ var sg=ev[i].segs; if(!sg) continue; for(var j=0;j<sg.length;j++){ if(sg[j].utf8) o.push(sg[j].utf8); } } return o.join('').replace(/\s+/g,' ').trim(); } }catch(e){}
return '';
}
if(s.indexOf('<text')>=0){ var o1=[],re=/<text[^>]*>([\s\S]*?)<\/text>/g,m; while((m=re.exec(s))!==null){ o1.push(decodeEnt(m[1].replace(/<[^>]+>/g,' '))); } return o1.join(' ').replace(/\s+/g,' ').trim(); }
if(s.indexOf('<p')>=0){ var o2=[],re2=/<p[^>]*>([\s\S]*?)<\/p>/g,m2; while((m2=re2.exec(s))!==null){ o2.push(decodeEnt(m2[1].replace(/<[^>]+>/g,' '))); } var t2=o2.join(' ').replace(/\s+/g,' ').trim(); if(t2) return t2; }
if(s.indexOf('WEBVTT')>=0){ var lines=s.split(/\r?\n/), out=[]; for(var k=0;k<lines.length;k++){ var ln=lines[k]; if(!ln) continue; if(ln.indexOf('-->')>=0) continue; if(/^WEBVTT/.test(ln)) continue; if(/^\d+$/.test(ln.trim())) continue; if(/^(NOTE|STYLE|REGION)/.test(ln)) continue; out.push(ln.replace(/<[^>]+>/g,' ')); } return decodeEnt(out.join(' ')).replace(/\s+/g,' ').trim(); }
return '';
}
try{
var html;
try{ var r=await fetch(location.href,{credentials:'include'}); html=await r.text(); }
catch(e){ window.__nxT='NEXUS_ERR:html '+(e&&e.message?e.message:e); return; }
if(!html){ window.__nxT='NEXUS_ERR:html vide'; return; }

var idx = html.indexOf('""captionTracks""');
if(idx < 0){ window.__nxT='NEXUS_NONE'; return; }
var arrText = sliceArray(html, idx);
if(!arrText){ window.__nxT='NEXUS_ERR:crochets'; return; }
var tracks; try{ tracks=JSON.parse(arrText); }catch(e){ window.__nxT='NEXUS_ERR:parse '+(e&&e.message?e.message:e); return; }
if(!tracks||!tracks.length){ window.__nxT='NEXUS_NONE'; return; }

var pick=null;
for(var i=0;i<tracks.length;i++){ if(tracks[i].languageCode&&tracks[i].languageCode.indexOf('fr')===0){pick=tracks[i];break;} }
if(!pick){ for(var j=0;j<tracks.length;j++){ if(tracks[j].languageCode&&tracks[j].languageCode.indexOf('en')===0){pick=tracks[j];break;} } }
if(!pick) pick=tracks[0];
var base=pick.baseUrl; if(!base){ window.__nxT='NEXUS_NONE'; return; }
base = base.replace(/\\u0026/g,'&').replace(/&amp;/g,'&');
base = base.replace(/([?&])fmt=[^&]*/g,'$1').replace(/[?&]$/,'');

var fmts=['json3','srv3','srv1','vtt','']; var diag=[];
for(var f=0; f<fmts.length; f++){
var u = base + (fmts[f] ? ((base.indexOf('?')>=0?'&':'?')+'fmt='+fmts[f]) : '');
var body='';
try{ var cr=await fetch(u,{credentials:'include'}); body=await cr.text(); }
catch(e){ diag.push((fmts[f]||'def')+':err'); continue; }
diag.push((fmts[f]||'def')+':'+body.length);
var txt=extract(body);
if(txt){ window.__nxT=txt; return; }
}
window.__nxT='NEXUS_ERR:formats '+diag.join(' ');
}catch(e){ window.__nxT='NEXUS_ERR:'+(e&&e.message?e.message:e); }
})();
return 'STARTED';
})()";

        try { await core.ExecuteScriptAsync(kickoff); }
        catch (Exception ex) { return (null, "kickoff " + ex.Message); }

        string? result = null;
        for (var i = 0; i < 100; i++)
        {
            await Task.Delay(200);
            string probe;
            try { probe = await core.ExecuteScriptAsync("(window.__nxT===undefined?null:window.__nxT)"); }
            catch (Exception ex) { return (null, "probe " + ex.Message); }
            if (probe == "null" || string.IsNullOrEmpty(probe)) continue;
            try { result = System.Text.Json.JsonSerializer.Deserialize<string>(probe); }
            catch { result = probe.Trim('"'); }
            break;
        }
        if (string.IsNullOrWhiteSpace(result)) return (null, "timeout (aucune reponse)");
        if (result.StartsWith("NEXUS_NONE", StringComparison.Ordinal)) return (null, "NONE");
        if (result.StartsWith("NEXUS_ERR:", StringComparison.Ordinal)) return (null, result.Substring("NEXUS_ERR:".Length));
        return (System.Net.WebUtility.HtmlDecode(result), "OK");
    }

    private void Vault_Click(object sender, RoutedEventArgs e)
    {
        _vaultOn = !_vaultOn;
        if (_vaultOn) _blocked = 0;
        NavigationService.PrivateMode = _vaultOn;
        _ = ApplyVaultSessionAsync(_vaultOn);
        UpdateVaultPill();
        Post(new { type = "vault-state", on = _vaultOn, count = _blocked });
    }

    private async Task ApplyVaultSessionAsync(bool on)
    {
        try
        {
            var core = Web.CoreWebView2;
            if (core == null) return;
            var profile = core.Profile;
            if (profile == null) return;
            await profile.ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.Cookies
                | CoreWebView2BrowsingDataKinds.DiskCache
                | CoreWebView2BrowsingDataKinds.CacheStorage);
        }
        catch { }
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Max_Click(object sender, RoutedEventArgs e)
    {
        if (!_maxed)
        {
            _restore = new System.Windows.Rect(Left, Top, Width, Height);
            var wa = SystemParameters.WorkArea;
            Left = wa.Left; Top = wa.Top; Width = wa.Width; Height = wa.Height;
            _maxed = true;
        }
        else
        {
            Left = _restore.Left; Top = _restore.Top; Width = _restore.Width; Height = _restore.Height;
            _maxed = false;
        }
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var op = e.DownloadOperation;
        var id = Guid.NewGuid().ToString();
        _downloads[id] = op;
        _dlStart[id] = DateTimeOffset.UtcNow;
        PushDownload(id, op);
        op.BytesReceivedChanged += (_, _) => PushDownload(id, op);
        op.StateChanged += (_, _) => PushDownload(id, op);
    }

    private void PushDownload(string id, CoreWebView2DownloadOperation op)
    {
        var state = op.State switch
        {
            CoreWebView2DownloadState.InProgress => "active",
            CoreWebView2DownloadState.Completed => "done",
            CoreWebView2DownloadState.Interrupted =>
                op.InterruptReason == CoreWebView2DownloadInterruptReason.UserPaused ? "paused" : "failed",
            _ => "active"
        };
        string speed = "";
        if (state == "active" && _dlStart.TryGetValue(id, out var started))
        {
            var secs = (DateTimeOffset.UtcNow - started).TotalSeconds;
            if (secs > 0.5) speed = FmtBytes((long)(op.BytesReceived / secs)) + "/s";
        }
        var item = new
        {
            id,
            name = Path.GetFileName(op.ResultFilePath),
            received = op.BytesReceived,
            total = op.TotalBytesToReceive ?? 0,
            state,
            speed
        };
        Post(new { type = "download", item });
    }

    private void CancelDownload(string id)
    {
        if (_downloads.TryGetValue(id, out var op) && op.State == CoreWebView2DownloadState.InProgress)
            op.Cancel();
    }

    private void OpenDownload(string id)
    {
        if (_downloads.TryGetValue(id, out var op) && File.Exists(op.ResultFilePath))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{op.ResultFilePath}\"") { UseShellExecute = true });
    }

    private static string FmtBytes(long b)
    {
        if (b <= 0) return "0 o";
        string[] u = { "o", "Ko", "Mo", "Go" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }

    private void PushTabs()
    {
        var items = _tabs.Tabs.Select(t => new
        {
            id = t.Id.ToString(),
            title = t.Title,
            url = t.Url?.ToString() ?? "",
            live = _tabs.Active?.Id == t.Id
        });
        Post(new { type = "tabs", items });
    }

    private async Task PushBookmarksAsync()
    {
        var items = (await _bookmarks.GetAllAsync())
            .Select(b => new
            {
                id = b.Id.ToString(),
                title = b.Title,
                url = b.Url.ToString(),
                collection = string.IsNullOrWhiteSpace(b.Collection) ? "Unsorted" : b.Collection,
                tags = b.Tags ?? new List<string>(),
                thumbnail = b.ThumbnailPath
            });
        Post(new { type = "bookmarks", items });
    }

    private async Task PushHistoryAsync()
    {
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = (await _history.QueryAsync(null, null))
            .Where(h => seen.Add(h.Url.Host))
            .Take(5)
            .Select(h => new { title = h.Title, url = h.Url.ToString(), when = Relative(h.VisitedAt) });
        Post(new { type = "history", items });
    }

    private async Task PushTimelineAsync(string? term = null)
    {
        var entries = await _history.QueryAsync(string.IsNullOrWhiteSpace(term) ? null : term, null);
        var groups = entries
            .GroupBy(h => h.VisitedAt.ToLocalTime().Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new
            {
                day = DayLabel(g.Key),
                items = g.OrderByDescending(h => h.VisitedAt).Select(h => new
                {
                    title = h.Title,
                    url = h.Url.ToString(),
                    domain = h.Url.Host,
                    time = h.VisitedAt.ToLocalTime().ToString("HH:mm"),
                    thumbnail = ThumbUrlFor(h.Url.ToString())
                })
            });
        Post(new { type = "timeline", groups });
    }

    private string DayLabel(DateTime day)
    {
        var today = DateTime.Today;
        if (day == today) return T("day.today");
        if (day == today.AddDays(-1)) return T("day.yesterday");
        return day.ToString("dddd d MMMM", DateCulture());
    }

    private string Relative(DateTimeOffset when)
    {
        var span = DateTimeOffset.UtcNow - when;
        if (span.TotalMinutes < 1) return T("rel.now");
        var (m, h, d) = _lang switch
        {
            "en" => ("m", "h", "d"),
            "ar" => ("د", "س", "ي"),
            _ => ("m", "h", "j"),
        };
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}{m}";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}{h}";
        return $"{(int)span.TotalDays}{d}";
    }

    private async Task RunAiAsync(string capabilityLabel, string content)
    {
        var capability = MapCapability(capabilityLabel);
        var text = string.IsNullOrWhiteSpace(content) ? capabilityLabel : content;
        Post(new { type = "ai-start" });
        try
        {
            await foreach (var token in _ai.RunAsync(capability, text, _aiModel, _aiProvider))
                Post(new { type = "ai-token", token });
        }
        catch (Exception ex) { Post(new { type = "ai-error", message = ex.Message }); }
        Post(new { type = "ai-done" });
    }

    private void Post(object o) => Dispatcher.Invoke(() =>
    {
        var core = Web.CoreWebView2;
        if (core == null) return;
        core.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(o));
    });

    private static AiCapability MapCapability(string label) => label switch
    {
        _ when label.StartsWith("Summarize", StringComparison.OrdinalIgnoreCase) => AiCapability.Summarize,
        _ when label.StartsWith("Translate", StringComparison.OrdinalIgnoreCase) => AiCapability.Translate,
        _ when label.StartsWith("Draft", StringComparison.OrdinalIgnoreCase) => AiCapability.Rewrite,
        _ => AiCapability.Answer
    };

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        _tabs.Activate(null);
        if (!string.IsNullOrEmpty(_homeUri)) Web.CoreWebView2?.Navigate(_homeUri);
        PushTabs();
    }

    private void Spaces_Click(object sender, RoutedEventArgs e)
    {
        var core = Web.CoreWebView2;
        var onHome = core != null
            && core.Source.StartsWith(_homeUri, StringComparison.OrdinalIgnoreCase);
        if (onHome)
        {
            Post(new { type = "open-constellation" });
        }
        else
        {
            _openConstAfterLoad = true;
            _tabs.Activate(null);
            if (!string.IsNullOrEmpty(_homeUri)) core?.Navigate(_homeUri);
            PushTabs();
        }
    }

    private void Rail_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { Max_Click(sender, new RoutedEventArgs()); return; }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
