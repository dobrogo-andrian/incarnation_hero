# Прогрес і контекст між сесіями

Цей файл читають на початку кожної сесії. Він замінює повторну перевірку вже зробленого: якщо пункт позначено ✅, вважаємо його виконаним і не перевіряємо знову. Перевіряємо лише тоді, коли щось зламалося.

Правила ведення:

- Оновлюємо в кінці кожної сесії, у тому ж коміті, що й робота.
- Тут записуємо стан і неочевидний контекст. Правила гри зберігаються в GDD, архітектура — у техдоку, історія — у git. Не дублюємо їх тут, а посилаємося.
- Коротко. Застаріле видаляємо, а не накопичуємо.

## Зараз

**Етап:** 1 «Макропетля, MVP-0» ([tech 9](tech/09-roadmap.md)). Етап 0 закрито 2026-10-26.
**Наступний крок:** прорив із прогнозом (одна функція), приріст тривалості життя (§4.5, tech 4.4). Перед початком узгодити обсяг з автором (форма з варіантами).

## Етап 1: чекліст

Порядок — за залежностями; уточнюємо по ходу.

| Пункт | Стан | Де |
|---|---|---|
| Модель часу: активність із тривалістю або «до кінця», переривання, старіння, тривалість життя, смерть → RunEnd | ✅ | `TimeSystem`, `LifeSystem`, `ActivitySystem`; D21–D24 |
| Персонаж: стати, аспекти, ресурси; єдина система модифікаторів із джерелами | ✅ | `CharacterRules`, `ModifierSystem`, `content/character/`; D25–D28 |
| Ручна карта 5–6 зон, подорож; місця медитації з резервом ці | ✅ | `content/world/region.yaml`, `WorldMap`, `WorldSystem`; D29 |
| Медитація: очищення (чистота), відновлення, витрата й виснаження резерву місця | ✅ | `MeditationSystem`, `content/cultivation/meditation.yaml`; D30 |
| А. Бафи (осяяння, енергія пігулки) і засвоєння → шкала етапу | ✅ | `CultivationSystem`, `meditation.assimilation`; D31 |
| Б. Пігулки, токсини, камені (мінімум), крамниця хабу | ✅ | `ItemSystem`, `EconomySystem`, `content/items/`, `balance/economy.yaml`; D32–D33, A10–A11 |
| Фоліанти й осягнення (знання — частина фундаменту), Сприйняття від вивчення | ✅ | `KnowledgeSystem`, `content/knowledge/`, `items/folios.yaml`; D34 |
| Прорив із прогнозом (одна функція), приріст тривалості життя | ⬜ | §4.5, tech 4.4 |
| Загальні травми і лікування | ⬜ | §8, tech 4.6 |
| Події (20–30) і режим EventDialog; типізовані умови й ефекти; базові чутки | ⬜ | §10, tech 4.3 |
| Формульний розв'язувач бою за інтерфейсом бою | ⬜ | tech 4.1 |
| Sim.Cli: `--runs`, JSONL, 2–3 боти; 1000 забігів за хвилини | ⬜ | tech 6.1 |
| Простий UI в Godot для повного забігу | ⬜ | tech 7 |
| Перші Python-звіти | ⬜ | tech 6.2 |
| Плейтест гіпотези «тиск часу створює цікаві рішення» (автор + 2–3 тестери) | ⬜ | tech 9 |

Останній прогін: `dotnet test` дав 145/145 зелених (2026-10-26).

Критерії готовності етапу 1: повний забіг від 12 років до смерті проходиться в простому UI; 1000 headless-забігів за хвилини; звіти показують різницю між стратегіями ботів; автор і 2–3 тестери відповіли на гіпотезу. Якщо ні — правимо макропетлю до бою.

**Етап 0 ✅ (2026-10-26):** RNG, GameState/команди/PlayerView, ironman-сейв, YAML-завантажувач і валідатор, ключі локалізації, `Sim.Cli validate|run`, golden-тест, Godot-оболонка (підтверджено в редакторі).

## Що вже є в коді (щоб не перечитувати)

