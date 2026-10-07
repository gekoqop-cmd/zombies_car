# Тексты для консоли Яндекс Игр

## Общее (черновик)
- Версия: 1.0.0 → **1.1.0**
- Языки: русский, английский (уже стоят)

### Комментарий разработчика (меняется первый абзац, добавлен абзац про лидерборды)
Игра на русском и английском языках: язык берётся из environment.i18n.lang, игрок может сменить его переключателем RU / EN в главном меню (выбор запоминается). Поддерживаются компьютеры, Android и iOS в альбомной ориентации. Управление описано в поле «Как играть» и в обучении первого забега. Полный забег — 30 волн и три босса (10, 20 и 30 волна), есть дневная и ночная версии карты и три уровня сложности.

Сохранения (п. 1.9): общий прогресс (монеты, скины, рекорды, достижения, ежедневные бонусы, раскладка джойстиков) сохраняется сразу после изменения — на устройстве и через 2 с в облаке Яндекса (player.setData, ключ meta), при сворачивании вкладки — немедленно. Внутри забега на «Лёгком» и «Среднем» прогресс сохраняется чекпоинтами после боссов 10-й и 20-й волны (продолжение с 11-й или 21-й волны). «Хардкор» по замыслу проходится без сохранений, игрок предупреждается об этом перед стартом.

SDK Яндекс Игр: LoadingAPI.ready() при показе главного меню; GameplayAPI.start()/stop(); пауза игры и звука при потере фокуса, по game_api_pause и во время рекламы; полноэкранная реклама только в логических паузах (перед картой и при выходе в меню); реклама за вознаграждение — только по кнопке с явной подписью, награда выдаётся в onRewarded и является дополнительным бонусом. Кейсы со скинами открываются за внутриигровые монеты или за просмотр рекламы; покупок за реальные деньги нет.

Лидерборды (тип time): TimeEasy, bestTimeNormal (основной), timeHardcore — время прохождения всех 30 волн на соответствующей сложности; результат отправляется только после победы и только для авторизованного игрока.

В релизной сборке нет отладочной панели и читов, внешних ссылок и сторонней рекламы.

---

## Русский

**Название:** Rustwave: Выживи любой ценой (без изменений)

**Описание для SEO:** Зомби-экшен на выживание: защитите машину от 30 волн, прокачайте её в мастерской и победите трёх боссов. Три уровня сложности, испытайте себя!

**Короткое описание:** Защитите машину от 30 волн зомби и сокрушите трёх боссов. (без изменений)

**Об игре:**
Rustwave: динамичный top-down экшен о выживании на заброшенной городской стоянке. Защищайте свою машину от 30 волн зомби и сразитесь с тремя боссами: Маткой, Тараном и Жнецом.

Стреляйте из автомата, экономьте патроны, а в ближнем бою берите топор: быстрые удары или мощный заряженный удар. Получайте опыт и на каждом уровне выбирайте одну из трёх карточек улучшений. Между волнами прокачивайте машину в мастерской: броню, корпус, шипы, электроразряд и оружейный ящик.

Выберите сложность: на «Лёгком» и «Среднем» прогресс сохраняется после боссов 10-й и 20-й волны, а «Хардкор» нужно пройти за один раз. Лучшее время попадает в таблицу лидеров.

Взрывайте красные бочки, ставьте мины, сражайтесь днём и ночью. За забеги вы получаете монеты на кейсы со скинами автомата, топора и пуль, а за ежедневный вход бонусы. Сможете ли вы спасти город?

**Как играть:**
Цель: пережить 30 волн и не дать зомби уничтожить героя или машину. Убивайте врагов, собирайте металлолом, между волнами улучшайте машину в мастерской и пополняйте патроны у автомобиля. Красные бочки взрываются и наносят урон по области.

На компьютере:
• WASD: движение.
• Мышь: прицел, ЛКМ: стрельба.
• Топор: клик ЛКМ для быстрого удара, зажмите и отпустите для мощного.
• R: перезарядка.
• Q или колесо мыши: смена оружия; 1 / 2: автомат / топор; 3: аптечка.
• E: мастерская и патроны у машины, в перерыве можно пропустить перерыв.

На телефоне и планшете: левый джойстик для движения, правый для прицела и огня, быстрый тап по нему стреляет в ближайшего зомби. С топором зажмите правый джойстик, наведите и отпустите для мощного удара. Кнопки справа: перезарядка, смена оружия и действие.

---

## English

**Title:** Rustwave: Survive to the end! (unchanged)

**SEO description:** Zombie survival action: defend your car from 30 waves, upgrade it in the workshop and defeat three bosses. Three difficulty levels to test yourself!

**Short description:** Defend your car from 30 zombie waves and crush three bosses.

**About the game:**
Rustwave is a fast top-down survival shooter set in an abandoned city parking lot. Defend your car from 30 waves of zombies and take on three bosses: the Broodmother, the Ram and the Reaper.

Shoot your rifle, save your ammo, and grab the axe up close: quick swings or a powerful charged blow. Earn experience and pick one of three upgrade cards every level. Between waves, upgrade your car in the workshop: armor, hull, spikes, electric shock and an ammo crate.

Choose your difficulty: on Easy and Normal your progress is saved after the bosses of waves 10 and 20, while Hardcore has to be beaten in one go. Your best time goes on the leaderboard.

Blow up red barrels, lay mines, fight by day and by night. Every run earns coins for cases with rifle, axe and bullet skins, and daily logins bring bonuses. Can you save the city?

**How to play:**
Goal: survive 30 waves and don't let the zombies destroy your hero or your car. Kill enemies, collect scrap, upgrade the car in the workshop between waves and refill ammo at the car. Red barrels explode and damage everything around them.

On a computer:
• WASD: move.
• Mouse: aim, left button: shoot.
• Axe: click for a quick hit, hold and release for a heavy one.
• R: reload.
• Q or mouse wheel: switch weapons; 1 / 2: rifle / axe; 3: medkit.
• E: workshop and ammo at the car; during a break, skip the break.

On a phone or tablet: the left joystick moves, the right one aims and shoots; a quick tap on it shoots the nearest zombie. With the axe, hold the right joystick, aim and release for a heavy blow. Buttons on the right: reload, switch weapons and action.

---

## Лидерборды (3 новых)
| Техническое название | RU | EN | Основной | Тип | Сортировка |
|---|---|---|---|---|---|
| bestTimeNormal | Лучшее время · Средний | Best time · Normal | да | time | по возрастанию |
| TimeEasy | Лучшее время · Лёгкий | Best time · Easy | нет | time | по возрастанию |
| timeHardcore | Лучшее время · Хардкор | Best time · Hardcore | нет | time | по возрастанию |

## Файлы (загрузить вручную: встроенный браузер не умеет прикреплять файлы)
- Архив: `unity/Builds/Rustwave_yandex.zip`
- Скриншоты, русские: десктопные `Promo/store/desktop/RU_*`, мобильные `Promo/store/RU_*`
- Скриншоты, английские: десктопные `Promo/store/desktop/EN_*`, мобильные `Promo/store/EN_*`
- Горизонтальное видео (обязательно): `Promo/Rustwave_trailer_v3.mp4` (снято на старой версии)
