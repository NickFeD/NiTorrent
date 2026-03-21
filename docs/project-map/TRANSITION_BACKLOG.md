# NiTorrent — TRANSITION_BACKLOG

## Назначение
Этот файл фиксирует временные мосты между старой engine-centered архитектурой и новой product-centered архитектурой из `TARGET_ARCHITECTURE_V2.md`.

Каждый пункт обязан иметь:
- причину существования;
- новый источник истины, к которому он должен привести;
- критерий удаления.

---

## Активные transition-only зоны

### TB-001. `TorrentSnapshot` остаётся основным read model и одновременно участвует в продуктовых решениях
**Почему пока живёт:** UI и engine flow уже построены вокруг snapshot-публикации.

**Что должно стать новым источником истины:**
- `TorrentEntry`
- product-owned collection repository
- domain policies для status/intent/deferred actions

**Когда удалить мост:**
Когда `Presentation` будет читать список из product read model, построенной на `TorrentEntry`, а snapshot останется только engine-facing DTO.

---

### TB-002. `MonoTorrentService` остаётся главным engine facade
**Почему пока живёт:** текущие use cases и инфраструктурные сервисы ещё опираются на единый engine-centric контракт.

**Что должно стать новым источником истины:**
- `ITorrentEngineGateway`
- product workflows в `Application`
- `ITorrentCollectionRepository`

**Когда удалить мост:**
Когда startup, commands и restore перестанут зависеть от `MonoTorrentService` как от центрального оркестратора.

---

### TB-003. Текущий catalog всё ещё хранит смесь cache/runtime/product intent
**Почему пока живёт:** рабочий билд уже использует этот каталог для restore и раннего списка торрентов.

**Что должно стать новым источником истины:**
- новый product-owned repository коллекции
- явная модель `TorrentEntry`
- отдельные runtime facts от engine gateway

**Когда удалить мост:**
Когда коллекция торрентов будет храниться отдельно от engine-state snapshots.

---

### TB-004. `ShouldRun` остаётся переходным представлением пользовательского намерения
**Почему пока живёт:** текущий restore path и startup logic опираются на булевый флаг.

**Что должно стать новым источником истины:**
- `TorrentIntent`
- `DeferredAction`
- domain policy по восстановлению intent после запуска

**Когда удалить мост:**
Когда product repository хранит `TorrentIntent` и очередь отложенных действий напрямую.

---

### TB-005. Настройки закрытия окна всё ещё представлены через legacy bool
**Почему пока живёт:** текущий shell flow и settings UI используют `MinimizeToTrayOnClose`.

**Что должно стать новым источником истины:**
- `AppCloseBehavior`
- единый settings model уровня продукта

**Когда удалить мост:**
Когда shell workflow читает полноценный `AppCloseBehavior`, включая future `AskUser`.
