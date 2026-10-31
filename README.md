# sql-practice

Практикум по базам данных на **Microsoft SQL Server**: от `CREATE TABLE` и `SELECT` до хранимых процедур, оконных функций, CTE и интеграции с C#.

## Что внутри

| Путь | Назначение |
| --- | --- |
| `database/init.sql` | Идемпотентный скрипт: пересоздаёт `PracticeDB`, создаёт 4 таблицы и заполняет их тестовыми данными |
| `database/sql-notes.md` | Задания 1–30: SQL, фактические результаты, выводы |
| `src/PracticeDb.App` | Консольное приложение C# для заданий 28–30 (`Microsoft.Data.SqlClient`) |

## Требования

- Microsoft SQL Server 2019 или новее (проверено на SQL Server 2025 Express).
- SSMS, Azure Data Studio или `sqlcmd`.
- .NET SDK 10.0 или новее.
- NuGet-пакет `Microsoft.Data.SqlClient` восстанавливается автоматически.

## Быстрый старт

### 1. Создать базу

Из корня репозитория:

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -i database\init.sql
```

Если у вас другой экземпляр SQL Server, замените `.\SQLEXPRESS` на свой сервер.

> `init.sql` удаляет существующую `PracticeDB` и создаёт её заново. Не запускайте скрипт на базе с нужными вам данными.

Проверка:

```sql
USE PracticeDB;
SELECT COUNT(*) FROM Employees; -- 10
```

### 2. Задать строку подключения

Строка подключения **не хранится в репозитории**. Она передаётся через переменную окружения `PRACTICE_DB_CONNECTION`:

```powershell
$env:PRACTICE_DB_CONNECTION = "Server=.\SQLEXPRESS;Database=PracticeDB;Integrated Security=True;TrustServerCertificate=True"
```

Для постоянного хранения добавьте переменную в свойства системы или в локальный `.env` (он исключён через `.gitignore`).

### 3. Запустить приложение

```powershell
dotnet run --project src\PracticeDb.App
```

Откроется интерактивное меню:

```text
1 — показать сотрудников
2 — добавить сотрудника
3 — отчёт по отделам
0 — выход
```

Для неинтерактивной проверки предусмотрены аргументы:

```powershell
dotnet run --project src\PracticeDb.App -- select
dotnet run --project src\PracticeDb.App -- add "Новый Сотрудник" 55000.00 2
dotnet run --project src\PracticeDb.App -- report
```

## Что демонстрирует приложение

- **Задание 28** — подключение к `PracticeDB`, `OpenAsync`, `SqlDataReader`, `SELECT EmployeeName, Salary FROM Employees`, обработка `SqlException`.
- **Задание 29** — параметризованный `INSERT` через `SqlParameter` (без конкатенации), проверка добавленной записи, обработка FK violation и дубликатов.
- **Задание 30** — отчёт по отделам через `LEFT JOIN`, выравнивание колонок, `NULL` → `N/A`, обработка пустого результата.

## Задания

Все 30 заданий описаны в [`database/sql-notes.md`](database/sql-notes.md). Для каждого задания приведены SQL-запрос, фактический результат и краткий вывод.

Уровни:

- 1–10: DDL, INSERT, SELECT/WHERE, ORDER BY/TOP, агрегаты, GROUP BY/HAVING, JOIN.
- 11–20: self-join, подзапросы, EXISTS, UNION, UPDATE/DELETE, транзакции, VIEW, индексы.
- 21–30: процедуры, функции, оконные функции, CTE, CASE, даты, C# + SQL.

## Схема базы

```text
Departments 1 ─── * Employees
                      │  ▲
                      │  └── ManagerId (self-reference)
                      │
                      * 
               EmployeeProjects * ─── 1 Projects
```

- `Departments` — отделы.
- `Employees` — сотрудники; `DepartmentId` и `ManagerId` — внешние ключи.
- `Projects` — проекты.
- `EmployeeProjects` — назначения сотрудников на проекты с количеством часов.

## Безопасность

- Строка подключения читается только из `PRACTICE_DB_CONNECTION`.
- В репозитории нет паролей, `.env` и файлов базы (`*.mdf`, `*.ldf`, `*.bak`).
- Все SQL-запросы в C# используют параметры, а не конкатенацию.

## Проверка перед сдачей

```powershell
# 1. Пересоздать базу с нуля
sqlcmd -S .\SQLEXPRESS -E -C -i database\init.sql

# 2. Собрать приложение
dotnet build src\PracticeDb.App -c Release

# 3. Задать строку подключения и проверить три режима
$env:PRACTICE_DB_CONNECTION = "Server=.\SQLEXPRESS;Database=PracticeDB;Integrated Security=True;TrustServerCertificate=True"
dotnet run --project src\PracticeDb.App -c Release -- select
dotnet run --project src\PracticeDb.App -c Release -- add "Проверка" 50000.00 1
dotnet run --project src\PracticeDb.App -c Release -- report
```
