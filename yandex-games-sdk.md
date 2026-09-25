# Памятка: Яндекс Игры SDK (проект Frostline)

Источник: https://yandex.ru/dev/games/doc/ru/ · индекс: https://yandex.ru/dev/games/doc/ru/llms.txt


## Подключение и жизненный цикл
- `<script src="/sdk.js"></script>` (относительный путь при хостинге в Консоли) ДО вызова `YaGames.init()`.
- `const ysdk = await YaGames.init();` (`{signed:true}` — для серверной проверки покупок).
- `ysdk.features.LoadingAPI?.ready()` — когда все ресурсы загружены и игрок может играть (обязательно, п.1.19.2).
- `ysdk.features.GameplayAPI?.start()/stop()` — старт/возобновление уровня vs пауза, меню, конец уровня, показ рекламы, уход со вкладки (обязательно, п.1.19.3).
- События `ysdk.on('game_api_pause'|'game_api_resume', cb)` — выключать звук+геймплей. Платформа сама показывает fullscreen-рекламу на старте → не запускать звук/игру, пока пауза.
- Прочие: `ysdk.EVENTS.HISTORY_BACK` (ТВ), `ysdk.dispatchEvent(ysdk.EVENTS.EXIT)`, `ACCOUNT_SELECTION_DIALOG_OPENED/CLOSED` (перечитать данные игрока).

## Реклама (`ysdk.adv`)
- `showFullscreenAdv({callbacks:{onOpen,onClose(wasShown),onError}})` — только в логических паузах (между уровнями); частоту ограничивает платформа; НЕ по setInterval.
- `showRewardedVideo({callbacks:{onOpen,onRewarded,onClose,onError}})` — награду давать в onRewarded; кнопка с явным текстом «смотреть рекламу → награда»; награда — бонус, не условие продолжения игры.
- Стики-баннер: включается в Консоли; `getBannerAdvStatus/showBannerAdv/hideBannerAdv`.
- Во время рекламы — пауза звука и геймплея (п.4.7). Только реклама SDK, никаких сторонних баннеров.

## Игрок и сохранения
- `const player = await ysdk.getPlayer()` (лимит 20 запросов/5 мин — кэшировать). `player.isAuthorized()`, `ysdk.auth.openAuthDialog()` (только по действию игрока).
- `player.setData(obj, flush)` / `getData(keys)` — до 200 КБ; `setStats/incrementStats/getStats` — числа, до 10 КБ.
- `getName()`, `getPhoto('small'|'medium'|'large')`, `getUniqueID()`. `ysdk.getStorage()` — безопасный localStorage (потеря на iOS).
- Прогресс сохранять сразу, не терять при перезагрузке/повороте; гостевой режим с сохранением.

## Прочее API
- Лидерборды: `ysdk.leaderboards.setScore(name, score)` (1/сек, нужна авторизация, проверять `ysdk.isAvailableMethod('leaderboards.setScore')`), `getPlayerEntry`, `getEntries(name,{quantityTop,includeUser,quantityAround})`, `getDescription`.
- Покупки: `ysdk.payments.purchase({id})`, `getPurchases()`, `getCatalog()`, `consumePurchase(token)`; при старте обрабатывать неконсумированные покупки; валюта из SDK.
- `ysdk.environment.i18n.lang` — автоопределение языка (обязательно, п.2.14); `environment.payload`.
- `ysdk.deviceInfo.isMobile()/isDesktop()/isTablet()/isTV()`; `ysdk.screen.fullscreen.request()/exit()`.
- `ysdk.getFlags({defaultFlags})` — remote config; `ysdk.serverTime()` — для ежедневных наград; `ysdk.feedback.canReview()/requestReview()` (раз за сессию); `ysdk.shortcut`, `ysdk.clipboard.writeText`.

## Ключевые требования модерации
- Архив ≤100 МБ, `index.html` в корне, имена без пробелов/кириллицы; без абсолютных ссылок на S3 Яндекса; внешние хосты — через CSP в Консоли.
- Звук выключается при потере фокуса/сворачивании. Мобильные: полный экран, управление жестами, нет выделения текста/контекстного меню по долгому тапу, нет скролла страницы/swipe-to-refresh. Десктоп: мышь/клавиатура независимо от раскладки, без системных хоткеев, соотношение сторон ≤2:1.
- Корректный ресайз/поворот: ничего не обрезается и не перекрывается.
- Нужна монетизация (реклама или покупки). Нет ошибок в консоли, игра завершённая, >10 минут контента или реиграбельность, растущая сложность, управление описано.
- Контент: без политики, религии, реалистичного насилия, внешних ссылок, выигрыша реальных денег; интерактивный ИИ запрещён (заранее сгенерированные ИИ-ассеты можно). Тексты переведены на заявленные языки.
- Браузеры: Яндекс, Chrome, Firefox, Opera, Safari; Android 5+, iOS 9+.
- Рекомендуется: кнопка звука, пауза, обучение, e-mail для связи.

## Локальная разработка
- `npx @yandex-games/sdk-dev-proxy -p <папка игры> [--app-id=<ID>]` — проксирует /sdk.js; или черновик + `?game_url=https://localhost`.
- Debug-панель: `&debug-mode=16` в URL.
