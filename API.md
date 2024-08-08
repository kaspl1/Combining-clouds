# HTTP API запросы

## Загрузка файла на облако

**URL:** `/`

**Метод:** `POST`

**Параметры:**
- `postToDB=true|false` — Загрузить файл на Dropbox.
- `postToYD=true|false` — Загрузить файл на Yandex Disk.
- `postToGD=true|false` — Загрузить файл на Google Drive.
- `name=<filename>` — Имя файла.
- `path=<cloud_path>` — Путь в облачном хранилище.

**Тело запроса:** Файл в бинарном формате.

## Скачивание файла с облака

**URL:** `/`

**Метод:** `GET`

**Параметры:**
- `getFromDB=true|false`
- `getFromYD=true|false`
- `getFromGD=true|false`
- `name=<filename>`
- `path=<cloud_path>`

**Сохранение:** Файл сохраняется в локальную директорию, указанную в конфигурационном файле проекта.
