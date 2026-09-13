# Карта архітектури

Це карта архітектури для реалізованого наскрізного розширення підсумку інцидентів за критичністю (варіант 2-А).

## Компоненти

| Компонент | Розташування | Відповідальність |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Надсилає HTTP-запити через `fetch`, безпечно показує відповідь за допомогою DOM API (`textContent`) |
| Presentation | `src/SecureLab.Api/Presentation/` | Описує endpoints (`IncidentEndpoints.cs`), читає зовнішні параметри, формує HTTP-відповідь за допомогою DTO |
| Application | `src/SecureLab.Api/Application/` | Виконує сценарій отримання списку або деталей інциденту |
| Data | `src/SecureLab.Api/Data/` | Відображає C#-сутності на PostgreSQL через EF Core/Npgsql (`SecureLabDbContext.cs`, таблиця `incidents`) |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані у локальному контейнері |

## Підготовлений наскрізний маршрут

```text
кнопка summary у Client/index.html
  → handler у Client/app.js
  → GET /api/incidents/severity-summary
  → IncidentEndpoints
  → IncidentQueries
  → SecureLabDbContext.Incidents / таблиця incidents
  → IncidentSeveritySummaryResponse як JSON
  → textContent у списку підсумку
```
## Ключові файли

- Клієнт: `src/SecureLab.Api/Client/index.html`, `src/SecureLab.Api/Client/app.js`
- Endpoint: `src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs`
- Application Layer: `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs`
- DTO: `src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs`
- DbContext & Таблиця: `src/SecureLab.Api/Data/SecureLabDbContext.cs`, таблиця `incidents`

## Межі довіри

| Межа | Чому даним ще не можна довіряти | Де перевіряємо або обмежуємо |
|---|---|---|
| Користувач → Browser client | Користувач контролює введення та середовище браузера | На стороні серверного API, оскільки клієнтські форми не гарантують безпеку даних і можуть бути змінені чи обведені |
| Browser client → API | Клієнт і HTTP-запит можна змінити поза UI (будь-який сторонній клієнт може надіслати запит напряму) | У шар Presentation та маршрутизації ASP.NET Core (IncidentEndpoints) за допомогою серверних обмежень і валідації вхідних параметрів |
| PostgreSQL → API → DOM | У БД може зберігатися раніше введений недовірений текст (наприклад, сутності з тегами на кшталт script) | У запиті використовується AsNoTracking(); в API застосовується окремий response DTO; у клієнті виконується безпечне виведення через textContent у app.js |

## Спосіб повернення до відомого seed-стану
Для очищення навчальних таблиць та відновлення початкових seed-даних використовується службова команда:

```bash
dotnet run --project src/SecureLab.Api --reset-database
```