- `RngStream`: Worldgen, Events, Rumors, Npc, Loot, Combat, Ai, Life (нові потоки — лише в кінець). Стани всіх потоків записуються в `GameState.RngStates` після кожної команди.
- Команди (Overworld): `StartActivity(activity, weeks?)`, `ContinueActivity`, `CancelActivity`, `SetInterruptStops(categories)`. Команда з чужого режиму відхиляється з `RejectReasons.WrongMode`; у RunEnd відхиляється все.
- Активності — records від `Activity` у `State/Activities.cs` (JSON-дискримінатор `kind`), поки лише `WaitActivity`. Нова активність: record + `[JsonDerivedType]` там же + гілка в `ActivitySystem` і назва в `SimText.ActivityName`.
- Тиждень: `TimeSystem.RunActivity` крутить тижні (тиждень/вік → `LifeSystem` → `ActivitySystem`), доки активність не скінчиться, не спрацює переривання із `StoppingInterrupts` або забіг не завершиться. Переривання — SimEvent від `InterruptEvent` з `Category`. Кінець забігу — лише через `RunEndSystem.End`.
- Події: `WeekPassed`, `ActivityFinished`, `ActivityCancelled`, `LifeEndApproaching`, `RunEnded`.
- `Simulation.NewGame(content, seed)` / `Simulation.Resume(content, state)` — точки входу для UI, тестів і Sim.Cli. Коефіцієнти система бере з `config.run` через `ContentDb`.
- `SaveSerializer.HashState(state)` — стабільний хеш стану для golden-тестів.
- Один культиваційний трек `qi`, константа `Simulation.QiVectorId`.
- Контент: `ContentLoader.Load(IEnumerable<ContentFile>)` → `ContentDb` + список `ContentError(file, line, msg)`. Ядро не читає диск саме (`ContentFiles.FromDirectory` — хелпер для тестів/CLI), тож Godot подасть `res://`-файли так само. Тип об'єкта = перший сегмент ID (`vector`, `stage`), реєстр `TypesByKind` у `ContentLoader`. Файл — одна мапа або список мап. Невідомі поля — помилка. Зараз є лише `vector.qi` і `stage.qi.1..4` у `content/cultivation/qi_stages.yaml`.
- Валідатор: посилання позначаються атрибутом `[ContentRef(typeof(T))]` на властивості def-класу (`string` або `List<string>`); `ContentValidator` знаходить їх рефлексією і перевіряє існування й тип. Також перевіряє непорожній `uk` у кожному `LocalizedText`. Вкладені блоки (класи з `Core.Content`, не `ContentDef`) перевіряються рекурсивно, шлях у помилці — `'item.f'.Folio.Subject`. `Sim.Cli validate <dir>` повертає 0/1/2 (ок / помилки / неправильні аргументи).
- Локалізація: `LocKey(Key, Args)` у `Core.Loc`; `Localizer.FromContent(db).Resolve(key)` підставляє `{name}` (числа, рядки або вкладені `LocKey`). Відсутній ключ повертає сам ключ. Таблицю ключів будує `LocTable.FromContent`. Тест `EveryLocKeyDeclaredInCore_HasTextInRepoContent` ловить статичні `LocKey` у Core без тексту в контенті.
- Баланс забігу: `config.run` (`content/balance/run.yaml`, клас `RunConfigDef`): `start_age_years`, `start_lifespan_years` (§3), `weeks_per_year` (D16), `natural_death_window_years` (D21), `life_end_warning_years` (D24), списки `stats` і `resources` (склад і порядок показу), `start_aspect_profile`.
- Персонаж: контент `stat.*`, `aspect.*`, `aspect_profile.*`, `resource.*` (`content/character/`, класи в `Content/CharacterDefs.cs`). У стані — базові стати, поточні ресурси, `AspectProfileId`, `Modifiers`. `CharacterRules` (`Core/Character/`, екземпляр — `Simulation.Rules`) рахує ланцюжок стат → аспект → максимум ресурсу: `Value(player, id)` / `Explain(player, id)` → `StatBreakdown` з базою і внеском кожного модифікатора (для підказок). Правило модифікаторів — `CharacterRules.ApplyModifiers` (D28). `ModifierSystem.Apply` замінює модифікатор того ж джерела на ту ж ціль; тривалість тікає у світовій фазі тижня. PlayerView має `Stats` (з аспектами) і `Resources` (поточне + розкладка максимуму).
- Світ: контент `zone.*` (небезпека), `route.*` (двобічний, тижні), `place.*` (зона, щільність, `capacity`, `regen_per_week`), `config.run.start_zone`. Стан — `Player.ZoneId` і `World.Places` (резерв кожного місця). `WorldMap` (`Core/World/`, екземпляр — `Simulation.Map`): `FindRoute`, `ReachableFrom`. Команда `Travel(zoneId)` → `TravelActivity(Legs)`; `ActivitySystem` ставить гравця в зону, коли минули тижні її етапу, і шле `ZoneEntered`. `StartActivity` відхиляє активності з власною тривалістю (`HasPlayerChosenLength`). `WorldSystem` щотижня відновлює резерви. PlayerView: `Zone` і `Places` поточної зони.
- Медитація: контент `meditation.*` (швидкість, ціль — стат або ресурс), `MeditateActivity(direction, place)` через `StartActivity`; `ActivitySystem.Validate` перевіряє напрям і місце в поточній зоні. `MeditationSystem` бере щільність з резерву, рахує ефективність із підлогою `config.run.meditation_depleted_floor`, шле переривання `PlaceQiLow`. Чистота — `stat.purity`; `resource.qi_pool` росте з нею (`source`) і сам відновлюється (`regen_per_week`, `ResourceSystem`). Порядок у світовій фазі: модифікатори → ресурси → місця; медитація — у фазі активності.
- `SimContext` (контент, `config.run`, `CharacterRules`, `WorldMap`, RNG) — один на `Simulation`, його отримують системи.
- Double у стані — лише через `StateNumbers.Round` (4 знаки).
- Валідатор: `[ContentRef]` на словнику перевіряє ключі; власні правила типу — інтерфейс `IValidatedDef` (профіль аспектів: частки стату дають 1; джерело ресурсу — стат або аспект).
- `Localizer`: аргумент-список `LocKey` розв'язується і з'єднується через кому.
- `SimText.ViewLines(view, db)` і `SimText.Describe(event)` → `LocKey`. Їх використовують Sim.Cli і Godot-оболонка (`game/scripts/Main.cs`), тексти `str.view.*`, `str.event.*`, `str.mode.*`, `str.activity.*`, `str.end.*`.
- `Sim.Cli run --seed N [--travel zone] [--weeks W|end] [--meditate direction] [--content dir]` спершу подорожує, потім чекає або медитує в першому місці зони; друкує помітні події з тижнем, PlayerView і `state hash`; переривання сам продовжує. Службові повідомлення йдуть у stderr, тож stdout чистий.
- Golden: `GoldenTests.TravelToPeakThenPurifyUntilDeath_Seed42` і `TravelToPeakTakeTwoPillsThenAssimilateUntilDeath_Seed42`, хеші — константи в тестах; ті самі хеші дають відповідні команди `sim run` (наведено в doc-коментарях тестів).
- `RunSession.OpenOrStart(store, content, newSeed)` — ironman-забіг: продовжує сейв або починає новий (пошкоджений чи несумісний сейв замінюється новим забігом, `Notice` пояснює гравцю). `Execute` зберігає після кожної прийнятої команди до повернення результату; відхилена команда не пише. `TryStartNewRun()` — новий забіг лише після RunEnd.
- Тести: `TestContent.Repo` (контент репо, завантажений раз), `TestContent.WithExtra(yaml)` (репо + додатковий файл), `TestContent.Wait(weeks)`; корінь репо — `RepoPaths`.
- Бафи (D31): `PlayerState.Buffs` (≤ 1 на `BuffKind`, порядок enum = порядок витрачання). `CultivationSystem`: `GrantCombatInsight` (більше з двох), `AddPillEnergy` (сумується), згасання осяяння у світовій фазі (після модифікаторів), `Assimilate`. Засвоєння — `meditation.*` з `target: vector.*`; `MeditationSystem.Validate` відхиляє без бафу або при повній шкалі. `StageProgress` — double; розмір шкали — `stage.*.progress_required`. Переривання `BuffExhausted`, `StageScaleFull`. Джерела осяяння ще немає (буде з формульного бою), тож поки його видають лише тести.
- Предмети й економіка (D32–D33): `PlayerState.Stones`, `Items`, `PillsTaken` (`ItemCount`, порядок за ID; `ItemSystem.Change` — єдиний спосіб змінити). Миттєві команди (без плину часу, заняття не скасовують): `UsePill`, `Buy`, `RestoreQiWithStones`, `RefillPlaceWithStones`; кожна обробляється однією функцією системи, що повертає `CommandOutcome`. Токсини — звичайний ресурс зі `start_empty: true`, на який вказує `config.run.toxins_resource`; напрям медитації може мати `purges`/`purge_ratio`. `ResourceSystem.Drain` — зняти ресурс. Одноразові конфіги з власним типом реєструються в `ContentLoader.TypesById` (`config.economy`). PlayerView: `Stones`, `Items` (з енергією наступної пігулки), `ShopOffers` поточної зони. `sim run --pills a,b` приймає пігулки після подорожі.
- Знання (D34): `PlayerState.Knowledge` (`KnowledgeLevel`, порядок за ID). `KnowledgeDef` для видів `tech`/`element`/`method`. `MeditateActivity.SubjectId` — фоліант або стихія лише для напряму зі `studies: true`, іншим напрямам предмет заборонено. `KnowledgeSystem.CannotStudy`/`Study`; `CultivationSystem.FoundationOf` → `Foundation(Knowledge, Purity)` — прорив братиме `Total` звідси. `StatDef.DiminishScale` і `side_gains` напряму працюють через `MeditationSystem.GainStat`. Переривання `StudyCapReached`, подія `KnowledgeLevelUp`. `sim run --meditate meditation.comprehension --subject element.fire`.
- Сейв `schema_version` = 8 (міграцій немає: старіші сейви несумісні → новий забіг). Піднімати версію, коли стан отримує поля, без яких ядро не працює.

