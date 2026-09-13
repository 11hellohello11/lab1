# Звіт до лабораторної роботи № 1

## 1. Ідентифікація стану

- Варіант: 2-A
- Робоча гілка: lab/1-system. 
- Основна гілка: main.
- Тег: v0.1.0
- Commit hash: 9857ed1b88d165ab157c7b703111c3396d1dec77 (git rev-parse HEAD)

## 2. Вивід docker compose ... ps, який підтверджує успішний запуск контейнера БД

NAME                    IMAGE                      COMMAND                  SERVICE    CREATED        STATUS                  PORTS
secure-lab-postgres-1   postgres:18.4-alpine3.24   "docker-entrypoint.s…"   postgres   24 hours ago   Up 23 hours (healthy)   127.0.0.1:54329->5432/tcp

## 3. Змінений маршрут

Маршрут від дії у браузері до бази даних і назад:
1. Клієнт: Натискання кнопки ініціює fetch-запит у Client/app.js.
2. Мережа: GET-запит на /api/incidents/severity-summary.
3. API Endpoint: Обробка маршруту в IncidentEndpoints.cs.
4. Бізнес-логіка: Виклик IncidentQueries.cs, формування LINQ-запиту з GroupBy та Count.
5. Дані: Трансляція запиту через EF Core (SecureLabDbContext) до таблиці incidents у PostgreSQL.
6. Відповідь: Формування IncidentSeveritySummaryResponse у JSON.
7. DOM: Безпечне відображення даних у браузері через властивість textContent.

## 4. Збережена HTTP-відповідь 501 до реалізації точки розширення
HTTP/1.1 501 Not Implemented
Content-Length: 0

## 5. HTTP request/response успішного сценарію після реалізації
=== REQUEST ===
GET http://localhost:5080/api/incidents/severity-summary HTTP/1.1
Accept: application/json
traceparent: 00-836a42acaaa94f73bf7a040f9c34d3cf-29d5c0b903d149b1-00

=== RESPONSE ===
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Sun, 13 Sep 2026 15:29:58 GMT
Referrer-Policy: no-referrer
Server: Kestrel
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff

[{"severity":"High","count":1},{"severity":"Low","count":1},{"severity":"Medium","count":1}]

## 6. HTTP request/response 404 для відсутнього інциденту
=== REQUEST ===
GET http://localhost:5080/api/incidents/99999999-9999-9999-9999-999999999999 HTTP/1.1
Accept: application/json
traceparent: 00-62a241af2ddc4366b1d4b793bc986f14-c134672501284b32-00

=== RESPONSE ===
HTTP/1.1 404 Not Found
Content-Type: application/problem+json
Date: Sun, 13 Sep 2026 15:32:41 GMT
Referrer-Policy: no-referrer
Server: Kestrel
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Інцидент не знайдено","status":404,"detail":"Інцидент '99999999-9999-9999-9999-999999999999' не існує.","traceId":"00-62a241af2ddc4366b1d4b793bc986f14-bae5340ae4c48260-00"}

## 7. Запис Network для одного browser request
Request URL: http://localhost:5080/api/incidents/severity-summary
Request Method: GET
Status Code: 200 OK

## 8. Невеликий фрагмент маршруту endpoint → application layer → DbContext, а не весь код
// IncidentEndpoints.cs
app.MapGet("/api/incidents/severity-summary", async (IncidentQueries queries) => 
    Results.Ok(await queries.GetSeveritySummaryAsync()));

// IncidentQueries.cs
await dbContext.Incidents.AsNoTracking().GroupBy(i => i.Severity)...

## 9. Результат нової функції
[
    {
        "severity": "High",
        "count": 1
    },
    {
        "severity": "Low",
        "count": 1
    },
    {
        "severity": "Medium",
        "count": 1
    }
]

## 10. Діаграма або карта потоку
кнопка summary → handler у app.js → GET /api/incidents/severity-summary → IncidentEndpoints → IncidentQueries → SecureLabDbContext

## 11. Фактичний результат автоматизованої перевірки чи .http-сценарію
Test summary: total: 4; failed: 0; succeeded: 4; skipped: 0; duration: 3,0s
Build succeeded in 8,5s

## 12. Виконані зміни
Реалізовано метод агрегації інцидентів за критичністю (GetSeveritySummaryAsync) в IncidentQueries, додано новий ендпоінт GET /api/incidents/severity-summary в IncidentEndpoints, а також оновлено клієнтський JavaScript app.js для виклику API та безпечного відображення результату через textContent.

## 13. Перевірка
| ID | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| T-01 | Наявність `compose.yaml` | `docker compose ps` | Контейнер PostgreSQL у стані Up | Успішний запуск | `secure-lab-postgres-1 ... Up 23 hours (healthy)` |
| T-02 | До реалізації функції | `GET /api/incidents/severity-summary` | Відповідь 501 Not Implemented | Отримано 501 | `HTTP/1.1 501 Not Implemented Content-Length: 0` |
| T-03 | Після реалізації | `GET /api/incidents/severity-summary` | 200 OK та JSON із масивом | Дані отримано | `HTTP/1.1 200 OK [{"severity":"High","count":1}...` |
| T-04 | Відсутній інцидент | `GET /api/incidents/9999...` | 404 Not Found | Отримано 404 | `HTTP/1.1 404 Not Found "detail":"Інцидент не існує"` |
| T-05 | Взаємодія через UI | Клік на кнопку summary | Запит фіксується у DevTools | Запит успішний | `Request URL: .../severity-summary Status Code: 200 OK` |
| T-06 | Перевірка рішення | `dotnet test` | Усі тести проходять успішно | 4/4 succeeded | `Test summary: total: 4; failed: 0; succeeded: 4` |
| T-07 | Готовність до коміту | `git diff --staged` | Відсутність секретів у змінах | Секретів немає | Не виявлено токенів, паролів, `.env` чи дампів БД |

## 14. Підтвердження перегляду diff на відсутність секретів
Перед виконанням коміту було виконано команду git diff --staged. Під час перегляду не виявлено токенів, паролів, .env файлів чи локальних дампів БД.

## 15. Висновок
Функцію підсумку інцидентів успішно інтегровано в архітектуру з дотриманням меж довіри. Застосовано EF Core параметризацію та безпечне DOM-виведення. Репозиторій відтворюваний, зміни перевірені та зафіксовані у гілці main.