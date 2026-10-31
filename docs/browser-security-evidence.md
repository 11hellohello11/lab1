# Докази безпеки браузера (DEL-01)

Рівень: достатній («Задовільно»).

## Стенд і Git-стан (Для всіх)

ОС, browser/version, API/probe origins: Windows; Google Chrome 156.0.8078.74 (64-розрядна версія). API origin — http://localhost:5080; дозволений навчальний probe — http://localhost:4173; контрольний недозволений probe — http://localhost:4174.

Release/base\_commit/entry\_parent з receipt: release — lab-05-start-v1; base\_commit — 768bc43000699fd0c499e14e0ab591f075fad39c; entry\_parent — 35d534322d9ba69c96cf6381e878bb4f4ae2fc17. Значення entry\_parent відрізняється від base\_commit через документаційний коміт після завершення ЛР4.

Vulnerable/fixed hashes, v0.5.0, main і спосіб передавання: початковий вразливий стан — c6cc0dd; коміт виправлення XSS — a9f289b; коміт налаштування CORS — b697033. Робоча гілка — lab/5-browser-xss. На момент підготовки документа тег v0.5.0 ще не створено, а зміни ще не перенесено до main; поточний стан main — b4b0d42 в окремому Git worktree C:\\SecureLab-LR2. Остаточний тег, стан main та спосіб передавання фіксуються після фінального Git-оформлення.

