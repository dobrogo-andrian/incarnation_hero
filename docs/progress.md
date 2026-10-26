# Прогрес і контекст між сесіями

Цей файл читають на початку кожної сесії. Він замінює повторну перевірку вже зробленого: якщо пункт позначено ✅, вважаємо його виконаним і не перевіряємо знову. Перевіряємо лише тоді, коли щось зламалося.

Правила ведення:

- Оновлюємо в кінці кожної сесії, у тому ж коміті, що й робота.
- Тут записуємо стан і неочевидний контекст. Правила гри зберігаються в GDD, архітектура — у техдоку, історія — у git. Не дублюємо їх тут, а посилаємося.
- Коротко. Застаріле видаляємо, а не накопичуємо.

## Зараз

**Етап:** 0 «Фундамент» ([tech 9](tech/09-roadmap.md)).
**Наступний крок:** підтвердити Godot-оболонку в редакторі. Код написано (`game/scenes/main.tscn`, `game/scripts/Main.cs`), headless-запуск проходить без помилок і пише автосейв, але файли `game/` ще не закомічено: чекають підтвердження автора. Після нього закомітити їх, позначити пункт ✅ і закрити етап 0.

## Етап 0: чекліст

| Пункт | Стан | Де |
|---|---|---|
| Структура репо, рішення, Godot-проєкт, `net8.0` | ✅ | `IncarnationHero.sln`, `game/` |
| RNG: SplitMix64 → xoshiro256**, потоки | ✅ | `src/Core/Rng/` |
| GameState, Command/SimEvent, FSM режимів, PlayerView | ✅ каркас | `src/Core/State/`, `src/Core/Sim/` |
| Сейв: JSON, `schema_version`, sha256, атомарна заміна, бекап | ✅ | `src/Core/Save/` |
| Завантажувач YAML-контенту | ✅ | `src/Core/Content/`, `content/` |
| Валідатор (биті посилання) | ✅ | `ContentValidator` у Core + `Sim.Cli validate <dir>` |
| Ключі локалізації (Core повертає ключ і параметри) | ✅ | `src/Core/Loc/`, `content/strings/` |
| Godot-оболонка: показує PlayerView, надсилає одну команду | 🟡 чекає перевірки в редакторі | `RunSession` у Core ✅; `game/scripts/Main.cs` |
| Sim.Cli на тому ж ядрі | ✅ | `Sim.Cli validate`, `Sim.Cli run`; golden-тест `GoldenTests` |

Останній прогін: `dotnet test` дав 66/66 зелених (2026-10-26).

Критерії готовності етапу 0: гра запускається і в Godot, і в Sim.Cli на одному ядрі; сейв переживає цикл save→load без змін; валідатор ловить биті посилання.

## Що вже є в коді (щоб не перечитувати)

