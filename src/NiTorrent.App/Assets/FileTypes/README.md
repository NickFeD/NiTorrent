# Значки файлов и папок

Цветные SVG размером 24 × 24 для дерева содержимого торрента. Их можно
отображать в WinUI через `SvgImageSource`; векторный формат масштабируется
под разные DPI. Окно предварительного просмотра использует эти ресурсы для
узлов дерева файлов.

Предлагаемое соответствие расширений (сравнение без учёта регистра):

| Значок | Расширения |
| --- | --- |
| `folder` | папка |
| `document` | txt, md, rtf, doc, docx, odt, log, nfo |
| `pdf` | pdf |
| `spreadsheet` | xls, xlsx, ods, csv |
| `presentation` | ppt, pptx, odp |
| `archive` | zip, rar, 7z, tar, gz, bz2, xz, zst, cab |
| `image` | jpg, jpeg, png, gif, webp, bmp, svg, tiff, avif |
| `video` | mkv, mp4, avi, mov, webm, m4v, wmv, mpg, ts |
| `audio` | mp3, flac, wav, m4a, aac, ogg, opus, wma, aiff |
| `disc` | iso, img, bin, cue, nrg |
| `code` | cs, cpp, h, js, ts, py, json, xml, html, css, sh, ps1 |
| `executable` | exe, msi, msix, bat, cmd, apk, appx |
| `font` | ttf, otf, woff, woff2 |
| `ebook` | epub, mobi, azw, azw3, fb2 |
| `subtitle` | srt, ass, ssa, vtt, sub |
| `settings` | ini, cfg, conf, toml, yaml, yml |
| `torrent` | torrent |
| `file` | неизвестное расширение или файл без расширения |

Список легко расширять: добавить SVG в этот каталог и сопоставить расширение
с именем ресурса в `TorrentTreeItemViewModel.GetIconName`. Для составных
расширений сейчас используется последнее окончание (например, `.gz`).
