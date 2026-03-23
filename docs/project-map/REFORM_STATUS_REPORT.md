# Reform status report

Статус: **closed**.

## Что завершено
- legacy facade `ITorrentService` удалён из активной архитектуры;
- write/read/status/maintenance boundaries переведены на engine-backed implementations;
- runtime facts, engine gateway, engine lifecycle и engine state store получили собственные infrastructure-backed реализации;
- application и domain очищены от transition-only duplicates.

## Что считать источником истины
- продуктовая логика: `USER_APP_LOGIC.md`
- текущая архитектура: `CURRENT_ARCHITECTURE_STATE.md`
- целевая архитектура: `TARGET_ARCHITECTURE_V2.md`

## Что больше не использовать как статус миграции
Отдельные phase notes и historical plans не должны трактоваться как текущий статус системы.
