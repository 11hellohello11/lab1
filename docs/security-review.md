
# Security review SecureLab, ЛР 6

Рівень: достатній («Задовільно»).
Commit початку огляду (review-start): `71af2769ea5c3ad85d8cc8b723f406436c23df93`.
Фінальний commit: буде зазначено після завершення роботи.
Тег: `v1.0.0` — буде створено після завершення огляду.
Дата: 01.11.2026.
SDK: .NET 10.0.401.
Профілі запуску: Development, Production.
Клієнти: Google Chrome DevTools, PowerShell (`curl.exe`), xUnit.

## 1. Executive summary

У межах огляду перевірено основні механізми захисту SecureLab: обробку серверних помилок, HTTP-заголовки, cookie-автентифікацію, авторизацію, захист від SQL-ін'єкцій, XSS та CSRF. Виявлено й виправлено одну знахідку F-01 — відсутність заголовків безпеки у відповіді HTTP 500. Додатково реалізовано antiforgery-захист маршруту створення інцидентів. Після внесення змін виконано 15 автоматизованих тестів, усі завершилися успішно. Залишкові ризики пов'язані з використанням локального HTTP, неперевіреними сценаріями login CSRF та обмеженим охопленням окремих механізмів захисту.

## 2. Scope та exclusions

До меж огляду включено:

- серверну частину ASP.NET Core Web API;
- механізми автентифікації та авторизації;
- маршрути створення й отримання інцидентів;
- браузерний клієнт;
- конфігурацію HTTP-конвеєра;
- поточні відстежувані Git-файли конфігурації;
- автоматизовані тести.

Перевірено конфігураційні файли `appsettings.json`, `appsettings.Development.json`, `infra/.env.example`, `infra/compose.yaml` та правила `.gitignore`.

Історію Git повністю не перевірено. Зовнішнє тестування на проникнення, production-інфраструктура, розподілене навантаження та розширений аудит залежностей не входять до обраного рівня.

## 3. System overview і trust boundaries

SecureLab складається з браузерного клієнта, ASP.NET Core API, механізму ASP.NET Core Identity та бази даних PostgreSQL.

Основні межі довіри:

1. Браузер → API: передавання HTTP-запитів, JSON-даних, cookie та antiforgery-токенів.
2. API → PostgreSQL: читання й запис інцидентів, перевірка власника та формування SQL-запитів.
3. Середовище → конфігурація: використання змінних середовища й параметрів підключення.
4. Git → збірка: контроль вихідного коду, конфігурації та результатів тестування.

Детальну модель загроз наведено у `docs/threat-model.md`.

## 4. Methodology та test environment

Початковий стан зафіксовано на commit `71af2769ea5c3ad85d8cc8b723f406436c23df93`, базова версія — `v0.5.0`.

Тестування виконувалося на Windows із .NET SDK 10.0.401 та PostgreSQL у Docker Compose. Для перевірок використовувалися середовища Development і Production.

Основні команди:

- `dotnet restore SecureLab.sln --locked-mode`
- `dotnet build SecureLab.sln -c Release --no-restore`
- `dotnet run --project src/SecureLab.Api`
- `dotnet test SecureLab.sln -c Release`
- `powershell -NoProfile -ExecutionPolicy Bypass -File C:\lr06-resources\commands\Production.ps1 -Mode normal`
- `powershell -NoProfile -ExecutionPolicy Bypass -File C:\lr06-resources\commands\Production.ps1 -Mode broken`

Браузерні перевірки проводилися у Google Chrome із використанням DevTools. Для автентифікованих сценаріїв використовувався навчальний користувач Alice.

Контрольована помилка підключення до PostgreSQL відтворювалася у Production. Сервер повертав HTTP 500 у форматі Problem Details без розкриття внутрішніх деталей винятку.

Остаточний Git-хеш перевіреної версії буде зазначено після завершення роботи.

## 5. Findings summary

