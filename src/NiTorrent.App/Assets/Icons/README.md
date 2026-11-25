# Иконки интерфейса

Набор оригинальных контурных SVG для интерфейса NiTorrent. Все иконки имеют
размер 24 × 24 и одинаковую толщину линии. Файлы без суффикса предназначены
для тёмной темы, файлы с суффиксом `-light` — для светлой.

Имена обозначают действие или объект: `add`, `download`, `upload`, `pause`,
`play`, `folder`, `file`, `search`, `delete`, `link`, `peers`, `close`,
`more`, `check`. Новые иконки следует добавлять в обеих цветовых версиях.

В WinUI SVG можно использовать через `SvgImageSource`:

```xml
<Image Width="20" Height="20">
    <Image.Source>
        <SvgImageSource UriSource="ms-appx:///Assets/Icons/folder.svg" />
    </Image.Source>
</Image>
```

Цвет в файлах задан явно: выбирайте вариант в соответствии с темой приложения.
Для элементов, которым нужен автоматический цвет состояния или темы,
подходят системные `FontIcon` и Segoe Fluent Icons.