## Пастки і неочевидне

Постійні правила (TFM `net8.0`, два `.sln`, процес «перевірка → коміт → пуш у main») записано в `CLAUDE.md`, тут їх не повторюємо.

- **Назви етапів ці — заглушки** («Перший етап ці»…): GDD §4 їх не називає. Замінити, коли автор дасть назви.
- **`/content` лежить поза `game/`**: для експорту Godot його доведеться пакувати (етап 5). У dev-збірці читаємо з `../content`.
- **`StageIndex` — 0-базний індекс у `stages` вектора** (0 = «Перший етап ці»). GDD не каже, чи 12-річний персонаж уже на першому етапі чи «до» нього; це тимчасове рішення, легко змінити. Підняти з автором.
- **Godot**: `C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe --headless --path game --quit-after 30` — швидка перевірка, що сцена стартує. Сейв гри — `%APPDATA%\Godot\app_userdata\incarnation_hero\run.json`. У `game/incarnation_hero.csproj` увімкнено `ImplicitUsings` і `Nullable`, як в інших проєктах. Сид нового забігу береться в оболонці з `RandomNumberGenerator` (Core годинник і ОС не читає).
- **Баланс медитації — чернетка.** Очищення в Духовній жилі все життя дає чистоту ≈ 262 і пул ці ≈ 624 (golden-забіг): приріст лінійний і без стелі. Перевірити звітами Sim.Cli і, можливо, ввести спадний приріст або стелю від етапу — питання до автора на етапі балансу.
- **Засвоєння без бафу й осягнення на стелі тривають вхолосту**: приходить переривання, але заняття не завершується саме. `sim run --weeks end` продовжує його до смерті. Можна завершувати засвоєння автоматично; вирішити разом з UI етапу 1.
- **Атомарний сейв** зроблено через `File.Replace` (temp → slot, попередній файл стає `.bak`). Бекап використовується лише тоді, коли не зійшлася контрольна сума основного файлу.