| ID | Актив | Вплив | Ймовірність | Пріоритет | Статус | ID повторної перевірки |
|---|---|---|---|---|---|---|
| F-01 | HTTP-відповіді API | Відсутність заголовків безпеки при серверній помилці | За умови необробленого винятку | P2 | Fixed | RT-F01 |

За результатами огляду зафіксовано одну виправлену знахідку.

## 6. Detailed findings

### F-01. Відсутність заголовків безпеки у відповіді HTTP 500

Під час контрольованого виникнення серверної помилки встановлено відсутність заголовків `X-Content-Type-Options` і `Referrer-Policy`.

Першопричиною було розташування middleware встановлення заголовків після `UseExceptionHandler()`.

Виправлення полягало у зміні порядку middleware та використанні `Response.OnStarting()` для встановлення заголовків перед надсиланням відповіді.

RT-F01: повторний запит із контрольованою недоступністю PostgreSQL повернув HTTP 500 із заголовками `X-Content-Type-Options: nosniff` і `Referrer-Policy: no-referrer`.

Positive regression: відповідь `GET /health` зберегла HTTP 200 та обидва заголовки.

Статус: Fixed.

Повний опис наведено у `docs/findings/F-01-short-name.md`.

## 7. Regression results

| ID | Напрям | Команда / тест / сценарій | Передумова | Фактичний результат | Стан ресурсу |
|---|---|---|---|---|---|
| REG-SQLI-01 | SQLi | `Search_SqlInjectionInput_IsTreatedAsData` | Пошук із `' OR '1'='1` | PASS, HTTP 200, `[]` | Зміни даних не виконувалися |
| REG-AUTH-01 | Authentication | `A01_AnonymousUser_CannotAccessMe` | Без автентифікації | PASS | Без змін |
| REG-AUTH-02 | Authentication | `A02_AuthenticatedUser_CanAccessMe` | Авторизований користувач | PASS | Без змін |
| REG-IDOR-01 | Authorization | `A02_Owner_CanReadOwnIncident` | Власник інциденту | PASS | Без змін |
| REG-IDOR-02 | Authorization | `A03_NonOwner_CannotReadForeignIncident` | Інший користувач | PASS | Без змін |
| REG-IDOR-03 | Authorization | `A03R_Analyst_CanReadIncidentForAnalysis` | Користувач із роллю Analyst | PASS | Без змін |
| REG-XSS-01 | XSS | `ClientScript_DoesNotUseDangerousInnerHtmlSink` | Перевірка `app.js` | PASS | Без змін |
| REG-CSRF-01 | CSRF | POST `/api/incidents` із токеном | Авторизована Alice | HTTP 201 | Інцидент створено |
| REG-CSRF-02 | CSRF | POST `/api/incidents` без токена | Авторизована Alice | HTTP 400 | 0 збігів у переліку інцидентів |
| REG-CSRF-03 | CSRF | POST із недійсним токеном | Авторизована Alice | HTTP 400 | Запит відхилено |
| REG-ALL-01 | Регресія | `dotnet test SecureLab.sln -c Release` | Локальна тестова БД | PASS, 15/15 | Тестові сценарії виконано |

Для SQLi реалізовано додатковий тест із контрольним рядком `' OR '1'='1`. Перевірено, що введення не спричиняє обходу умов пошуку у дослідженому сценарії.

Для XSS перевірено відсутність `innerHTML` у дослідженому клієнтському скрипті. Це не є повним аудитом усіх можливих XSS-сценаріїв.

Для CSRF перевірено успішне створення інциденту з дійсним токеном, відхилення запитів без токена та з неправильним токеном.

Підсумковий автоматизований прогін містив 15 тестів, 0 помилок і 0 пропущених перевірок.

Фінальний commit перевіреної версії буде додано після його створення.

## 8. Hardening controls

### HTTP security headers

У `Program.cs` застосовано:

- `X-Content-Type-Options: nosniff`;
- `Referrer-Policy: no-referrer`.