- `RngStream`: Worldgen, Events, Rumors, Npc, Loot, Combat, Ai. Стани всіх потоків записуються в `GameState.RngStates` після кожної команди.
- Команд поки одна: `WaitWeeks(weeks)` (режим Overworld). Подія теж одна: `WeekPassed`. Команда з чужого режиму відхиляється з `RejectReasons.WrongMode`.
- `Simulation.NewGame(seed, settings)` / `Simulation.Resume(state)` — точки входу для UI, тестів і Sim.Cli.
- `SaveSerializer.HashState(state)` — стабільний хеш стану для golden-тестів.
- Один культиваційний трек `qi`, константа `Simulation.QiVectorId`.
- Контент: `ContentLoader.Load(IEnumerable<ContentFile>)` → `ContentDb` + список `ContentError(file, line, msg)`. Ядро не читає диск саме (`ContentFiles.FromDirectory` — хелпер для тестів/CLI), тож Godot подасть `res://`-файли так само. Тип об'єкта = перший сегмент ID (`vector`, `stage`), реєстр `TypesByKind` у `ContentLoader`. Файл — одна мапа або список мап. Невідомі поля — помилка. Зараз є лише `vector.qi` і `stage.qi.1..4` у `content/cultivation/qi_stages.yaml`.
- Валідатор: посилання позначаються атрибутом `[ContentRef(typeof(T))]` на властивості def-класу (`string` або `List<string>`); `ContentValidator` знаходить їх рефлексією і перевіряє існування й тип. Також перевіряє непорожній `uk` у кожному `LocalizedText`. Поки лише властивості верхнього рівня: вкладені структури (вузли подій) додамо разом із подіями. `Sim.Cli validate <dir>` повертає 0/1/2 (ок / помилки / неправильні аргументи).
- Локалізація: `LocKey(Key, Args)` у `Core.Loc`; `Localizer.FromContent(db).Resolve(key)` підставляє `{name}` (числа, рядки або вкладені `LocKey`). Відсутній ключ повертає сам ключ. Таблицю ключів будує `LocTable.FromContent`. Тест `EveryLocKeyDeclaredInCore_HasTextInRepoContent` ловить статичні `LocKey` у Core без тексту в контенті.
- Баланс забігу: `config.run` (`content/balance/run.yaml`, клас `RunConfigDef`): `start_age_years` (§3), `weeks_per_year` (D16). `NewGameSettings.FromContent(db)` будує налаштування нового забігу з нього.
- `PlayerViewText.Lines(view, db)` → список `LocKey`, по одному на рядок. Його використовують Sim.Cli і (далі) Godot, тексти `str.view.*`, `str.mode.*`.
- `Sim.Cli run --seed N --weeks W [--content dir]` друкує PlayerView і `state hash`. Службові повідомлення (кількість об'єктів, помилки) йдуть у stderr, тож stdout чистий.
- Golden: `GoldenTests.WaitTenYears_Seed42`, хеш — константа в тесті. Той самий хеш дає `sim run --seed 42 --weeks 520`.
- `RunSession.OpenOrStart(store, newGame)` — ironman-забіг: продовжує сейв або починає новий (пошкоджений чи несумісний сейв замінюється новим забігом, `Notice` пояснює гравцю). `Execute` зберігає після кожної прийнятої команди до повернення результату; відхилена команда не пише.
- Тести знаходять корінь репо через `RepoPaths` (шукає `IncarnationHero.sln`).

## Пастки і неочевидне

Постійні правила (TFM `net8.0`, два `.sln`, процес «перевірка → коміт → пуш у main») записано в `CLAUDE.md`, тут їх не повторюємо.

- **Назви етапів ці — заглушки** («Перший етап ці»…): GDD §4 їх не називає. Замінити, коли автор дасть назви.
- **`/content` лежить поза `game/`**: для експорту Godot його доведеться пакувати (етап 5). У dev-збірці читаємо з `../content`.
- **`StageIndex` — 0-базний індекс у `stages` вектора** (0 = «Перший етап ці»). GDD не каже, чи 12-річний персонаж уже на першому етапі чи «до» нього; це тимчасове рішення, легко змінити. Підняти з автором.
- **Godot**: `C:	ools\Godot_v4.7.2-stable_mono_win64\..._console.exe --headless --path game --quit-after 30` — швидка перевірка, що сцена стартує. Сейв гри — `%APPDATA%\Godotpp_userdata\incarnation_heroun.json`. У `game/incarnation_hero.csproj` увімкнено `ImplicitUsings` і `Nullable`, як в інших проєктах. Сид нового забігу береться в оболонці з `RandomNumberGenerator` (Core годинник і ОС не читає).
- **Атомарний сейв** зроблено через `File.Replace` (temp → slot, попередній файл стає `.bak`). Бекап використовується лише тоді, коли не зійшлася контрольна сума основного файлу.

## Відкриті питання, що стосуються поточного етапу

Немає. Відомих розбіжностей у документах теж немає (закрито 2026-10-26).

## Журнал сесій

Одна-дві строки на сесію, нові зверху.

- **2026-10-26** — `RunSession` (автосейв після команди, продовження/новий забіг) у Core; Godot-оболонка написана, чекає перевірки в редакторі.
- **2026-10-26** — `Sim.Cli run` (PlayerView через `Localizer` + хеш), `config.run` у YAML, `PlayerViewText`, перший golden-тест. Узгоджено обсяг Godot-оболонки.

- **2026-10-26** — ключі локалізації: `LocKey` з параметрами, `Localizer`, системні тексти `str.*` у YAML (D20).
- **2026-10-26** — валідатор контенту (`ContentValidator`, `[ContentRef]`) і перша команда `Sim.Cli validate`.
- **2026-10-26** — закрито розбіжності в документах з автором: D16–D19 (52 тижні; камені — валюта; миттєве відновлення пулу каменями; камені не підсилюють місця), A8 (один ґатунок каменів), A9 (множина без узгодження з числом). Q1 закрито, Q7 і Q10 звужено. Виправлено посилання на неіснуючі A8/Q13/Q14, `ai-guidelines.md`, «розд. 12.3».
- **2026-10-26** — план закриття етапу 0 (5 кроків). Зроблено YAML-завантажувач (YamlDotNet 16.3.0): `ContentDb`, збір усіх помилок з рядками, перший контент `qi_stages.yaml`.
- **2026-10-26** — додано кореневий `README.md` і цей файл. У `CLAUDE.md` з'явився розділ Workflow (перевірка → `progress.md` → коміт → пуш у `main`), зайве з нього прибрано. Ізоляцію worktree вимкнено (`.claude/settings.json`): усі сесії працюють прямо в `main`.
- **до 2026-10-26** — scaffold, RNG, GameState/команди/PlayerView, ironman-сейв, конвертація .docx у Markdown (див. `git log`).
