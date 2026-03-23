# NiTorrent — REGRESSION_CHECKLIST

Этот чеклист привязан к **текущей рабочей архитектуре**, где главным runtime boundary остаётся `ITorrentService/MonoTorrentService`, а список обновляется через snapshot pipeline.

---

## 1. High-risk зоны
Полный расширенный smoke нужен, если менялись:
- `App.xaml.cs`
- `AppStartupService`
- `AppActivationService`
- `AppCloseCoordinator`
- `MonoTorrentService`
- `TorrentStartupCoordinator`
- `TorrentStartupRecovery`
- `TorrentUpdatePublisher`
- `TorrentCatalogStore`
- `TorrentCatalogSnapshotSynchronizer`
- `TorrentMonitor`
- `TorrentViewModel`

---

## 2. Базовый smoke после нетривиального PR
Проверить минимум:
- приложение собирается;
- cold start проходит без исключений;
- главное окно открывается;
- cached список отображается;
- engine потом догружается без падения;
- приложение корректно закрывается и повторно запускается.

---

## 3. Startup / shell / lifecycle
Проверять при изменениях в `NiTorrent.App` и startup path:
- приложение стартует с чистым состоянием;
- приложение стартует с уже существующим catalog/state;
- вторичный запуск не создаёт сломанную вторую копию;
- `.torrent` activation при холодном старте обрабатывается корректно;
- hide-to-tray работает;
- explicit exit реально завершает процесс;
- после полного выхода повторный запуск стабилен.

Особенно смотреть:
- нет ли двойной инициализации engine;
- нет ли двойных подписок на `UpdateTorrent`/`Loaded`;
- нет ли ошибок при hide → show → hide → exit циклах.

---

## 4. Torrent list / snapshot pipeline
Проверять при изменениях в инфраструктурном update-flow:
- cached список показывается сразу после старта;
- после появления live runtime список не удваивается;
- один торрент не превращается в две строки (`cached` + `unknown/live`);
- удалённый торрент исчезает один раз;
- aggregate speed обновляется корректно;
- повторные ticks `TorrentMonitor` не плодят фантомные элементы.

---

## 5. Add / preview / duplicate
Проверять при изменениях в add-flow:
- `.torrent` проходит через preview;
- magnet проходит через preview так же, как и `.torrent`;
- подтверждённый add создаёт ровно один элемент;
- duplicate scenario обрабатывается предсказуемо;
- cancel в preview не создаёт записи в списке;
- после add и перезапуска список остаётся целым.

---

## 6. Start / pause / stop / remove
Проверять при изменениях в command path:
- start работает;
- pause работает;
- stop работает;
- remove удаляет торрент из списка;
- remove не оставляет мусорных записей в catalog/state;
- быстрые повторные нажатия не ломают lifecycle;
- исключение в одном торренте не валит всё приложение.

---

## 7. Restore / interrupted shutdown
Проверять при изменениях в persistence и startup:
- список восстанавливается после обычного выхода;
- список восстанавливается после hide-to-tray и последующего show;
- восстановленные торренты не дублируются;
- базовые поля (имя, путь, размер) не теряются;
- битый или частично заполненный state не валит приложение;
- каталог после старта не начинает самопроизвольно разрастаться дублями.

---

## 8. Settings / close behavior
Проверять при изменениях в настройках:
- staged-edit форма работает;
- `Save` сохраняет значения;
- `Reload` откатывает локальные изменения;
- `MinimizeToTrayOnClose` реально влияет на close behavior;
- runtime settings apply не валит engine;
- после перезапуска настройки сохраняются.

---

## 9. Ошибки и логирование
Проверять при изменениях в startup/engine/settings path:
- ошибка логируется с понятным этапом;
- пользовательский диалог не теряет контекст;
- исключение не проглатывается молча;
- при ошибке engine-init UI не ломается полностью;
- лог-файл продолжает писаться после нескольких ошибок подряд.

---

## 10. Документация
Если менялись архитектурные границы или активный runtime path:
- обновить `CURRENT_ARCHITECTURE_STATE.md`;
- обновить `README_ARCHITECTURE.md`;
- при изменении hotspot'ов обновить `PROJECT_MAP.md`;
- если изменилось целевое направление, синхронизировать `TRANSITION_BACKLOG.md` и target docs.