Перевірено відповіді HTTP 200, 400, 401, 404 і 500. Для HTTP 500 перевірку проведено в середовищі Production через `curl.exe`.

Після виправлення F-01 заголовки присутні також у відповіді на контрольовану серверну помилку.

### Cookie authentication

У `Lab03Identity.cs` встановлено:

- `HttpOnly = true`;
- `SameSite = Lax`;
- `SecurePolicy = SameAsRequest` у Development;
- `SecurePolicy = Always` поза Development.

Під час аналізу конфігурації також встановлено повернення HTTP 401 для неавтентифікованих користувачів і HTTP 403 у випадку недостатніх прав.

Окрему перевірку фактичних атрибутів `Set-Cookie` для кожного профілю не виконано.

### Antiforgery

У `Program.cs` зареєстровано `AddAntiforgery()` із заголовком `X-CSRF-TOKEN`.

Додано маршрут `GET /api/antiforgery/token`, який використовує `GetAndStoreTokens()`.

У `POST /api/incidents` реалізовано `IAntiforgery.IsRequestValidAsync()` перед записом інциденту до бази даних.

У `browser-lab.js` додано отримання токена та його передавання у POST-запиті.

### Периметр змінювальних HTTP-маршрутів

| ID | Method + route | Джерело / розбіжність | Яка зміна стану | Cookie authentication | Browser-accessible | Antiforgery validation | Regression ID |
|---|---|---|---|---|---|---|---|
| SC-01 | POST `/api/incidents` | `Lab02Endpoints.cs`, `browser-lab.js` | Створення інциденту | Так | Так | Explicit | REG-CSRF-01–03 |
| SC-02 | POST `/api/auth/register` | `Lab03Identity.cs`, `register-check.js` | Реєстрація користувача | Ні | Так | Не реалізовано; окремий ризик | Не перевірено |
| SC-03 | POST `/api/auth/login` | `Lab03Identity.cs`, `auth-lab.js` | Створення сеансу | Ні | Так | Не реалізовано; ризик login CSRF | Не перевірено |
| SC-04 | POST `/api/auth/logout` | `Lab03Identity.cs`, `auth-lab.js` | Наразі HTTP 501, зміни стану немає | Потенційно так | Так | Не реалізовано | Не перевірено |

Для маршруту створення інциденту antiforgery-захист реалізовано та перевірено.

Маршрути входу й реєстрації не потребують наявної authentication cookie для доступу, однак мають окремі CSRF-ризики. Вони залишені в переліку обмежень огляду.

Маршрут виходу наразі повертає HTTP 501. Перед реалізацією фактичного виходу через `SignOutAsync()` необхідно передбачити antiforgery-перевірку.

### Transport

У `launchSettings.json` налаштовано Development-профіль `http://localhost:5080`.

HTTPS-профілю немає. Для локального навчального стенда використовується HTTP, а перед розгортанням у реальному середовищі необхідно налаштувати HTTPS.

### Rate limiting та інші controls

Наявність окремого login limiter під час перевірки не підтверджено. Перевірки quota, HTTP 429 та відновлення після обмеження частоти не проводилися.

Розширений пошуковий rate limiting, CSP та додаткові контролі вищих рівнів не входять до обраного рівня «Задовільно».

## 9. Dependency and secret audit

Перевірено поточні відстежувані файли конфігурації та застосовано пошук за ключовими словами `password`, `passwd`, `secret`, `api.?key`.

Виявлені збіги стосувалися назв полів, навчальних параметрів та демонстраційного значення `local-study-password`, яке використовується виключно для локального середовища.

Файли `infra/.env` та `appsettings.Local.json` виключено з відстеження правилами `.gitignore`.

Реальних production-секретів у перевірених матеріалах не підтверджено.

Повний аудит історії Git, систематичний аналіз залежностей та розширені перевірки журналів, передбачені для вищих рівнів, не виконувалися.

