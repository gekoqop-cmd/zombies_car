// Bridge between the game and the Yandex Games SDK (window.ysdkPromise, started by the WebGL template's index.html
// as soon as the page opens, so the SDK loads in parallel with the game data). Every call waits for the SDK and does
// nothing when it is missing (local test without /sdk.js). Events go back to the "YandexSdk" object in Unity.
var YandexSdkLib = {
  $Ya: {
    receiver: "YandexSdk",
    player: null,                 // the Yandex player (cloud saves), set by YaCloudLoad
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

  // Rewarded video (a case opened for watching): the reward is granted only in onRewarded, the game resumes on close.
  YaShowRewardedAd: function () {
    Ya.sdk().then(function (s) {
      if (!s || !s.adv) { Ya.send("OnRewardedClose", "0"); return; }
      try {
        s.adv.showRewardedVideo({
          callbacks: {
            onOpen: function () { Ya.send("OnAdOpen"); },
            onRewarded: function () { Ya.send("OnRewarded"); },
            onClose: function () { Ya.send("OnRewardedClose", "1"); },
            onError: function (e) { console.warn("[YandexSdk] rewarded error", e); Ya.send("OnRewardedClose", "0"); },
          },
        });
      } catch (e) { console.warn(e); Ya.send("OnRewardedClose", "0"); }
    });
  },

  // Leaderboard score (the victory's time). Needs an authorized player: the SDK refuses otherwise, which is fine —
  // nothing is sent. Current API: ysdk.leaderboards.setScore; older SDKs: getLeaderboards().setLeaderboardScore.
  YaSubmitScore: function (namePtr, score) {
    var name = UTF8ToString(namePtr);
    Ya.sdk().then(function (s) {
      if (!s) { Ya.send("OnScoreSubmitted", "0"); return; }
      var done = function () { Ya.send("OnScoreSubmitted", "1"); };
      var fail = function (e) { console.warn("[YandexSdk] setScore", e); Ya.send("OnScoreSubmitted", "0"); };
      try {
        if (s.leaderboards && s.leaderboards.setScore) {
          var check = s.isAvailableMethod ? s.isAvailableMethod("leaderboards.setScore") : Promise.resolve(true);
          check.then(function (ok) {
            if (!ok) { Ya.send("OnScoreSubmitted", "0"); return; }
            s.leaderboards.setScore(name, score).then(done, fail);
          }, fail);
        } else if (s.getLeaderboards) {
          s.getLeaderboards().then(function (lb) { return lb.setLeaderboardScore(name, score); }).then(done, fail);
        } else Ya.send("OnScoreSubmitted", "0");
      } catch (e) { fail(e); }
    });
  },

  // Cloud save, read once at start: the player (a guest too — scopes: false asks for nothing) and their "meta" key,
  // the game's progress as a JSON string. Answers OnCloudLoaded with that string ("" = nothing saved yet) or "!" when
  // the cloud cannot be used (no SDK, an error): the game then keeps its progress on this device only.
  YaCloudLoad: function () {
    Ya.sdk().then(function (s) {
      if (!s || !s.getPlayer) { Ya.send("OnCloudLoaded", "!"); return; }
      var fail = function (e) { console.warn("[YandexSdk] cloud load", e); Ya.send("OnCloudLoaded", "!"); };
      try {
        s.getPlayer({ scopes: false }).then(function (p) {
          Ya.player = p;
          return p.getData(["meta"]);
        }).then(function (data) {
          Ya.send("OnCloudLoaded", data && typeof data.meta === "string" ? data.meta : "");
        }, fail);
      } catch (e) { fail(e); }
    });
  },

  // Cloud save: the whole progress (JSON) under "meta". The platform batches the writes (flush: false).
  YaCloudSave: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    if (!Ya.player || !Ya.player.setData) return;
    try {
      Ya.player.setData({ meta: json }, false).then(function () { Ya.send("OnCloudSaved", "1"); },
        function (e) { console.warn("[YandexSdk] cloud save", e); Ya.send("OnCloudSaved", "0"); });
    } catch (e) { console.warn("[YandexSdk] cloud save", e); }
  },

  // The top of a leaderboard with the player's own entry; answers with JSON (or "" when there is nothing to show).
  YaGetLeaderboardEntries: function (namePtr, count) {
    var name = UTF8ToString(namePtr);
    Ya.sdk().then(function (s) {
      if (!s) { Ya.send("OnLeaderboardEntries", ""); return; }
      var options = { quantityTop: count, includeUser: true, quantityAround: 0 };
      var fail = function (e) { console.warn("[YandexSdk] getEntries", e); Ya.send("OnLeaderboardEntries", ""); };
      var toJson = function (res) {
        var entry = function (e) {
          var p = e.player || {};
          var avatar = "";
          try { avatar = p.getAvatarSrc ? p.getAvatarSrc("small") : ""; } catch (x) { }
          return { rank: e.rank || 0, name: p.publicName || "", avatar: avatar || "", score: e.score || 0 };
        };
        var out = { available: true, entries: [], userRank: res.userRank || 0 };
        (res.entries || []).forEach(function (e) { out.entries.push(entry(e)); });
        Ya.send("OnLeaderboardEntries", JSON.stringify(out));
      };
      try {
        if (s.leaderboards && s.leaderboards.getEntries) s.leaderboards.getEntries(name, options).then(toJson, fail);
        else if (s.getLeaderboards)
          s.getLeaderboards().then(function (lb) { return lb.getLeaderboardEntries(name, options); }).then(toJson, fail);
        else Ya.send("OnLeaderboardEntries", "");
      } catch (e) { fail(e); }
    });
  },
};

autoAddDeps(YandexSdkLib, "$Ya");
mergeInto(LibraryManager.library, YandexSdkLib);
