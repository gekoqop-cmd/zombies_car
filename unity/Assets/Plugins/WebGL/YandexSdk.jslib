// Bridge between the game and the Yandex Games SDK (window.ysdkPromise, started by the WebGL template's index.html
// as soon as the page opens, so the SDK loads in parallel with the game data). Every call waits for the SDK and does
// nothing when it is missing (local test without /sdk.js). Events go back to the "YandexSdk" object in Unity.
var YandexSdkLib = {
  $Ya: {
    receiver: "YandexSdk",
    // Resolves with the SDK (or null when it could not be loaded).
    sdk: function () {
      return window.ysdkPromise || Promise.resolve(null);
    },
    send: function (method, arg) {
      try {
        if (typeof SendMessage === "function") SendMessage(Ya.receiver, method, arg === undefined ? "" : String(arg));
        else if (window.unityInstance) window.unityInstance.SendMessage(Ya.receiver, method, arg === undefined ? "" : String(arg));
      } catch (e) { console.warn("[YandexSdk] SendMessage failed", e); }
    },
  },

  // Subscribes to the platform's pause / resume (fullscreen ads shown by the platform, overlays) and reports the
  // language once the SDK is up.
  YaInit: function () {
    Ya.sdk().then(function (s) {
      if (!s) { Ya.send("OnSdkMissing"); return; }
      try {
        s.on("game_api_pause", function () { Ya.send("OnPlatformPause"); });
        s.on("game_api_resume", function () { Ya.send("OnPlatformResume"); });
      } catch (e) { console.warn("[YandexSdk] events", e); }
      var lang = "ru";
      try { lang = s.environment.i18n.lang || "ru"; } catch (e) { }
      Ya.send("OnSdkReady", lang);
    });
  },

  // The game has loaded and the player can play (LoadingAPI.ready, requirement 1.19.2).
  YaLoadingReady: function () {
    Ya.sdk().then(function (s) {
      try { if (s && s.features && s.features.LoadingAPI) s.features.LoadingAPI.ready(); } catch (e) { console.warn(e); }
    });
  },

  // Gameplay started / resumed (1) or stopped: pause, menu, ad, hidden tab (0) — GameplayAPI, requirement 1.19.3.
  YaGameplay: function (running) {
    Ya.sdk().then(function (s) {
      try {
        if (!s || !s.features || !s.features.GameplayAPI) return;
        if (running) s.features.GameplayAPI.start(); else s.features.GameplayAPI.stop();
      } catch (e) { console.warn(e); }
    });
  },

  // Interstitial ad at a logical break; the platform limits how often it really shows.
  YaShowFullscreenAd: function () {
    Ya.sdk().then(function (s) {
      if (!s || !s.adv) { Ya.send("OnAdClose", "0"); return; }
      try {
        s.adv.showFullscreenAdv({
          callbacks: {
            onOpen: function () { Ya.send("OnAdOpen"); },
            onClose: function (wasShown) { Ya.send("OnAdClose", wasShown ? "1" : "0"); },
            onError: function (e) { console.warn("[YandexSdk] ad error", e); Ya.send("OnAdClose", "0"); },
          },
        });
      } catch (e) { console.warn(e); Ya.send("OnAdClose", "0"); }
    });
  },
};

autoAddDeps(YandexSdkLib, "$Ya");
mergeInto(LibraryManager.library, YandexSdkLib);