## 10. Residual risks

| Ризик | Статус | Відповідальний | Наступна дія |
|---|---|---|---|
| HTTP без TLS у Development | Прийнято для локального стенда | Розробник | Налаштувати HTTPS перед розгортанням |
| Login CSRF | Не перевірено | Розробник | Окремо оцінити та реалізувати захист |
| CSRF під час реєстрації | Не перевірено | Розробник | Оцінити ризик реєстраційного маршруту |
| Logout без реалізації | HTTP 501 | Розробник | Перед `SignOutAsync()` додати antiforgery |
| Login rate limiting | Не підтверджено | Розробник | Розглянути обмеження частоти входів |
| Повна історія Git | Не перевірено | Розробник | Провести аудит перед production-релізом |
| Повне охоплення XSS і SQLi | Не перевірено | Розробник | Розширити тестові сценарії |

## 11. Conclusion

Під час лабораторної роботи виконано підсумковий огляд механізмів захисту SecureLab. Виявлено й усунуто недолік F-01, пов'язаний із відсутністю HTTP-заголовків безпеки у відповідях на серверні помилки. Реалізовано antiforgery-захист маршруту створення інцидентів та перевірено позитивні й негативні сценарії його роботи. Додано регресійний тест SQL-ін'єкцій, унаслідок чого загальна кількість автоматизованих перевірок збільшилася до 15. Усі 15 тестів завершилися успішно. Залишкові ризики та межі виконаного огляду задокументовано окремо.

## 12. Evidence appendix

Основні докази:

- `docs/threat-model.md` — модель загроз;
- `docs/findings/F-01-short-name.md` — опис знахідки, виправлення та retest;
- `src/SecureLab.Api/Program.cs` — security headers і antiforgery;
- `src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs` — перевірка CSRF-токена;
- `src/SecureLab.Api/Scaffolding/Lab03Identity.cs` — налаштування authentication cookie;
- `src/SecureLab.Api/Client/browser-lab.js` — передавання CSRF-токена;
- `tests/SecureLab.Api.Tests/CookieClient.cs` — тестові POST-запити з CSRF-токеном;
- `tests/SecureLab.Api.Tests/SearchMechanicsTests.cs` — SQLi regression;
- звіт лабораторної роботи — скріншоти перевірок і їхні результати.

Основні команди перевірки:

- `dotnet build SecureLab.sln -c Release --no-restore`
- `dotnet test SecureLab.sln -c Release`
- `curl.exe -sS -D - -o NUL http://localhost:5080/health`
- запуск `Production.ps1` у режимах `normal` і `broken`.

Контрольована відповідь HTTP 500 зіставлялася з журнальними записами за `traceId`.

Паролі, значення authentication cookies, antiforgery-токени та секрети у доказах не наводяться.

## Декларація й журнал ШІ

Використовувався ШІ: ChatGPT (OpenAI).

Штучний інтелект використовувався для допомоги з програмним кодом, зокрема підготовки та уточнення окремих фрагментів реалізації механізмів захисту і тестових методів. Зміни вносилися до проєкту та перевірялися шляхом компіляції й виконання тестів.

| Дата | Сервіс | Завдання/фрагмент | Авторська адаптація | Перевірка й результат |
|---|---|---|---|---|
| 01.11.2026 | ChatGPT | Допомога з кодом middleware та заголовків безпеки | Внесення змін до `Program.cs` | Збірка успішна, заголовки HTTP 500 і HTTP 200 перевірені |
| 01.11.2026 | ChatGPT | Допомога з реалізацією antiforgery | Зміни в `Program.cs`, `Lab02Endpoints.cs`, `browser-lab.js` | HTTP 201 із токеном, HTTP 400 без токена |
| 01.11.2026 | ChatGPT | Допомога з кодом автоматизованих тестів | Додавання `PostWithCsrfAsync()` та SQLi-тесту | 15 із 15 тестів пройдено |