## Відкриті питання, що стосуються поточного етапу

- Підтвердити з автором припущення A10 (поріг небезпеки для каменів) і A11 (лічильник пігулок не скидається).
- Q7 (баланс каменів) лишається відкритим: числа в `economy.yaml`, `items/pills.yaml` і `progress_required` етапів — чернетка до звітів Sim.Cli.

## Журнал сесій

Одна-дві строки на сесію, нові зверху.

- **2026-10-26** — фоліанти й осягнення: знання з рівнями, базове відчуття стихій, книгарня хабу, фундамент як число, Сприйняття від медитації зі спадним приростом, рекурсивна перевірка вкладених блоків у валідаторі (D34); сейв v8.
- **2026-10-26** — пігулки зі спадною користю за типом, токсини як ресурс із порогом, очищення виводить токсини першими, камені (пул ці, резерв місця), крамниця хабу (D32–D33, A10–A11); другий golden; сейв v7.
- **2026-10-26** — бафи культивації й засвоєння в шкалу етапу (D31, Q5 закрито); узгоджено обсяг частини Б (пігулки зі спадною користю, токсини, камені, крамниця); сейв v6.
- **2026-10-26** — медитація: очищення й відновлення, витрата резерву з підлогою ефективності, переривання `PlaceQiLow` (D30), чистота як стат, `SimContext`; осягнення винесено в окремий пункт із фоліантами; сейв v5.
- **2026-10-26** — світ: ручна карта (хаб + 5 зон), `Travel` з автомаршрутом, місця медитації з резервом, що відновлюється (D29); `sim run --travel`; сейв v4.
- **2026-10-26** — персонаж: 5 статів, аспекти (D25), ресурси з лінійних формул (D27), єдина система модифікаторів з розкладкою (D28), `IValidatedDef` у валідаторі; сейв v3.
- **2026-10-26** — модель часу: активності, переривання з налаштуванням зупинок, тривалість життя, природна смерть у вікні ризику, RunEnd (D21–D24). Оболонка: «Чекати до переривання», «Новий забіг».
- **2026-10-26** — Godot-оболонка підтверджена в редакторі; етап 0 закрито, складено чекліст етапу 1.
- **2026-10-26** — `RunSession` (автосейв після команди, продовження/новий забіг) у Core; Godot-оболонка написана.
- **2026-10-26** — `Sim.Cli run` (PlayerView через `Localizer` + хеш), `config.run` у YAML, `PlayerViewText`, перший golden-тест. Узгоджено обсяг Godot-оболонки.
- **2026-10-26** — ключі локалізації: `LocKey` з параметрами, `Localizer`, системні тексти `str.*` у YAML (D20).
- **2026-10-26** — валідатор контенту (`ContentValidator`, `[ContentRef]`) і перша команда `Sim.Cli validate`.
- **2026-10-26** — закрито розбіжності в документах з автором: D16–D19 (52 тижні; камені — валюта; миттєве відновлення пулу каменями; камені не підсилюють місця), A8 (один ґатунок каменів), A9 (множина без узгодження з числом). Q1 закрито, Q7 і Q10 звужено. Виправлено посилання на неіснуючі A8/Q13/Q14, `ai-guidelines.md`, «розд. 12.3».
- **2026-10-26** — план закриття етапу 0 (5 кроків). Зроблено YAML-завантажувач (YamlDotNet 16.3.0): `ContentDb`, збір усіх помилок з рядками, перший контент `qi_stages.yaml`.
- **2026-10-26** — додано кореневий `README.md` і цей файл. У `CLAUDE.md` з'явився розділ Workflow (перевірка → `progress.md` → коміт → пуш у `main`), зайве з нього прибрано. Ізоляцію worktree вимкнено (`.claude/settings.json`): усі сесії працюють прямо в `main`.
- **до 2026-10-26** — scaffold, RNG, GameState/команди/PlayerView, ironman-сейв, конвертація .docx у Markdown (див. `git log`).
