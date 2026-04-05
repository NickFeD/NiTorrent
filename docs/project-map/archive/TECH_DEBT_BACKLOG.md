# Tech Debt Backlog

## TD-001. Reflection в per-torrent runtime settings adapter
`TorrentEntrySettingsRuntimeApplier` использует best-effort reflective доступ к настройкам `TorrentManager`.
Если MonoTorrent даст стабильный публичный API для этих настроек, reflection нужно удалить.

## TD-002. Единая нормализация пользовательских ошибок
Сейчас основные torrent-scenarios уже нормализуют сообщения, но это правило нужно удерживать при новых экранах и интеграциях.

## TD-003. Дальнейшая чистка комментариев и исторических docs
Исторические phase/transition документы остаются как архив; source-of-truth — `USER_APP_LOGIC.md` и актуальные project-map docs.
