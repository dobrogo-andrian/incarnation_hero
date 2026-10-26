# Документація: індекс

Джерело правди — Markdown у цій теці. Читай лише потрібні файли.
Позначення: `§N` — розділ GDD (`docs/gdd/`); у техдоку розділи без `§` («tech 2.8»). D/A — `tech/decisions.md`, Q — `tech/open-questions.md`, R — `tech/10-risks.md`.

Поточний стан робіт, наступний крок і контекст між сесіями: [progress.md](progress.md).

## GDD — WUXIA ROGUELIKE, дизайн-документ MVP (`gdd/`)

| Файл | Зміст |
|---|---|
| [00-passport.md](gdd/00-passport.md) | §0 pitch, стовпи дизайну, референси, мета MVP |
| [01-game-structure.md](gdd/01-game-structure.md) | §1 забіг, макроцикл, завершення забігу |
| [02-character.md](gdd/02-character.md) | §2.1–2.7 характеристики, ресурси, культиваційні параметри, передумови, вік, вектори |
| [03-time-lifespan.md](gdd/03-time-lifespan.md) | §3 час, старіння, стартовий вік і тривалість життя |
| [04-cultivation.md](gdd/04-cultivation.md) | §4.1–4.5 фундамент/етап, медитація, бафи, токсичність, прорив |
| [05-training.md](gdd/05-training.md) | §5 види тренувань, межа |
| [06-knowledge.md](gdd/06-knowledge.md) | §6 техніки, стихії, фоліанти, метод культивації |
| [07-combat.md](gdd/07-combat.md) | §7.1–7.7 структура бою, ресурси, дії, шкода, зона контролю, втеча |
| [08-injuries.md](gdd/08-injuries.md) | §8 загальні й серйозні травми, лікування, спіраль смерті |
| [09-world.md](gdd/09-world.md) | §9 регіон, місця медитації, правило генерації, NPC |
| [10-rumors-events.md](gdd/10-rumors-events.md) | §10 чутки, події |
| [11-crisis-victory.md](gdd/11-crisis-victory.md) | §11 тригер кризи, перемога |
| [12-meta-progression.md](gdd/12-meta-progression.md) | §12 резонанс душі |
| [13-economy.md](gdd/13-economy.md) | §13.1–13.5 духовні камені: валюта, джерела, торгівля, витрати |
| [14-tech-frame.md](gdd/14-tech-frame.md) | §14 рамки інтерфейсу й генерації |
| [15-mvp-scope.md](gdd/15-mvp-scope.md) | §15 таблиця «у MVP / відкладено» |

## Техдок — технічний документ MVP (Demo) (`tech/`)

| Файл | Зміст |
|---|---|
| [00-context.md](tech/00-context.md) | 0 версія, команда, темп, платформа, головний принцип |
| [01-stack.md](tech/01-stack.md) | 1 таблиця стеку, обґрунтування; 1.1 структура репо |
| [02-architecture.md](tech/02-architecture.md) | 2.1–2.8 Command/SimEvent, режими, час, GameState, модифікатори, треки, PlayerView, RNG |
| [03-content-pipeline.md](tech/03-content-pipeline.md) | 3.1–3.6 каталог /content, ID і теги, приклади YAML, локалізація, валідатор |
| [04-subsystems.md](tech/04-subsystems.md) | 4.1–4.9 гекс-бій і AI, генерація регіону, чутки/події, культивація, тренування, травми, економіка, мета, криза |
| [05-saves.md](tech/05-saves.md) | 5 ironman-сейв, commit-before-reveal, атомарність, бекап |
| [06-tooling.md](tech/06-tooling.md) | 6.1–6.4 Sim.Cli і боти, Python-аналітика, golden-тести, debug-панель |
| [07-presentation.md](tech/07-presentation.md) | 7 Godot-шар; 7.1 ШІ-ассети |
| [08-content-targets.md](tech/08-content-targets.md) | 8 обсяги контенту для демо |
| [09-roadmap.md](tech/09-roadmap.md) | 9 етапи 0–5 з критеріями готовності |
| [10-risks.md](tech/10-risks.md) | 10 реєстр ризиків R1–R12 |
| [decisions.md](tech/decisions.md) | 11 ухвалені рішення D1–D15; 12 робочі припущення A1–A7 |
| [open-questions.md](tech/open-questions.md) | 13 відкриті питання Q1–Q12 |
| [14-deferred.md](tech/14-deferred.md) | 14 відкладене за межі демо |

Субагент-дизайнер для сесій над GDD: `.claude/agents/game-designer.md`. Оригінали .docx: `sources/archive/` (не редагувати).
