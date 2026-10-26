# incarnation_hero

Покроковий рогалик у жанрі уся/сяня (робоча назва). Герой культивує ці, тренується, збирає знання й бореться на гексагональній сітці. Головний ресурс — час: 1 хід дорівнює тижню, і герой старіє. Після смерті в наступну реінкарнацію переходить лише резонанс душі.

Соло-розробка. Ціль — публічне демо в Steam.

## Статус

Йде етап 0 («Фундамент»). Що зроблено і що робити далі, див. у [docs/progress.md](docs/progress.md).

## Стек

- **Godot 4.7.2 .NET**, лише C#. Godot відповідає тільки за UI, бойову сцену, ввід і звук.
- **`src/Core`** — чиста C#-бібліотека без залежностей від Godot: правила, стан, системи, RNG, завантаження контенту. Одне й те саме ядро працює в грі, у тестах і в Sim.Cli.
- Контент зберігається в YAML (YamlDotNet), сейви — у JSON (System.Text.Json) зі `schema_version` і контрольною сумою.
- Тести: xUnit із фіксованими сидами.
- Python (pandas, Jupyter) — лише для аналізу балансу в `/analysis`.

Усі проєкти таргетують `net8.0`, бо цього вимагає Godot.

## Структура

```
src/Core          ядро симуляції (без Godot)
src/Core.Tests    xUnit-тести
src/Sim.Cli       headless-прогони ботами, експорт метрик
game/             Godot-проєкт (презентаційний шар)
docs/             дизайн-документи (українською)
  gdd/            GDD — джерело правди для правил гри (§N)
  tech/           технічний документ: архітектура, роадмап, рішення D*/A*, питання Q*
  progress.md     стан робіт і контекст між сесіями
sources/archive/  оригінальні .docx (лише для читання)
```

Заплановано: `content/` (YAML), `loc/` (таблиці локалізації), `analysis/` (ноутбуки), `src/Tools.Validate`.

## Команди

```
dotnet build IncarnationHero.sln
dotnet test IncarnationHero.sln
dotnet test src/Core.Tests --filter "FullyQualifiedName~RngTests"
dotnet run --project src/Sim.Cli -- <args>
```

Godot-проєкт відкривається з `game/project.godot`. Редактор збирає його через власний `game/incarnation_hero.sln`, а Core підтягується через `ProjectReference`.

## Документація

- [docs/README.md](docs/README.md) — індекс GDD і техдоку.
- [docs/tech/09-roadmap.md](docs/tech/09-roadmap.md) — етапи 0–5 і критерії готовності.
- [docs/tech/decisions.md](docs/tech/decisions.md) і [docs/tech/open-questions.md](docs/tech/open-questions.md) — ухвалені рішення та відкриті питання.
- [CLAUDE.md](CLAUDE.md) — архітектурні правила й інструкції для ШІ-асистента.
