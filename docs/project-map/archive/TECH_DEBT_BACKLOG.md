# Tech Debt Backlog

<!-- documentation-status-2026-11-27 -->
> Исторический документ. Статусы active/closed, типы и результаты ниже относятся к дате исходной записи. Это не карта текущего кода и не поручение на реализацию. Начать с [актуального контекста](../../analysis/README.md). Ссылки на перенесённые файлы могут вести на более поздний код; они не подтверждают старые наблюдения.

## TD-001. Reflection в per-torrent runtime settings adapter
`TorrentEntrySettingsRuntimeApplier` использует best-effort reflective доступ к настройкам `TorrentManager`.
Если MonoTorrent даст стабильный публичный API для этих настроек, reflection нужно удалить.

## TD-002. Единая нормализация пользовательских ошибок
Сейчас основные torrent-scenarios уже нормализуют сообщения, но это правило нужно удерживать при новых экранах и интеграциях.

## TD-003. Дальнейшая чистка комментариев и исторических docs
Исторические phase/transition документы остаются как архив; source-of-truth — `USER_APP_LOGIC.md` и актуальные project-map docs.