Механізм сеансу (Application → Storage → Cookies → http://localhost:5080): ASP.NET Core Identity використовує cookie `.AspNetCore.Identity.Application`; Domain — localhost; Path — /; HttpOnly — увімкнено; SameSite — Lax; Secure — не встановлено; Expires / Max-Age — Session. Атрибути встановлено в DevTools; значення cookie (Value) не зберігається у доказах.

## Гіпотеза й REG-XSS-01 (Для всіх)

Карта Description: поле опису інциденту в browser-lab.html → створення інциденту через API → збереження Incident.Description в PostgreSQL → GET /api/incidents/{id} → JSON-поле `description` → `current.description` в browser-lab.js → DOM-елемент `#description`.

Гіпотеза source → storage → response → sink/context: якщо в текстовому описі інциденту міститься HTML-подібний рядок, небезпечне присвоєння `innerHTML` інтерпретує його як розмітку та може виконати обробник події. Заміна `innerHTML` на `textContent` повинна зберегти оригінальні дані у відповіді API, але відображати їх у браузері виключно як текст.

Передумови: локальний PostgreSQL і API; тестовий користувач Alice, навчальний інцидент «Перевірка XSS», GUID `59e2a1ee-7938-4f72-9f52-e6450e273f14`; marker із `C:\\\\lab-05-scaffold\\\\resources\\\\lab05-marker.txt`. Значення marker: `<img src=x onerror="document.querySelector('#lab05-marker-target').setAttribute('data-lab05-marker','executed')">`. Початковий стан — коміт c6cc0dd, виправлений стан — a9f289b. Локальний пароль користувачів задається поза Git змінною `SeedUsers\\\_\\\_Password`. Початкове заповнення БД за потреби виконується командою `dotnet run --project src/SecureLab.Api -- --reset-database` (команда видаляє поточні навчальні дані та не потрібна для повторної перевірки збереженого інциденту). API запускається командою `dotnet run --project src/SecureLab.Api`; створення інциденту та його читання виконувалися через браузерну форму та запит GET на конкретний GUID.

|Спостереження|До fix|Після fix|Посилання на доказ|
|-|-|-|-|
|Значення в БД/API|API повертає вихідний marker у JSON-полі description|API повертає той самий marker без змін|Рис. 10, 17|
|DOM основного Description|У DOM створено елемент img з атрибутом onerror|Значення показано як текст; елемент img із marker не створюється|Рис. 9, 16|
|Дія marker|`data-lab05-marker` отримав значення `executed`|Значення атрибута — `null`; виконання marker не відбулося|Рис. 8, 15|
|Апостроф/лапки, 5 < 7, Unicode, HTML-like рядок|Окремий позитивний контроль до fix не зафіксовано|Спеціальні символи, Unicode та HTML-подібний рядок відображаються буквально|Рис. 18|
|Довжина за власними правилами ЛР 2|Окремий тест до fix не зафіксовано|Текст опису довжиною 4000 символів відображено повністю, DOM textContent.length = 4000|Рис. 19|
|Вікно Morgan: GET і перевірка marker; контроль Bob|Окремий тест до fix не зафіксовано|Morgan: GET 200, marker = null; Bob після оновлення сторінки: GET 404, чужий опис не показано|Рис. 20, 21|

Finding: актив — текстові відомості про інциденти та безпечне відображення їх у браузері. Передумова експлуатації — наявність у збереженому описі недовіреної HTML-розмітки й подальше відкриття інциденту у вразливому клієнті. Першопричина — використання `innerHTML` для `current.description` та `comment.text`. Safe DOM diff — заміна обох присвоєнь на `textContent` у `src/SecureLab.Api/Client/browser-lab.js` (рис. 11–14). Після виправлення текст маркера залишається в JSON, але не інтерпретується як HTML у DOM; контрольний атрибут лишається `null`. Залишковий ризик: інші майбутні DOM sink або небезпечне використання даних у контекстах атрибутів, URL чи JavaScript потребують окремої перевірки; `textContent` захищає саме текстовий контекст.

Повторювана REG-XSS-01: (1) запустити БД і API, увійти як Alice; (2) у стані c6cc0dd створити інцидент з незміненим marker із навчального файлу та прочитати його за GUID; (3) зафіксувати JSON marker, DOM-елемент img та значення `executed`; (4) перейти до виправленої версії з комітом a9f289b і повторно відкрити той самий інцидент; (5) перевірити незмінний JSON, текстовий DOM-вузол і `null` замість `executed`; (6) виконати позитивний контроль спеціальних символів, Unicode і довжини 4000; (7) перевірити дозволений доступ Morgan і відмову Bob для чужого інциденту. Негативний контроль — стан до fix із marker = `executed`; очікування після fix — marker = `null` за незмінного вмісту API.

## CORS і REG-CORS-01 (Для всіх)

Прогноз (Етап 3, крок 1), до зміни policy:

|Сценарій|Чи доходить request до API?|Чи може JavaScript прочитати response?|Чому|
|-|-|-|-|
|Same-origin 5080 → API|Так|Так|Обмеження CORS для same-origin не застосовується|
|Simple GET 4173 → API|Так|До policy — ні; після дозволу — так|Браузеру потрібен відповідний ACAO|
|Simple GET 4174 → API|Так|Ні|Origin відсутній у дозволеному переліку|
|curl/.http → API|Так|Не застосовується: прямий клієнт читає HTTP-відповідь|Browser SOP не обмежує curl|
|Preflighted 4173 → API («Добре / Відмінно»)|Не перевірялося|Не перевірялося|Поза обсягом рівня «Задовільно»|

Команди запуску probes (окремі консолі з кореня репозиторію):

```powershell
py -3 -m http.server 4173 --bind localhost --directory demonstrations/cors-probe
py -3 -m http.server 4174 --bind localhost --directory demonstrations/cors-probe
```

Policy — `Lab05BrowserProbe`, лише для Development: `WithOrigins("http://localhost:4173")`, `WithMethods("GET")`, `WithHeaders("X-Lab-Cors-Probe")`. У pipeline `UseRouting()` → `UseCors("Lab05BrowserProbe")` → `UseAuthentication()` → `UseAuthorization()`. Origin 4174 — негативний контроль. `AllowCredentials()` не налаштовано. Реалізацію зафіксовано комітом b697033 (рис. 28–30).

|Клієнт / простий запит|До policy|Fixed state|Status, ACAO, Console / доказ|
|-|-|-|-|
|Same-origin 5080|Доступ дозволено|Доступ дозволено|GET 200; CORS не потрібен|
|Allowed 4173|Читання заблоковано|Читання дозволено|GET 200; `Access-Control-Allow-Origin: http://localhost:4173`; рис. 26, 32, 34|
|Disallowed 4174|Читання заблоковано|Читання заблоковано|API повертає GET 200, ACAO відсутній; браузер повідомляє `TypeError: Failed to fetch`; рис. 27, 33, 35|
|Direct HTTP / curl|До policy не перевірялося|Відповідь читається|HTTP 200, JSON `{"status":"ready"}`, ACAO відсутній; рис. 36|

REG-CORS-01: на сторінках `http://localhost:4173/` і `http://localhost:4174/` виконано однаковий `fetch("http://localhost:5080/health")`. До policy обидва запити блокували читання відповіді браузером (рис. 26, 27). Після налаштування лише 4173 отримав доступ до JSON і заголовок ACAO із точним дозволеним origin (рис. 32, 34); для 4174 браузер продовжив блокувати читання, хоча сервер повертав 200 (рис. 33, 35).

Фактичні HTTP-відповіді: для Origin 4173 у браузерній вкладці Network зафіксовано 200 OK і `Access-Control-Allow-Origin: http://localhost:4173`; для Origin 4174 — 200 OK без `Access-Control-Allow-Origin`. Прямий контроль виконано командою `curl.exe -i -H "Origin: http://localhost:4174" http://localhost:5080/health` із результатом HTTP 200 та JSON `{"status":"ready"}` (рис. 36). На відміну від browser JavaScript, curl не застосовує SOP/CORS до читання відповіді.

Перевірка порядку middleware: зі сторінки 4173 без `credentials: "include"` виконано `fetch("http://localhost:5080/api/incidents/20000000-0000-0000-0000-000000000001").then(response => response.status)`. Отримано доступний JavaScript статус `401 Unauthorized`: CORS дозволяє прочитати статус, але автентифікація блокує доступ до ресурсу (рис. 37). Маршрут списку `GET /api/incidents` є публічним, тому для перевірки 401 використано захищений маршрут деталей.

Регресійна перевірка після CORS: `dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release` — 14 успішних тестів, 0 помилок, 0 пропущених (рис. 38). Змінну `SeedUsers\\\_\\\_Password` задають локально перед запуском тестів, не зберігаючи пароль у Git.

## Додатково для Добре та Відмінно

Не виконується: обрано рівень «Задовільно».

## Додатково для Відмінно

Не виконується: обрано рівень «Задовільно».
